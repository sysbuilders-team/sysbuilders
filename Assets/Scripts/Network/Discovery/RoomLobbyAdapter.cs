using System;
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
        private int _readyCount;
        private int _lastStateHash;
        private GameObject _lastLocalPlayerObject;
        private GameRoomPlayer _cachedLocalPlayer;

        public event Action OnSlotsChanged;

        // Being connected to a room is enough to show the panel: the local room player can take a
        // few frames to spawn on the client, and that is what left the client without a Ready button.
        public bool IsInRoom => NetworkClient.active && NetworkClient.isConnected;

        public bool CanSetReady => LocalRoomPlayer() != null;

        public int MaxPlayers => _maxPlayers;

        public IReadOnlyList<RoomSlotInfo> Slots => _slots;

        public int ReadyCount => _readyCount;

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
		    // Poll a cheap hash of the room state. Rebuild only when it changes.
		    // Beats the previous behavior — full Rebuild plus allocations every
		    // frame — while avoiding a hard dependency on Mirror's room
		    // callbacks, which differ across Mirror versions.
		    bool disconnected = !NetworkClient.active && !NetworkServer.active;

		    if (disconnected)
		    {
		        // The adapter lives on the DontDestroyOnLoad NetworkManager, so none of its
		        // fields are cleared when the host stops or the client drops. Reset explicitly:
		        // otherwise an identical 2-player/1-ready state in the next match hashes to the
		        // same value, Update skips Rebuild and the panel keeps stale RoomSlotInfo that
		        // point at destroyed player objects.
		        if (_lastStateHash != 0 || _slots.Count > 0) ResetForDisconnect();

		        if (_disableMirrorRoomGui) ApplyMirrorGuiFlag();
		        return;
		    }

		    int hash = ComputeStateHash();
		    if (hash != _lastStateHash)
		    {
		        _lastStateHash = hash;
		        Rebuild();
		        OnSlotsChanged?.Invoke();
		    }

		    if (_disableMirrorRoomGui) ApplyMirrorGuiFlag();
		}

		public void ResetForDisconnect()
		{
		    _lastStateHash = 0;
		    _cachedLocalPlayer = null;
		    _lastLocalPlayerObject = null;
		    _slots.Clear();
		    _ordered.Clear();
		    _readyCount = 0;
		    OnSlotsChanged?.Invoke();
		}

		private int ComputeStateHash()
		{
		    var room = RoomManager();
		    if (room == null) return 0;

		    unchecked
		    {
		        // The local player object spawns after the room state is synced. If its presence
		        // is not in the hash, Rebuild can run once while LocalRoomPlayer() is still null
		        // and never run again, leaving CanSetReady false and the panel stuck on
		        // "Entrando na sala..." even though the player is fully spawned.
		        var localPlayer = NetworkClient.localPlayer;
		        int hash = IsInRoom ? 1 : 0;
		        hash = hash * 31 + room.roomSlots.Count;
		        hash = hash * 31 + (localPlayer != null ? localPlayer.GetInstanceID() : 0);

		        foreach (var slot in room.roomSlots)
		        {
		            if (slot == null) continue;
		            hash = hash * 31 + slot.index;
		            hash = hash * 31 + (slot.readyToBegin ? 1 : 0);
		            if (slot is GameRoomPlayer rp && !string.IsNullOrEmpty(rp.playerName))
		            {
		                hash = hash * 31 + rp.playerName.GetHashCode();
		            }
		        }
		        return hash;
		    }
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
			_readyCount = 0;

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
				if (slot.readyToBegin) _readyCount++;
            }
        }

        private static NetworkRoomManager RoomManager() => NetworkManager.singleton as NetworkRoomManager;

        private GameRoomPlayer LocalRoomPlayer()
		{
		    if (!NetworkClient.active) return null;

		    var playerObject = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
		    if (playerObject != _lastLocalPlayerObject)
		    {
		        _lastLocalPlayerObject = playerObject;
		        _cachedLocalPlayer = playerObject != null ? playerObject.GetComponent<GameRoomPlayer>() : null;
		    }

		    return _cachedLocalPlayer;
		} 
    }
}
