using System;
using Core.Discovery;
using Mirror;
using Network.Discovery;
using TMPro;
using UnityEngine;

namespace Network
{
	public class MenuManager : MonoBehaviour, IRoomJoinActions
	{
		[Header("Painéis de Navegação")]
		[SerializeField] private GameObject _startPanel;
		[SerializeField] private GameObject _lobbyPanel;
		[SerializeField] private GameObject _settingsPanel;
		[SerializeField] private GameObject _tutorialPanel;

		[Header("Referências de Rede")]
		[Tooltip("Deixe vazio: o serviço é criado/obtido no mesmo GameObject do NetworkManager.")]
		[SerializeField] private RoomDiscoveryService _discovery;
		[SerializeField] private TMP_InputField _playerNameInputField;
		[SerializeField] private TMP_InputField _ipInputField;
		[SerializeField] private TMP_Text _localIpText;
		[SerializeField] private TMP_Text _discoveryStatusText;

		private string _sessionId;
		private RoomAdvertisement _advertisement;

		public RoomDiscoveryService Discovery => _discovery;

		private void Awake()
		{
			_sessionId = Guid.NewGuid().ToString("N");
		}

		/// <summary>
		/// Resolve o serviço de descoberta sob demanda.
		///
		/// Ele NÃO pode ser criado neste GameObject: o MenuManager vive no MenuPanel, que fica
		/// INATIVO quando o lobby (ou a sala) está aberto — e um serviço em objeto inativo não roda
		/// Update, ou seja, para de processar os datagramas recebidos e de fundir a lista. Era por
		/// isso que a lista de salas só se atualizava ao sair e voltar do painel.
		/// Quem cria é o GameRoomManager, no GameObject do NetworkManager, que fica sempre ativo.
		/// </summary>
		private RoomDiscoveryService ResolveDiscovery()
		{
			if (_discovery != null) return _discovery;

			// Inclui inativos: o serviço pode estar num objeto pai inativo em algum momento.
			foreach (var behaviour in FindObjectsByType<MonoBehaviour>(
						FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				if (behaviour is not RoomDiscoveryService service) continue;

				_discovery = service;
				_discovery.OnDiagnostic += HandleDiscoveryDiagnostic;
				break;
			}

			return _discovery;
		}

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
			if (_discovery != null)
			{
				_discovery.OnDiagnostic -= HandleDiscoveryDiagnostic;
			}
		}

		public void SetPlayerName(string newName)
		{
			if (!string.IsNullOrWhiteSpace(newName))
			{
				PlayerPrefs.SetString("PlayerName", newName.Trim());
				PlayerPrefs.Save();
			}
		}


		public void HostGame()
		{
			if (NetworkManager.singleton == null) return;

			SaveCurrentPlayerName();

			// Grava o tema antes de StartHost: o MatchSyncBridge exige ChosenTheme definido,
			// para os dois peers não divergirem por PlayerPrefs local.
			var room = NetworkManager.singleton as GameRoomManager;
			if (room != null)
			{
				room.SetChosenTheme(PlayerPrefs.GetString("SelectedTheme", "Biblioteca"));
			}

			NetworkManager.singleton.StartHost();
			StartAdvertisingRoom();
			ResolveDiscovery()?.StartBrowsing();

			Debug.Log("[MenuManager] Host iniciado, sala publicada por mDNS e descoberta ouvindo a rede local.");
		}

		/// <summary>
		/// Sai da sala de espera e volta ao menu inicial. O host encerra a sessão (o cliente cai
		/// junto); o cliente apenas desconecta. O painel da sala se esconde sozinho, porque
		/// IRoomLobbySource.IsInRoom deixa de ser verdadeiro.
		/// </summary>
		public void SairDaSala()
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

			AbrirStart();

			Debug.Log("[MenuManager] Saída da sala de espera solicitada.");
		}

		private void StartAdvertisingRoom()
		{
			var discovery = ResolveDiscovery();
			if (discovery == null) return;

			_advertisement = BuildAdvertisement(CountPlayersInRoom());
			discovery.StartAdvertising(_advertisement);
		}

