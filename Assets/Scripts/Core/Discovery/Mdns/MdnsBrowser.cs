using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Core.Discovery.Mdns
{
    /// <summary>
    /// mDNS browser: asks for the service type on the multicast group and keeps the room list with
    /// expiry driven by the TTL of the PTR record announced by the host.
    ///
    /// The query is sent WITHOUT the QU bit (multicast reply, RFC 6762 section 5.4 default), so two
    /// instances on the same machine (Editor + build sharing port 5353 with SO_REUSEADDR) both receive
    /// the response. See SendQuery for the full rationale.
    ///
    /// Goodbye (TTL 0) removes the room immediately.
    /// </summary>
    public sealed class MdnsBrowser : IRoomBrowser
    {
        public static readonly TimeSpan DefaultQueryInterval = TimeSpan.FromSeconds(15);
        public static readonly TimeSpan DefaultEntryTtl = TimeSpan.FromSeconds(60);

        private readonly IMulticastSocket _socket;
        private readonly IPAddress _group;
        private readonly int _port;
        private readonly TimeSpan _queryInterval;
        private readonly TimeSpan _fallbackEntryTtl;
        private readonly Dictionary<string, Entry> _rooms = new Dictionary<string, Entry>(StringComparer.Ordinal);

        private DateTime _nextQueryUtc;
        private bool _versionWarned;

        public MdnsBrowser(IMulticastSocket socket, IPAddress group, int port,
            TimeSpan? queryInterval = null, TimeSpan? fallbackEntryTtl = null)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _group = group ?? throw new ArgumentNullException(nameof(group));
            if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));

            _port = port;
            _queryInterval = queryInterval ?? DefaultQueryInterval;
            _fallbackEntryTtl = fallbackEntryTtl ?? DefaultEntryTtl;
        }

        public bool IsBrowsing { get; private set; }

        public IReadOnlyCollection<DiscoveredRoom> Rooms => _rooms.Values.Select(e => e.Room).ToList();

        public event Action<DiscoveredRoom> OnRoomFound;
        public event Action<DiscoveredRoom> OnRoomUpdated;
        public event Action<string> OnRoomLost;
        public event Action<string> OnDiagnostic;

        public void StartBrowsing(DateTime utcNow)
        {
            IsBrowsing = true;
            SendQuery();
            _nextQueryUtc = utcNow + _queryInterval;
        }

        public void StopBrowsing()
        {
            IsBrowsing = false;
            foreach (string sessionId in _rooms.Keys.ToList())
            {
                RemoveRoom(sessionId, "busca encerrada");
            }
        }

        public void Refresh(DateTime utcNow)
        {
            if (!IsBrowsing) IsBrowsing = true;

            SendQuery();
            _nextQueryUtc = utcNow + _queryInterval;
        }

        public void HandleDatagram(MulticastDatagram datagram, DateTime utcNow)
        {
            if (!MdnsCodec.TryDecode(datagram.Payload, out MdnsMessage message)) return;
            if (!message.IsResponse) return;

            if (!MdnsRecordBuilder.TryParseRoom(message, datagram.Remote?.Address, out RoomAdvertisement room,
                    out int ttlSeconds)) return;

            // TTL 0 in the PTR = goodbye: we remove before the version check, because the signal is
            // unambiguous and always beneficial (the room no longer exists).
            if (ttlSeconds <= 0)
            {
                RemoveRoom(room.SessionId, "o host encerrou a sala");
                return;
            }

            if (room.Version != RoomAdvertisement.CurrentVersion)
            {
                if (!_versionWarned)
                {
                    _versionWarned = true;
                    OnDiagnostic?.Invoke(
                        $"Sala '{room.RoomName}' anunciada em v{room.Version}, diferente da nossa v{RoomAdvertisement.CurrentVersion}. " +
                        "Atualize o jogo para jogar com esse host.");
                }
                return;
            }

            TimeSpan entryTtl = ttlSeconds > 0 ? TimeSpan.FromSeconds(ttlSeconds) : _fallbackEntryTtl;
            var discovered = new DiscoveredRoom(room, datagram.Remote?.Address, DiscoveryTier.Mdns, utcNow);

            if (_rooms.TryGetValue(room.SessionId, out var existing))
            {
                bool changed = !existing.Room.Room.HasSameContent(room);
                _rooms[room.SessionId] = new Entry(discovered, utcNow, entryTtl);

                if (changed) OnRoomUpdated?.Invoke(discovered);
            }
            else
            {
                _rooms[room.SessionId] = new Entry(discovered, utcNow, entryTtl);
                OnRoomFound?.Invoke(discovered);
            }
        }

        public void Tick(DateTime utcNow)
        {
            ExpireRooms(utcNow);

            if (!IsBrowsing) return;
            if (utcNow < _nextQueryUtc) return;

            // Same ordering as MdnsResponder: advance before sending, so a failing SendQuery does
            // not repeat the exception on every frame while the network is down.
            _nextQueryUtc = utcNow + _queryInterval;
            SendQuery();
        }

        public void Dispose()
        {
            StopBrowsing();
        }

        private void SendQuery()
        {
            // WITHOUT the QU bit on purpose: we ask for a MULTICAST reply (the RFC 6762 section 5.4 default).
            // Reason measured in the field: with a unicast reply to the address on port 5353, two processes
            // on the SAME machine (Editor + build, or two instances on a lab PC) share the port with
            // SO_REUSEADDR and the OS delivers the unicast to only ONE of the sockets - which may be the
            // host's, so the client never sees the room. A multicast reply reaches every socket joined to
            // the group, with no ambiguity.
            byte[] payload = MdnsCodec.EncodeQuery(0, MdnsRecordBuilder.ServiceType, MdnsRecordType.PTR,
                unicastResponse: false);
            _socket.SendMulticast(payload, _group, _port);
        }

        private void ExpireRooms(DateTime utcNow)
        {
            List<string> expired = null;

            foreach (var pair in _rooms)
            {
                if (utcNow - pair.Value.LastSeenUtc <= pair.Value.Ttl) continue;

                expired ??= new List<string>();
                expired.Add(pair.Key);
            }

            if (expired == null) return;

            foreach (string sessionId in expired)
            {
                RemoveRoom(sessionId, "TTL expirado sem re-anuncio");
            }
        }

        private void RemoveRoom(string sessionId, string reason)
        {
            if (string.IsNullOrEmpty(sessionId)) return;
            if (!_rooms.Remove(sessionId)) return;

            OnRoomLost?.Invoke($"{sessionId} ({reason})");
        }

        private readonly struct Entry
        {
            public readonly DiscoveredRoom Room;
            public readonly DateTime LastSeenUtc;
            public readonly TimeSpan Ttl;

            public Entry(DiscoveredRoom room, DateTime lastSeenUtc, TimeSpan ttl)
            {
                Room = room;
                LastSeenUtc = lastSeenUtc;
                Ttl = ttl;
            }
        }
    }
}
