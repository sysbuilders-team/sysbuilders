using System;
using Core.Discovery;
using TMPro;
using UnityEngine;

namespace UI
{
	/// <summary>
	/// Menu flow facade: navigation, player preferences and status labels.
	///
	/// Lives in Sys.UI and talks to the network layer only through IMenuNetworkActions (a Core
	/// port), so this assembly still does not reference Sys.Network. It keeps the public entry
	/// points the scene's buttons and the room list are wired to.
	/// </summary>
	public class MenuUIController : MonoBehaviour, IRoomJoinActions
	{
		[Header("Navegação")]
		[Tooltip("Centralizador de telas do menu (MenuNavigator). Se vazio, é resolvido na cena.")]
		[SerializeField] private MonoBehaviour _navigatorBehaviour;

		[Header("Referências")]
		[SerializeField] private TMP_InputField _playerNameInputField;
		[SerializeField] private TMP_InputField _ipInputField;
		[SerializeField] private TMP_Text _localIpText;
		[SerializeField] private TMP_Text _discoveryStatusText;

		[Header("Rede")]
		[Tooltip("Componente que implementa IMenuNetworkActions. Se vazio, é resolvido na cena.")]
		[SerializeField] private MonoBehaviour _networkBehaviour;

		private IMenuNavigationActions _navigator;
		private IMenuNetworkActions _network;

		/// <summary>Resolved on first use so scene event wiring cannot outrun Awake.</summary>
		private IMenuNetworkActions Network
		{
			get
			{
				if (_network != null) return _network;

				if (_networkBehaviour is IMenuNetworkActions assigned)
				{
					_network = assigned;
					return _network;
				}

				foreach (var behaviour in FindObjectsByType<MonoBehaviour>(
							FindObjectsInactive.Include, FindObjectsSortMode.None))
				{
					if (behaviour is not IMenuNetworkActions found) continue;

					_network = found;
					break;
				}

				return _network;
			}
		}

		private void Awake()
		{
			var network = Network;
			if (network != null)
			{
				network.OnDiagnostic += HandleDiscoveryDiagnostic;
			}
			else
			{
				Debug.LogError("[MenuUI] No IMenuNetworkActions found in the scene; network actions are disabled.");
			}

			ResolveNavigator();
		}

		/// <summary>Injected by tests and by anything that already holds the navigator.</summary>
		public void SetNavigator(IMenuNavigationActions navigator) => _navigator = navigator;

		private void Start()
		{
			string savedName = PlayerPrefs.GetString("PlayerName", "Jogador");
			if (_playerNameInputField != null)
			{
				_playerNameInputField.text = savedName;
			}

			if (_localIpText != null)
			{
				_localIpText.text = $"Seu IP Local: {GetLocalIPAddress()}";
			}

			UpdateDiscoveryStatus();
		}

		private void OnDestroy()
		{
			if (_network != null)
			{
				_network.OnDiagnostic -= HandleDiscoveryDiagnostic;
			}
		}

		// IRoomJoinActions: forwarded so the room list (also in Sys.UI) can drive join and the
		// TOFU recovery flow without referencing Mirror or GameRoomManager.

		public event Action<string, string> OnHostKeyMismatch
		{
			add
			{
				var network = Network;
				if (network != null) network.OnHostKeyMismatch += value;
			}
			remove
			{
				var network = Network;
				if (network != null) network.OnHostKeyMismatch -= value;
			}
		}

		public bool TrustPendingKeyAndRetry()
		{
			var network = Network;
			return network != null && network.TrustPendingKeyAndRetry();
		}

		public void SetPlayerName(string newName)
		{
			if (!string.IsNullOrWhiteSpace(newName))
			{
				PlayerPrefs.SetString("PlayerName", newName.Trim());
				PlayerPrefs.Save();
			}
		}