		// A contagem de jogadores do anúncio (1/2 -> 2/2) NÃO é publicada daqui: este componente vive
		// no MenuPanel, que é desligado quando a sala ou a lista de partidas aparece — e objeto
		// inativo não roda Update, então o anúncio congelava em 1/2. Quem cuida disso agora é o
		// GameRoomManager, no objeto do NetworkManager (sempre ativo).

		private RoomAdvertisement BuildAdvertisement(int players)
		{
			string playerName = PlayerPrefs.GetString("PlayerName", "Jogador");
			if (string.IsNullOrWhiteSpace(playerName)) playerName = "Jogador";

			return new RoomAdvertisement(
					sessionId: _sessionId,
					roomName: $"Sala de {playerName}",
					hostName: playerName,
					theme: PlayerPrefs.GetString("SelectedTheme", "Biblioteca"),
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


		/// <summary>Botão "Buscar Partidas": abre o lobby e começa a escutar mDNS/multicast.</summary>
		public void BuscarPartidas()
		{
			AbrirLobby();

			var discovery = ResolveDiscovery();
			if (discovery == null) return;

			discovery.StartBrowsing();
			UpdateDiscoveryStatus();
			Debug.Log($"[MenuManager] Procurando salas via mDNS na interface {discovery.ActiveInterface}.");
		}

		public void PararBusca()
		{
			ResolveDiscovery()?.StopBrowsing();
		}

		/// <summary>Chamado pela lista de salas do lobby.</summary>
		public void JoinRoom(DiscoveredRoom room)
		{
			if (room == null) return;

			JoinAddress(room.Address != null ? room.Address.ToString() : null, room.Port, $"sala '{room.Room.RoomName}'");
		}

		public void JoinByIP()
		{
			string ipAddress = (_ipInputField != null && !string.IsNullOrWhiteSpace(_ipInputField.text))
				? _ipInputField.text.Trim()
				: "localhost";

			JoinAddress(ipAddress, 0, "IP informado");
		}

		private void JoinAddress(string address, int port, string description)
		{
			if (NetworkManager.singleton == null) return;

			SaveCurrentPlayerName();
			ResolveDiscovery()?.StopBrowsing();

			// A sala descoberta pode anunciar porta diferente da configurada localmente.
			if (port > 0) TransportPortAdapter.TryApplyPort(port);

			NetworkManager.singleton.networkAddress =
				string.IsNullOrWhiteSpace(address) ? "localhost" : address;

			NetworkManager.singleton.StartClient();

			Debug.Log($"[MenuManager] Conectando à {description}: " +
					$"{NetworkManager.singleton.networkAddress}:{TransportPortAdapter.ResolvePort()}");
		}


		public void AbrirStart() => TrocarPainel(_startPanel);
		public void AbrirLobby() => TrocarPainel(_lobbyPanel);
		public void AbrirTutorial() => TrocarPainel(_tutorialPanel);
		public void AbrirSettings() => TrocarPainel(_settingsPanel);

		private void TrocarPainel(GameObject painelAtivo)
		{
			if (_startPanel != null) _startPanel.SetActive(_startPanel == painelAtivo);
			if (_lobbyPanel != null) _lobbyPanel.SetActive(_lobbyPanel == painelAtivo);
			if (_tutorialPanel != null) _tutorialPanel.SetActive(_tutorialPanel == painelAtivo);
			if (_settingsPanel != null) _settingsPanel.SetActive(_settingsPanel == painelAtivo);
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

			string network = _discovery != null ? _discovery.ActiveInterface : "indisponível";
			_discoveryStatusText.text = $"Rede: {network}";
		}

		/// <summary>
		/// IPv4 of the primary interface, using the same selection as discovery. Dns.GetHostEntry
		/// blocks the main thread and can stall the menu for seconds when DNS is misconfigured.
		/// </summary>
		private static string GetLocalIPAddress()
		{
			var candidates = NetworkInterfaceSelector.Enumerate();
			if (NetworkInterfaceSelector.TrySelectPrimary(candidates, out var selected, out string reason))
			{
				return selected.Address.ToString();
			}

			Debug.LogWarning($"[MenuManager] Could not determine the local IPv4 address: {reason}");
			return "127.0.0.1";
		}
	}
}
