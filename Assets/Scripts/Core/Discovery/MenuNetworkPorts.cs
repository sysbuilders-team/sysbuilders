using System;

namespace Core.Discovery
{
    /// <summary>
    /// Port the menu UI uses to drive host, join and discovery without referencing Mirror or the
    /// network assembly. Implemented by the network layer (MenuNetworkController) and resolved at
    /// runtime, so the dependency stays UI -> Core and never UI -> Network.
    /// </summary>
    public interface IMenuNetworkActions
    {
        /// <summary>Network interface discovery is using, for the menu's status label.</summary>
        string ActiveInterface { get; }

        /// <summary>True while hosting or connected to a room.</summary>
        bool IsInRoomOrHosting { get; }

        /// <summary>Discovery diagnostics that the menu should display.</summary>
        event Action<string> OnDiagnostic;

        /// <summary>Fingerprint mismatch surfaced by the TOFU validation.</summary>
        event Action<string, string> OnHostKeyMismatch;

        void Host(string theme, string playerName);
        void StopRoom();

        void StartBrowsing();
        void StopBrowsing();

        void Join(DiscoveredRoom room);
        void JoinByIp(string address);

        bool TrustPendingKeyAndRetry();
    }
}
