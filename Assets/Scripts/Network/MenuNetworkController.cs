using System;
using Core.Discovery;
using Mirror;
using Network.Discovery;
using UnityEngine;

namespace Network
{
	/// <summary>
	/// Network half of the menu: discovery, room advertisement, host/join and TOFU forwarding.
	///
	/// Extracted from the menu UI so that the UI (Sys.UI) can stay free of Mirror. The UI reaches
	/// this component through IMenuNetworkActions, a Core port resolved on the scene.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("SysBuilders/Network/Menu Network Controller")]
	public sealed class MenuNetworkController : MonoBehaviour, IMenuNetworkActions
	{
		[Tooltip("Deixe vazio: o serviço é criado/obtido no mesmo GameObject do NetworkManager.")]
		[SerializeField] private RoomDiscoveryService _discovery;

		private string _sessionId;
		private RoomAdvertisement _advertisement;
		private bool _subscribed;

		/// <summary>Discovery diagnostics, forwarded so the UI can show them in its status label.</summary>
		public event Action<string> OnDiagnostic;

		public event Action<string, string> OnHostKeyMismatch
		{
			add
			{
				var room = NetworkManager.singleton as GameRoomManager;
				if (room != null) room.OnHostKeyMismatch += value;
			}
			remove
			{
				var room = NetworkManager.singleton as GameRoomManager;
				if (room != null) room.OnHostKeyMismatch -= value;
			}
		}

		public string ActiveInterface
		{
			get
			{
				var discovery = ResolveDiscovery();
				return discovery != null ? discovery.ActiveInterface : "indisponível";
			}
		}

		public bool IsInRoomOrHosting => NetworkServer.active || (NetworkClient.active && NetworkClient.isConnected);

		private void Awake()
		{
			_sessionId = Guid.NewGuid().ToString("N");
		}

		private void OnDestroy()
		{
			if (_discovery != null && _subscribed)
			{
				_discovery.OnDiagnostic -= HandleDiscoveryDiagnostic;
				_subscribed = false;
			}
		}

		public void Host(string theme, string playerName)
		{
			if (NetworkManager.singleton == null) return;

			var room = NetworkManager.singleton as GameRoomManager;
			if (room != null) room.SetChosenTheme(theme);

			NetworkManager.singleton.StartHost();

			if (!NetworkServer.active)
			{
				Debug.LogWarning("[MenuNetwork] StartHost nao iniciou. Sala NAO anunciada.");
				return;
			}

			StartAdvertising(theme, playerName);
			ResolveDiscovery()?.StartBrowsing();

			Debug.Log("[MenuNetwork] Host started, room published over mDNS.");
		}

		public void StopRoom()
		{
			if (NetworkManager.singleton == null) return;

			if (NetworkServer.active)
			{
				NetworkManager.singleton.StopHost();
			}
			else if (NetworkClient.active)
			{
				NetworkManager.singleton.StopClient();
			}
		}

		public void StartBrowsing()
		{
			ResolveDiscovery()?.StartBrowsing();
		}

		public void StopBrowsing()
		{
			ResolveDiscovery()?.StopBrowsing();
		}

		public void Join(DiscoveredRoom room)
		{
			if (room == null) return;

			JoinAddress(
				room.Address != null ? room.Address.ToString() : null,
				room.Port,
				$"sala '{room.Room.RoomName}'");
		}

		public void JoinByIp(string address)
		{
			JoinAddress(string.IsNullOrWhiteSpace(address) ? "localhost" : address.Trim(), 0, "IP informado");
		}

		public bool TrustPendingKeyAndRetry()
		{
			var room = NetworkManager.singleton as GameRoomManager;
			return room != null && room.TrustPendingKeyAndRetry();
		}

		private void JoinAddress(string address, int port, string description)
		{
			if (NetworkManager.singleton == null) return;

			ResolveDiscovery()?.StopBrowsing();

			// A discovered room may advertise a port different from the local one.
			if (port > 0) TransportPortAdapter.TryApplyPort(port);

			NetworkManager.singleton.networkAddress =
				string.IsNullOrWhiteSpace(address) ? "localhost" : address;

			NetworkManager.singleton.StartClient();

			Debug.Log($"[MenuNetwork] Connecting to {description}: " +
					  $"{NetworkManager.singleton.networkAddress}:{TransportPortAdapter.ResolvePort()}");
		}

		private RoomDiscoveryService ResolveDiscovery()
		{
			if (_discovery != null)
			{
				Subscribe(_discovery);
				return _discovery;
			}

			// Includes inactive objects: the service may sit under an inactive parent.
			foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
						 FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				if (behaviour is not RoomDiscoveryService service) continue;

				_discovery = service;
				Subscribe(service);
				break;
			}

			return _discovery;
		}

		private void Subscribe(RoomDiscoveryService discovery)
		{
			if (_subscribed || discovery == null) return;

			discovery.OnDiagnostic += HandleDiscoveryDiagnostic;
			_subscribed = true;
		}

		private void HandleDiscoveryDiagnostic(string message) => OnDiagnostic?.Invoke(message);

		private void StartAdvertising(string theme, string playerName)
		{
			var discovery = ResolveDiscovery();
			if (discovery == null) return;

			_advertisement = BuildAdvertisement(CountPlayersInRoom(), theme, playerName);
			discovery.StartAdvertising(_advertisement);
		}

		private RoomAdvertisement BuildAdvertisement(int players, string theme, string playerName)
		{
			if (string.IsNullOrWhiteSpace(playerName)) playerName = "Jogador";
			if (string.IsNullOrWhiteSpace(theme)) theme = "Biblioteca";

			return new RoomAdvertisement(
				sessionId: _sessionId,
				roomName: $"Sala de {playerName}",
				hostName: playerName,
				theme: theme,
				port: TransportPortAdapter.ResolvePort(),
				playerCount: players,
				maxPlayers: 2,
				encrypted: TransportPortAdapter.ActiveTransportIsEncrypted(),
				publicKeyFingerprint: TransportPortAdapter.ActiveTransportFingerprint(),
				state: RoomState.Lobby);
		}

		private static int CountPlayersInRoom()
		{
			if (!NetworkServer.active) return 1;
			return Mathf.Max(1, NetworkServer.connections.Count);
		}
	}
}
