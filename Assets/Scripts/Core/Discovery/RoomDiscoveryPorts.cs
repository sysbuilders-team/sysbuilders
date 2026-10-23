using System;
using System.Collections.Generic;

namespace Core.Discovery
{
    /// <summary>
    /// READ port for room discovery.
    ///
    /// The presentation layer (Sys.UI) depends ONLY on this interface, which lives in Core: it
    /// knows neither RoomDiscoveryService nor any network type. The network layer implements it
    /// and is resolved at runtime - the dependency direction stays UI -> Core, never UI -> Network
    /// (which is why Sys.UI.asmdef still does not reference Sys.Network).
    /// </summary>
    public interface IRoomDirectory
    {
        IReadOnlyCollection<DiscoveredRoom> Rooms { get; }
        bool IsBrowsing { get; }

        /// <summary>Network interface in use, so the player knows where we are looking.</summary>
        string ActiveInterface { get; }

        /// <summary>Immediate query that keeps the current list.</summary>
        void Refresh();

        void StopBrowsing();

        event Action OnRoomsChanged;
        event Action<string> OnDiagnostic;
    }

    /// <summary>
    /// ACTION port: "join this room". The UI raises the event; whoever knows how to talk to
    /// Mirror is the implementer (the menu flow). That way the UI needs neither Mirror nor network.
    /// </summary>
    public interface IRoomJoinActions
    {
        void JoinRoom(DiscoveredRoom room);

        /// <summary>
        /// Raised when a fingerprint mismatch is detected while connecting to a host.
        /// Subscribers (the room list) can prompt the player to trust the new key.
        /// </summary>
        event Action<string, string> OnHostKeyMismatch;

        /// <summary>
        /// Overwrite the stored fingerprint with the pending one and retry the
        /// connection. Returns false when there is no pending mismatch.
        /// </summary>
        bool TrustPendingKeyAndRetry();
    }
}
