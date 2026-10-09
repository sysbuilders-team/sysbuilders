using UnityEngine;

namespace Network.Discovery
{
    /// <summary>
    /// Persistence of the TOFU (trust on first use) of the host public key.
    /// The key is the address used for the connection: if the host changes IP (DHCP), it is treated
    /// as a new host - a limitation inherent to TOFU, mitigated by the log warning and by the
    /// fingerprint visible in the room list.
    /// </summary>
    public static class KnownHostStore
    {
        // v2: the v1 fingerprints were stored while the host still generated a new key on every run,
        // so they were junk that refused every reconnection. With the host key now persisted on disk,
        // the new namespace starts clean.
        private const string KeyPrefix = "SysBuilders.Tofu.v2.";

        public static string Get(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return string.Empty;
            return PlayerPrefs.GetString(KeyPrefix + Normalize(host), string.Empty);
        }

        public static void Store(string host, string fingerprint)
        {
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fingerprint)) return;

            PlayerPrefs.SetString(KeyPrefix + Normalize(host), fingerprint.Trim());
            PlayerPrefs.Save();
        }

        public static void Forget(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return;

            PlayerPrefs.DeleteKey(KeyPrefix + Normalize(host));
            PlayerPrefs.Save();
        }

        private static string Normalize(string host) => host.Trim().ToLowerInvariant();
    }
}