		public void SelectTheme(string themeName)
		{
			if (string.IsNullOrWhiteSpace(themeName)) return;

			string sanitized = themeName.Trim();
			PlayerPrefs.SetString("SelectedTheme", sanitized);
			PlayerPrefs.Save();
			Debug.Log($"[MenuUI] Theme selected: {sanitized}");
		}

		public void HostGame()
		{
			SaveCurrentPlayerName();
			ResolveNavigator()?.ShowScreen(MenuScreen.RoomWait);

			Network?.Host(
				PlayerPrefs.GetString("SelectedTheme", "Biblioteca"),
				PlayerPrefs.GetString("PlayerName", "Jogador"));
		}

		public void SairDaSala()
		{
			Network?.StopRoom();
			ResolveNavigator()?.ShowScreen(MenuScreen.Main);

			Debug.Log("[MenuUI] Room left and NetworkManager reset.");
		}

		public void BuscarPartidas()
		{
			ResolveNavigator()?.ShowScreen(MenuScreen.LobbyBrowser);

			Network?.StartBrowsing();
			UpdateDiscoveryStatus();

			Debug.Log($"[MenuUI] Searching rooms over mDNS on interface {Network?.ActiveInterface ?? "indisponível"}.");
		}

		public void PararBusca() => Network?.StopBrowsing();

		public void JoinRoom(DiscoveredRoom room)
		{
			if (room == null) return;

			SaveCurrentPlayerName();
			ResolveNavigator()?.ShowScreen(MenuScreen.RoomWait);
			Network?.Join(room);
		}

		public void JoinByIP()
		{
			string ipAddress = (_ipInputField != null && !string.IsNullOrWhiteSpace(_ipInputField.text))
				? _ipInputField.text.Trim()
				: "localhost";

			SaveCurrentPlayerName();
			ResolveNavigator()?.ShowScreen(MenuScreen.RoomWait);
			Network?.JoinByIp(ipAddress);
		}

		public void AbrirStart()
		{
			bool alreadyInRoom = Network != null && Network.IsInRoomOrHosting;
			ResolveNavigator()?.ShowScreen(alreadyInRoom ? MenuScreen.RoomWait : MenuScreen.Main);
		}

		public void AbrirLobby() => ResolveNavigator()?.ShowScreen(MenuScreen.LobbyBrowser);
		public void AbrirTutorial() => ResolveNavigator()?.ShowScreen(MenuScreen.Tutorial);
		public void AbrirSettings() => ResolveNavigator()?.ShowScreen(MenuScreen.Settings);

		private IMenuNavigationActions ResolveNavigator()
		{
			if (_navigator != null) return _navigator;

			if (_navigatorBehaviour is IMenuNavigationActions assigned)
			{
				_navigator = assigned;
				return _navigator;
			}

			foreach (var behaviour in FindObjectsByType<MonoBehaviour>(
						FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				if (behaviour is not IMenuNavigationActions nav) continue;

				_navigator = nav;
				break;
			}

			return _navigator;
		}

		private void SaveCurrentPlayerName()
		{
			if (_playerNameInputField != null && !string.IsNullOrWhiteSpace(_playerNameInputField.text))
			{
				PlayerPrefs.SetString("PlayerName", _playerNameInputField.text.Trim());
				PlayerPrefs.Save();
			}
		}

		private void HandleDiscoveryDiagnostic(string message)
		{
			if (_discoveryStatusText != null) _discoveryStatusText.text = message;
		}

		private void UpdateDiscoveryStatus()
		{
			if (_discoveryStatusText == null) return;

			string network = Network != null ? Network.ActiveInterface : "indisponível";
			_discoveryStatusText.text = $"Rede: {network}";
		}

		private static string GetLocalIPAddress()
		{
			var candidates = NetworkInterfaceSelector.Enumerate();
			if (NetworkInterfaceSelector.TrySelectPrimary(candidates, out var selected, out string reason))
			{
				return selected.Address.ToString();
			}

			Debug.LogWarning($"[MenuUI] Could not determine the local IPv4 address: {reason}");
			return "127.0.0.1";
		}
	}
}
