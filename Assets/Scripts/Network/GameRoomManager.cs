using System;
using System.IO;
using Core.Discovery;
using Mirror;
using Mirror.Transports.Encryption;
using Network.Discovery;
using UnityEngine;

namespace Network
{
	public class GameRoomManager : NetworkRoomManager
	{
		[Header("Descoberta de Salas")]
		[Tooltip("Deixe vazio: o serviço é obtido/criado no mesmo GameObject do NetworkManager.")]
		[SerializeField] private RoomDiscoveryService _discovery;

		[Tooltip("Recusa a conexão quando a chave pública do host mudou em relação ao que foi confiado (TOFU).")]
		[SerializeField] private bool _rejectOnFingerprintMismatch = true;

		[Tooltip("Accepts a host that presents no public key fingerprint. Off by default: an unverifiable host is not trusted silently.")]
		[SerializeField] private bool _acceptHostsWithoutFingerprint;

		public string ChosenTheme { get; private set; }
		public string ChosenTrailId { get; private set; }

		/// <summary>
		/// Define o tema da partida antes de StartHost. O MatchSyncBridge exige que este valor
		/// já esteja definido, para os dois peers não divergirem por estado local.
		/// </summary>
		public void SetChosenTheme(string theme)
		{
			ChosenTheme = string.IsNullOrWhiteSpace(theme) ? "Biblioteca" : theme;
		}

		public override void Start()
		{
			base.Start();

			maxConnections = 2;
			
			if (_discovery == null)
			{
				_discovery = GetComponent<RoomDiscoveryService>();
				if (_discovery == null) _discovery = gameObject.AddComponent<RoomDiscoveryService>();
			}

			SetupEncryption();
		}

		private void SetupEncryption()
		{
			var activeTransport = Transport.active;
			if (activeTransport is EncryptionTransport encryptionTransport)
			{
				EnsurePersistentServerKey(encryptionTransport);
				encryptionTransport.ClientValidateServerPubKey = EncryptionTransport.ValidationMode.Callback;
				encryptionTransport.OnClientValidateServerPubKey = ValidateServerPubKey;

				Debug.Log($"[GameRoomManager] Criptografia AES256-GCM ativada via EncryptionTransport (chave do host: {encryptionTransport.ServerKeypairPath}).");
			}
			else
			{
				Debug.LogWarning("[GameRoomManager] O transporte ativo não é EncryptionTransport. A conexão trafegará sem criptografia.");
			}
		}

		/// <summary>
		/// TOFU só faz sentido com identidade estável. Por padrão o EncryptionTransport gera um par
		/// de chaves NOVO a cada execução — ou seja, todo host era "uma chave diferente" e o cliente
		/// recusava toda reconexão ("Remote public key failed validation. This is fine.").
		/// Aqui o par é criado uma vez e guardado em disco, então a chave só muda se for outra
		/// máquina ou se o arquivo for apagado — e aí o aviso de chave trocada passa a significar
		/// algo de verdade.
		/// </summary>
		private void EnsurePersistentServerKey(EncryptionTransport transport)
		{
			string path = ServerKeyPath;

			if (!File.Exists(path))
			{
				try
				{
					EncryptionCredentials.Generate().SaveToFile(path);
					Debug.Log($"[GameRoomManager] Par de chaves do host criado em {path}.");
				}
				catch (Exception error)
				{
					Debug.LogWarning($"[GameRoomManager] Não consegui gravar a chave do host em {path} ({error.Message}). " +
							"Sem persistência o host troca de identidade a cada execução e o TOFU recusa reconexões.");
					return;
				}
			}

			transport.ServerKeypairPath = path;
			transport.ServerLoadKeyPairFromFile = true;
		}

		/// <summary>Caminho do par de chaves persistido do host (por usuário, fora do projeto).</summary>
		private static string ServerKeyPath =>
			Path.Combine(Application.persistentDataPath, "sysbuilders-server-keys.json");

		/// <summary>
		/// TOFU: aceita e registra a chave do host na primeira conexão; a partir daí, recusa (por
		/// padrão) se a chave mudar. Fecha o buraco que a lista de salas expõe: escolher um host na
		/// lista sem nenhuma verificação de identidade.
		/// </summary>
		private bool ValidateServerPubKey(PubKeyInfo pubKeyInfo)
		{
			string host = NetworkManager.singleton != null ? NetworkManager.singleton.networkAddress : string.Empty;

			// PubKeyInfo e struct no Mirror: nao existe null aqui (Fingerprint pode vir vazio).
			string presented = pubKeyInfo.Fingerprint;

			TofuDecision decision = TofuPolicy.Evaluate(KnownHostStore.Get(host), presented);
			string description = TofuPolicy.Describe(decision, string.IsNullOrWhiteSpace(host) ? "host" : host);

			if (decision == TofuDecision.Mismatch)
			{
				// The stored key is kept on purpose. Forgetting it here turned the rejection into a
				// two-click bypass: the next attempt evaluated as first use and accepted the new key
				// automatically. Trusting a rotated key is an explicit action now, driven by a
				// confirmation UI that calls KnownHostStore.Forget.
				Debug.LogError($"[GameRoomManager] {description}");
			}
			else
			{
				Debug.Log($"[GameRoomManager] {description}");
			}

			// A UI do lobby assina este canal: a decisão do TOFU aparece no status, não só no console.
			_discovery?.ReportDiagnostic(description);

			if (TofuPolicy.ShouldStore(decision)) KnownHostStore.Store(host, presented);

			return TofuPolicy.ShouldAccept(decision, _rejectOnFingerprintMismatch, _acceptHostsWithoutFingerprint);
		}

		/// <summary>
		/// Mantém o anúncio mDNS com a contagem de jogadores em dia (1/2 -> 2/2).
		///
		/// Fica AQUI, e não no MenuManager: aquele componente vive no MenuPanel, que é desligado
		/// quando a sala de espera ou a lista de partidas abre — e objeto inativo não roda Update,
		/// então o anúncio congelava em 1/2 justamente quando havia dois jogadores na sala.
		/// </summary>
		public override void Update()
		{
			base.Update();
			PublishPlayerCount();
		}

		private void PublishPlayerCount()
		{
			var discovery = _discovery;
			if (discovery == null || !discovery.IsAdvertising) return;
			if (!NetworkServer.active) return;

			var current = discovery.CurrentAdvertisement;
			if (current == null) return;

			int players = roomSlots.Count;
			if (current.PlayerCount == players) return;

			discovery.UpdateAdvertisement(current.WithPlayers(players, RoomState.Lobby));
		}

		public override void OnRoomServerPlayersReady()
		{
			
			Debug.Log($"[GameRoomManager] Tema da sala: {ChosenTheme}");

			// A sala sai da lista ao entrar na partida: o anúncio não faz sentido com o jogo rodando
			// (e o goodbye evita que a entrada fique expirando sozinha na lista dos outros).
			_discovery?.StopAdvertising();
			_discovery?.StopBrowsing();

			ServerChangeScene(GameplayScene);
		}

		public override void OnStopServer()
		{
			_discovery?.StopAdvertising();
			base.OnStopServer();
		}

		public override void OnStopClient()
		{
			_discovery?.StopBrowsing();
			base.OnStopClient();
		}
	}
}
