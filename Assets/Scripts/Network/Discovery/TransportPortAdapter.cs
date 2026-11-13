using Mirror;
using Mirror.Transports.Encryption;
using UnityEngine;

namespace Network.Discovery
{
    /// <summary>
    /// Isolates the single Mirror dependency of the discovery layer: the active transport port.
    /// Everything else (Core.Discovery) is plain C# and does not know Mirror.
    /// </summary>
    public static class TransportPortAdapter
    {
        public static bool TryGetActivePort(out int port)
        {
            port = 0;

            var transport = Transport.active;
            if (transport == null) return false;

            if (transport is PortTransport portTransport && portTransport.Port > 0)
            {
                port = portTransport.Port;
                return true;
            }

            return false;
        }

        public static int ResolvePort(int fallback = 7777)
        {
            return TryGetActivePort(out int port) ? port : fallback;
        }

        /// <summary>Used when joining a discovered room: the advertised port may not be the local one.</summary>
        public static bool TryApplyPort(int port)
        {
            if (port <= 0 || port > 65535) return false;

            var transport = Transport.active;
            if (transport is PortTransport portTransport)
            {
                portTransport.Port = (ushort)port;
                return true;
            }

            return false;
        }

        public static bool ActiveTransportIsEncrypted()
        {
            var transport = Transport.active;
            return transport != null && transport.IsEncrypted;
        }

        /// <summary>
        /// Fingerprint of the host public key, when the active transport exposes one.
        /// Only meaningful on the host and only after StartHost: ServerStart is what loads the
        /// persisted keypair, so calling this before hosting returns null.
        /// </summary>
        public static string ActiveTransportFingerprint()
        {
            var transport = Transport.active;
            return transport is EncryptionTransport encryption ? encryption.EncryptionPublicKeyFingerprint : null;
        }
    }
}
