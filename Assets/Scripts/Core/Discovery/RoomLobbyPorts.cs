using System.Collections.Generic;

namespace Core.Discovery
{
    /// <summary>One waiting-room seat, as the UI needs to see it.</summary>
    public sealed class RoomSlotInfo
    {
        public int Index { get; }
        public string PlayerName { get; }
        public bool IsReady { get; }
        public bool IsLocalPlayer { get; }

        public RoomSlotInfo(int index, string playerName, bool isReady, bool isLocalPlayer)
        {
            Index = index;
            PlayerName = playerName;
            IsReady = isReady;
            IsLocalPlayer = isLocalPlayer;
        }

        public string DisplayName => string.IsNullOrWhiteSpace(PlayerName) ? $"Jogador {Index + 1}" : PlayerName;

        public string ReadyLabel => IsReady ? "Pronto" : "Aguardando";

        public override string ToString() => $"{DisplayName} ({ReadyLabel}){(IsLocalPlayer ? " [você]" : string.Empty)}";
    }

    /// <summary>
    /// WAITING ROOM port (the Mirror "room").
    ///
    /// The UI reads the seats and asks for "ready" through here; the adapter in the network layer
    /// is who talks to Mirror. Same shape as the other ports: Sys.UI references neither Sys.Network
    /// nor Mirror, and that is what allows replacing the default Mirror box with our own panel.
    /// </summary>
    public interface IRoomLobbySource
    {
        /// <summary>True when the local player is already inside a waiting room.</summary>
        bool IsInRoom { get; }

        int MaxPlayers { get; }

        IReadOnlyList<RoomSlotInfo> Slots { get; }

        int ReadyCount { get; }

        bool IsLocalPlayerReady { get; }

        /// <summary>
        /// True when the ready state can be changed already (the local room player exists). While
        /// it is false the button stays disabled - avoids clicking a dead button right after connecting.
        /// </summary>
        bool CanSetReady { get; }

        void SetLocalReady(bool ready);
    }
}
