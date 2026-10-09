using System.Collections.Generic;
using Core.Discovery;
using Mirror;
using UnityEngine;

namespace Network.Discovery
{
    /// <summary>
    /// Adapter from Mirror NetworkRoomManager/NetworkRoomPlayer to the IRoomLobbySource port.
    ///
    /// Two Responsibilities:
    ///  1. translate the Mirror room (seats, names, ready) into the model the UI understands;
    ///  2. TURN OFF the default Mirror GUI (showRoomGUI of the manager and of each room player),
    ///     which is replaced by our own panel in Sys.UI.
    ///
    /// Lives on the NetworkManager GameObject (always active) - an inactive object does not run Update.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SysBuilders/Network/Room Lobby Adapter")]
    public sealed class RoomLobbyAdapter : MonoBehaviour, IRoomLobbySource
    {
        [SerializeField] private int _maxPlayers = 2;

        [Tooltip("Desliga a caixa de sala desenhada pelo Mirror (substituída pelo painel próprio).")]
        [SerializeField] private bool _disableMirrorRoomGui = true;

        private readonly List<RoomSlotInfo> _slots = new List<RoomSlotInfo>(2);
        private readonly List<NetworkRoomPlayer> _ordered = new List<NetworkRoomPlayer>(2);

        // Being connected to a room is enough to show the panel: the local room player can take a
        // few frames to spawn on the client, and that is what left the client without a Ready button.
        public bool IsInRoom => NetworkClient.active && NetworkClient.isConnected;

        public bool CanSetReady => LocalRoomPlayer() != null;

        public int MaxPlayers => _maxPlayers;

        public IReadOnlyList<RoomSlotInfo> Slots
        {
            get
            {
                Rebuild();
                return _slots;
            }
        }

        public int ReadyCount
        {
            get
            {
                Rebuild();

                int count = 0;
                foreach (var slot in _slots)
                {
                    if (slot.IsReady) count++;
                }

                return count;
            }
        }

        public bool IsLocalPlayerReady
        {
            get
            {
                var local = LocalRoomPlayer();
                return local != null && local.readyToBegin;
            }
        }

        public void SetLocalReady(bool ready)
        {
            var local = LocalRoomPlayer();
            if (local == null) return;
            if (local.readyToBegin == ready) return;

            local.CmdChangeReadyState(ready);
        }

        private void Update()
        {
            if (!_disableMirrorRoomGui) return;

            Rebuild();
            ApplyMirrorGuiFlag();
        }

        /// <summary>Keeps the default Mirror box off (including for whoever joins later).</summary>
        private void ApplyMirrorGuiFlag()
        {
            var room = RoomManager();
            if (room != null) room.showRoomGUI = false;

            foreach (var player in _ordered)
            {
                if (player != null) player.showRoomGUI = false;
            }
        }

        private void Rebuild()
        {
            _slots.Clear();
            _ordered.Clear();

            var room = RoomManager();
            if (room == null) return;

            foreach (var slot in room.roomSlots)
            {
                if (slot != null) _ordered.Add(slot);
            }

            _ordered.Sort((a, b) => a.index.CompareTo(b.index));

            var local = LocalRoomPlayer();

            foreach (var slot in _ordered)
            {
                string playerName = slot is GameRoomPlayer roomPlayer ? roomPlayer.playerName : null;
                _slots.Add(new RoomSlotInfo(slot.index, playerName, slot.readyToBegin, slot == local));
            }
        }

        private static NetworkRoomManager RoomManager() => NetworkManager.singleton as NetworkRoomManager;

        private static GameRoomPlayer LocalRoomPlayer()
        {
            if (!NetworkClient.active) return null;

            var player = NetworkClient.localPlayer;
            return player != null ? player.GetComponent<GameRoomPlayer>() : null;
        }
    }
}
