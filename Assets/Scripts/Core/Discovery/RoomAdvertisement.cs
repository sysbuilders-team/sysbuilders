using System;
using System.Text;

namespace Core.Discovery
{
    public enum RoomState
    {
        Lobby = 0,
        InGame = 1
    }

    /// <summary>
    /// Single room advertisement model, published as mDNS PTR/SRV/TXT/A records
    /// (MdnsRecordBuilder). If another discovery transport ever shows up, it reuses this
    /// same model - neither the UI nor the browsers' cache changes.
    /// </summary>
    public sealed class RoomAdvertisement
    {
        public const int CurrentVersion = 1;
        public const int MaxRoomNameLength = 32;
        public const int MaxShortTextLength = 24;
        public const int DefaultMaxPlayers = 2;

        public int Version { get; }
        public string SessionId { get; }
        public string RoomName { get; }
        public string HostName { get; }
        public string Theme { get; }
        public int Port { get; }
        public int PlayerCount { get; }
        public int MaxPlayers { get; }
        public bool Encrypted { get; }
        public RoomState State { get; }

        /// <summary>
        /// Fingerprint of the host public key, when known. Optional on purpose:
        /// it is not a security boundary (whoever intercepts controls both the advertisement and
        /// the handshake), it serves as the room visible identity. The real check is done by TOFU
        /// during the handshake (TofuPolicy) over the fingerprint presented by EncryptionTransport.
        /// </summary>
        public string PublicKeyFingerprint { get; }

        public RoomAdvertisement(
            string sessionId,
            string roomName,
            string hostName,
            string theme,
            int port,
            int playerCount,
            int maxPlayers = DefaultMaxPlayers,
            bool encrypted = true,
            RoomState state = RoomState.Lobby,
            string publicKeyFingerprint = null,
            int version = CurrentVersion)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("O Id da sessao nao pode ser vazio.", nameof(sessionId));
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), port, "A porta precisa estar entre 1 e 65535.");
            if (version <= 0)
                throw new ArgumentOutOfRangeException(nameof(version), version, "A versao do protocolo precisa ser positiva.");
            if (maxPlayers <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPlayers), maxPlayers, "A sala precisa de ao menos uma vaga.");

            Version = version;
            SessionId = sessionId;
            RoomName = SanitizeRoomName(roomName);
            HostName = SanitizeShortText(hostName, "Host");
            Theme = SanitizeShortText(theme, string.Empty);
            Port = port;
            MaxPlayers = maxPlayers;
            PlayerCount = playerCount < 0 ? 0 : (playerCount > maxPlayers ? maxPlayers : playerCount);
            Encrypted = encrypted;
            State = state;
            PublicKeyFingerprint = string.IsNullOrWhiteSpace(publicKeyFingerprint) ? null : publicKeyFingerprint.Trim();
        }

        public RoomAdvertisement WithPlayers(int playerCount, RoomState state)
        {
            return new RoomAdvertisement(SessionId, RoomName, HostName, Theme, Port, playerCount, MaxPlayers,
                Encrypted, state, PublicKeyFingerprint, Version);
        }

        public RoomAdvertisement WithVersion(int version)
        {
            return new RoomAdvertisement(SessionId, RoomName, HostName, Theme, Port, PlayerCount, MaxPlayers,
                Encrypted, State, PublicKeyFingerprint, version);
        }

        /// <summary>
        /// Compares only the published content (what the UI shows). It exists so an identical
        /// re-announcement does not fire "room updated".
        /// </summary>
        public bool HasSameContent(RoomAdvertisement other)
        {
            if (other == null) return false;

            return Version == other.Version
                   && string.Equals(SessionId, other.SessionId, StringComparison.Ordinal)
                   && string.Equals(RoomName, other.RoomName, StringComparison.Ordinal)
                   && string.Equals(HostName, other.HostName, StringComparison.Ordinal)
                   && string.Equals(Theme, other.Theme, StringComparison.Ordinal)
                   && Port == other.Port
                   && PlayerCount == other.PlayerCount
                   && MaxPlayers == other.MaxPlayers
                   && Encrypted == other.Encrypted
                   && State == other.State
                   && string.Equals(PublicKeyFingerprint, other.PublicKeyFingerprint, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The room name is free text typed by the player and goes to everyone else list:
        /// keep letters/digits/space/-/_ (accents included), collapse spaces and cap the length.
        /// </summary>
        public static string SanitizeRoomName(string value)
        {
            string cleaned = SanitizeShortText(value, string.Empty, MaxRoomNameLength);
            return string.IsNullOrEmpty(cleaned) ? "Sala" : cleaned;
        }

        public static string SanitizeShortText(string value, string fallback, int maxLength = MaxShortTextLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;

            var builder = new StringBuilder(Math.Min(value.Length, maxLength));
            bool lastWasSpace = false;

            foreach (char c in value.Trim())
            {
                bool allowed = char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ';
                if (!allowed) continue;

                if (c == ' ')
                {
                    if (lastWasSpace) continue;
                    lastWasSpace = true;
                }
                else
                {
                    lastWasSpace = false;
                }

                if (builder.Length >= maxLength) break;
                builder.Append(c);
            }

            string result = builder.ToString().Trim();
            return string.IsNullOrEmpty(result) ? fallback : result;
        }

        public override string ToString()
        {
            return $"'{RoomName}' por {HostName} ({PlayerCount}/{MaxPlayers}, tema={Theme}, porta={Port}, v{Version}, id={SessionId})";
        }
    }
}
