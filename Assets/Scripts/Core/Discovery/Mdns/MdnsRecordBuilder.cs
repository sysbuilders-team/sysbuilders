using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;

namespace Core.Discovery.Mdns
{
    /// <summary>
    /// Translates RoomAdvertisement into mDNS records (PTR/SRV/TXT/A) and back.
    /// The service type name is versioned so different builds can coexist.
    /// </summary>
    public static class MdnsRecordBuilder
    {
        public const string ServiceType = "_sysbuilders-v1._udp.local";
        public const string MetaServiceName = "_services._dns-sd._udp.local";
        public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(120);

        private const string KeyVersion = "v";
        private const string KeyId = "id";
        private const string KeyRoom = "room";
        private const string KeyHost = "host";
        private const string KeyPlayers = "players";
        private const string KeyMax = "max";
        private const string KeyTheme = "theme";
        private const string KeyEncrypted = "enc";
        private const string KeyState = "state";

        public static string ShortId(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return "0000";

            var builder = new StringBuilder(8);
            foreach (char c in sessionId)
            {
                if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
                if (builder.Length == 8) break;
            }

            return builder.Length == 0 ? "0000" : builder.ToString();
        }

        public static string InstanceNameFor(RoomAdvertisement room) => $"{room.RoomName} ({ShortId(room.SessionId)})";

        public static string InstanceFqdn(RoomAdvertisement room) => $"{InstanceNameFor(room)}.{ServiceType}";

        public static string TargetNameFor(RoomAdvertisement room) => $"sb-{ShortId(room.SessionId)}.local";

        public static IReadOnlyList<string> BuildTxtStrings(RoomAdvertisement room)
        {
            var entries = new List<string>
            {
                $"{KeyVersion}={room.Version}",
                $"{KeyId}={room.SessionId}",
                $"{KeyRoom}={room.RoomName}",
                $"{KeyHost}={room.HostName}",
                $"{KeyPlayers}={room.PlayerCount}",
                $"{KeyMax}={room.MaxPlayers}",
                $"{KeyTheme}={room.Theme}",
                $"{KeyEncrypted}={(room.Encrypted ? 1 : 0)}",
                $"{KeyState}={(room.State == RoomState.InGame ? "game" : "lobby")}"
            };

            return entries;
        }

        public static IReadOnlyList<MdnsRecord> AnnouncementRecords(RoomAdvertisement room, IPAddress address,
            TimeSpan ttl, bool cacheFlush)
        {
            var records = new List<MdnsRecord>(4)
            {
                MdnsRecord.Ptr(ServiceType, InstanceFqdn(room), ttl, cacheFlush),
                MdnsRecord.Srv(InstanceFqdn(room), room.Port, TargetNameFor(room), ttl, cacheFlush: cacheFlush),
                MdnsRecord.Txt(InstanceFqdn(room), BuildTxtStrings(room), ttl, cacheFlush)
            };

            if (address != null) records.Add(MdnsRecord.AddressA(TargetNameFor(room), address, ttl, cacheFlush));

            return records;
        }

        /// <summary>Goodbye: the same records with TTL 0 (RFC 6762).</summary>
        public static IReadOnlyList<MdnsRecord> GoodbyeRecords(RoomAdvertisement room)
        {
            return AnnouncementRecords(room, null, TimeSpan.Zero, cacheFlush: true);
        }

        /// <summary>
        /// Extracts the room from an mDNS response. Requires the PTR of our service type + SRV (port) +
        /// TXT with the Id; the address comes from the A record when present, otherwise from the source IP.
        /// </summary>
        public static bool TryParseRoom(MdnsMessage message, IPAddress sourceAddress,
            out RoomAdvertisement room, out int ttlSeconds)
        {
            room = null;
            ttlSeconds = 0;

            if (message == null || message.IsTruncated) return false;

            var records = new List<MdnsRecord>(message.Answers.Count + message.Additionals.Count);
            records.AddRange(message.Answers);
            records.AddRange(message.Additionals);

            MdnsRecord pointer = null;
            foreach (var record in records)
            {
                if (record.Type != MdnsRecordType.PTR) continue;
                if (!MdnsCodec.NamesEqual(record.Name, ServiceType)) continue;
                if (string.IsNullOrEmpty(record.TargetName)) continue;

                pointer = record;
                break;
            }

            if (pointer == null) return false;

            ttlSeconds = pointer.Ttl;
            string instance = pointer.TargetName;

            int port = 0;
            string target = null;
            IReadOnlyList<string> text = null;
            IPAddress address = null;

            foreach (var record in records)
            {
                if (!MdnsCodec.NamesEqual(record.Name, instance)) continue;

                switch (record.Type)
                {
                    case MdnsRecordType.SRV:
                        port = record.SrvPort;
                        target = record.TargetName;
                        break;
                    case MdnsRecordType.TXT:
                        text = record.Text;
                        break;
                }
            }

            if (port <= 0 || text == null) return false;

            if (!TryParseTxt(text, out var values)) return false;
            if (!values.TryGetValue(KeyId, out string sessionId) || string.IsNullOrEmpty(sessionId)) return false;

            int version = RoomAdvertisement.CurrentVersion;
            if (values.TryGetValue(KeyVersion, out string versionText) &&
                int.TryParse(versionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedVersion) &&
                parsedVersion > 0)
            {
                version = parsedVersion;
            }

            foreach (var record in records)
            {
                if (record.Type != MdnsRecordType.A || record.Address == null) continue;
                if (target != null && !MdnsCodec.NamesEqual(record.Name, target)) continue;

                address = record.Address;
                break;
            }

            address ??= sourceAddress;

            room = new RoomAdvertisement(
                sessionId,
                Get(values, KeyRoom, "Sala"),
                Get(values, KeyHost, "Host"),
                Get(values, KeyTheme, string.Empty),
                port,
                ParseInt(values, KeyPlayers),
                Math.Max(1, ParseInt(values, KeyMax, RoomAdvertisement.DefaultMaxPlayers)),
                ParseInt(values, KeyEncrypted, 1) != 0,
                string.Equals(Get(values, KeyState, "lobby"), "game", StringComparison.OrdinalIgnoreCase)
                    ? RoomState.InGame
                    : RoomState.Lobby,
                null,
                version);

            return true;
        }

        public static bool TryParseTxt(IReadOnlyList<string> entries, out Dictionary<string, string> values)
        {
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (entries == null) return false;

            foreach (string entry in entries)
            {
                if (string.IsNullOrEmpty(entry)) continue;

                int separator = entry.IndexOf('=');
                if (separator <= 0) continue;

                string key = entry.Substring(0, separator).Trim();
                string value = entry.Substring(separator + 1);
                if (key.Length == 0) continue;

                values[key] = value;
            }

            return values.Count > 0;
        }

        private static string Get(IReadOnlyDictionary<string, string> values, string key, string fallback)
            => values.TryGetValue(key, out string value) ? value : fallback;

        private static int ParseInt(IReadOnlyDictionary<string, string> values, string key, int fallback = 0)
            => values.TryGetValue(key, out string text) &&
               int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
    }
}
