using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Core.Evaluation;
using Core.Session;
using Managers;

namespace Network
{
		/// <summary>
		/// Client/server sync bridge for a running match
		/// Trust model: the server validates shape (authorization, bounds, sequence),
		/// not	values. Evaluation scores are computed client-side and relayed; a
		/// malicious client can still send any in-bound score and win. Closing that
		/// gap requires the server to recompute from the received diagram, tracked
		/// as future work.
		/// </summary>
		public class MatchSyncBridge : NetworkBehaviour
		{
		    /// <summary>Upper bound on the abort reason transmitted over the wire.</summary>
		    private const int MaxAbortReasonLength = 120;
            private const int RequiredPlayers = 2;

		    /// <summary>
		    /// Per-turn raw score ceiling. Each revealed requirement awards 100, and
		    /// at most one requirement is revealed per turn, so rawScore cannot
		    /// legitimately exceed 100 * turnNumber.
		    /// </summary>
		    private const int MaxRawScorePerTurn = 100;

		    /// <summary>
		    /// Per-turn penalty ceiling. Penalties scale with diagram size and smell
		    /// severity; observed maximum on a normal game is well under 2000. The
		    /// cap keeps legitimate players from being rejected while bounding the
		    /// arithmetic on the server side.
		    /// </summary>
		    private const int MaxPenaltyPerTurn = 5000;

		[SerializeField] private GameManager gameManager;
		[SerializeField] private RequirementManager requirementManager;

		[SyncVar] private string _theme;
		[SyncVar(hook = nameof(OnTrailSynced))] private string _trailId;

		private readonly HashSet<PlayerSlot> _readySlots = new HashSet<PlayerSlot>();
		private readonly HashSet<PlayerSlot> _evaluatedSlots = new HashSet<PlayerSlot>();
		private MatchSession _subscribedSession;
		private bool _matchAborted;

		public override void OnStartServer()
		{
			base.OnStartServer();
			_readySlots.Clear();
			_evaluatedSlots.Clear();

            NetworkServer.OnDisconnectedEvent += HandleServerDisconnect;

			var roomManager = NetworkManager.singleton as GameRoomManager;
			if (roomManager == null || string.IsNullOrEmpty(roomManager.ChosenTheme))
			{
				const string reason = "Tema da partida não definido.";
				Debug.LogError($"[MatchSyncBridge] {reason} Abortando partida.");

				// Notify connected clients before aborting locally: without this, a client connected
				// to a host that never finished setup gets dropped by StopHost with no explanation.
				// Same pattern as the error path inside the try block below.
				if (NetworkServer.active)
				{
					try { RpcNotifyMatchStartFailed(reason); }
					catch (Exception notifyEx)
					{
						Debug.LogError($"[MatchSyncBridge] Nao foi possivel notificar os clientes da falha: {notifyEx.Message}");
					}
				}

				AbortLocalMatch(reason);
				return;
			}

			_theme = roomManager.ChosenTheme;
			if (requirementManager == null)
			{
				if (!TryGetComponent(out requirementManager))
				{
					requirementManager = FindFirstObjectByType<RequirementManager>();
				}
			}

			try
			{
				if (requirementManager == null)
				{
					throw new InvalidOperationException("RequirementManager não encontrado para sortear a trilha.");
				}

				_trailId = requirementManager.GetRandomTrailId(_theme);

				if (string.IsNullOrEmpty(_trailId))
				{
					throw new InvalidOperationException($"Nenhuma trilha encontrada para o tema '{_theme}'.");
				}

				Debug.Log($"[MatchSyncBridge] Server: tema={_theme} | trail={_trailId}");

				StartLocalMatch(_theme, _trailId, PlayerSlot.PlayerOne);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[MatchSyncBridge] Erro crítico ao iniciar partida no servidor: {ex.Message}");

				// Avisa os clientes (melhor esforco) e devolve o proprio servidor ao menu:
				// nao depender do ClientRpc voltar para a ponta local.
				if (NetworkServer.active)
				{
					try
					{
						RpcNotifyMatchStartFailed(ex.Message);
					}
					catch (Exception notifyEx)
					{
						Debug.LogError($"[MatchSyncBridge] Nao foi possivel notificar os clientes da falha: {notifyEx.Message}");
					}
				}

				AbortLocalMatch(ex.Message);
			}
		}

        public override void OnStopServer()
        {
                NetworkServer.OnDisconnectedEvent -= HandleServerDisconnect;
                    _readySlots.Clear();
                        _evaluatedSlots.Clear();
                            base.OnStopServer();
        }

		[ClientRpc]
		private void RpcNotifyMatchStartFailed(string reason)
		{
			AbortLocalMatch(reason);
		}

		// A client that failed to start the match notifies the server. Without
        // this the Host would keep playing alone, because only the server-side
        // error path triggers RpcNotifyMatchStartFailed. The command is reachable
        // by any connection (requiresAuthority = false), so it must reject
        // unknown senders and cap the reason before relaying.
        [Command(requiresAuthority = false)]
        private void CmdNotifyMatchStartFailed(string reason, NetworkConnectionToClient sender = null)
        {
            if (!IsKnownMatchParticipant(sender))
            {
                Debug.LogWarning("[MatchSyncBridge] CmdNotifyMatchStartFailed rejected: connection not authorized.");
                return;
            }

            string safeReason = TruncateForWire(reason, MaxAbortReasonLength);
            Debug.LogError($"[MatchSyncBridge] Cliente reportou falha ao iniciar a partida: {safeReason}");
            RpcNotifyMatchStartFailed(safeReason);
        }
		
		// Caminho unico de aborto (Host, Client e relay do servidor): loga e devolve a maquina
		// local para o menu. Idempotente, porque no host o RPC chega tambem na ponta que reportou.
		private void AbortLocalMatch(string reason)
		{
			if (_matchAborted) return;
			_matchAborted = true;

			Debug.LogError($"[MatchSyncBridge] Partida abortada: {reason}");

			if (gameManager != null)
			{
				gameManager.ReturnToMainMenu();
			}
		}

		/// <summary>
		/// A participant dropped while a match was running. The quorum sets are
		/// stale (the missing peer will never send ready or evaluation), so a
		/// subsequent phase advance would never reach RequiredPlayers. Abort
		/// cleanly: log, notify the remaining peer, and return to menu.
		/// </summary>
		private void HandleServerDisconnect(NetworkConnectionToClient conn)
		{
		    if (_matchAborted) return;
		    if (gameManager == null || gameManager.Session == null) return;

		    _readySlots.Clear();
		    _evaluatedSlots.Clear();

		    AbortLocalMatch("O adversário desconectou.");
		}

		private void OnTrailSynced(string oldTrailId, string newTrailId)
		{
			if (!isClientOnly) return;
			if (string.IsNullOrEmpty(newTrailId)) return;

			Debug.Log($"[MatchSyncBridge] Cliente sincronizou trilha: {newTrailId} (Tema: {_theme})");

			// A montagem da partida local pode falhar nesta ponta (ex.: JSON local desatualizado com
			// uma trilha de tamanho diferente sob o mesmo Id). Sem tratamento, a excecao sobe dentro
			// do hook de SyncVar e o cliente fica travado, sem UI e sem o aviso que o Host recebe.
			try
			{
				StartLocalMatch(_theme, newTrailId, PlayerSlot.PlayerTwo);
			}
			catch (Exception ex)
			{
				RecoverFromMatchStartFailure(ex.Message);
			}
		}

		// Nunca deixa a ponta local travada: tenta avisar o servidor e, independentemente do
		// resultado do aviso, devolve esta maquina para o menu. Nenhuma excecao escapa daqui,
		// porque este caminho roda dentro do hook de SyncVar do Mirror.
		private void RecoverFromMatchStartFailure(string reason)
		{
			try
			{
				CmdNotifyMatchStartFailed(reason);
			}
			catch (Exception notifyEx)
			{
				Debug.LogError($"[MatchSyncBridge] Nao foi possivel avisar o servidor da falha: {notifyEx.Message}");
			}

			try
			{
				AbortLocalMatch(reason);
			}
			catch (Exception abortEx)
			{
				Debug.LogError($"[MatchSyncBridge] Falha ao devolver o cliente para o menu: {abortEx.Message}");
			}
		}

		private void StartLocalMatch(string theme, string trailId, PlayerSlot slot)
		{
			gameManager.RequireOpponentResult = true;
			gameManager.StartMatch(theme, trailId, slot);
			_matchAborted = false;

			// A Session só existe depois do StartMatch (no OnEnable ela ainda era nula,
			// então a assinatura antiga nunca acontecia e o placar não era enviado).
			if (gameManager.Session != null && _subscribedSession != gameManager.Session)
			{
				_subscribedSession = gameManager.Session;
				_subscribedSession.OnEvaluationCompleted += HandleLocalEvaluationCompleted;
			}
		}

		public void NotifyLocalPlayerReady(PlayerSlot slot)
		{
			if (gameManager?.Session?.TurnManager == null) return;
			var tm = gameManager.Session.TurnManager;
			CmdSetReady((int)slot, tm.CurrentTurn, (int)tm.CurrentPhase);
		}

		private bool IsSenderAuthorizedForSlot(NetworkConnectionToClient sender, PlayerSlot slot)
		{
			bool isHost = sender == null || sender == NetworkServer.localConnection;
			if (slot == PlayerSlot.PlayerOne) return isHost;
			if (slot == PlayerSlot.PlayerTwo) return !isHost;
			return false;
		}

		[Command(requiresAuthority = false)]
		private void CmdSetReady(int slotIndex, int turnNumber, int phase, NetworkConnectionToClient sender = null)
		{
			var slot = (PlayerSlot)slotIndex;
			if (!IsSenderAuthorizedForSlot(sender, slot))
			{
				Debug.LogWarning($"[MatchSyncBridge] CmdSetReady rejeitado: conexão não autorizada para o slot {slot}.");
				return;
			}

			if (gameManager == null || gameManager.Session == null)
			{
				Debug.LogWarning("[MatchSyncBridge] CmdSetReady rejeitado: sessão não inicializada.");
				return;
			}

			var tm = gameManager.Session.TurnManager;

			if (turnNumber != tm.CurrentTurn)
			{
				Debug.LogWarning($"[MatchSyncBridge] CmdSetReady descartado: turno {turnNumber} não é o atual ({tm.CurrentTurn}).");
				return;
			}

			// Phase token: a ready from the wrong phase of the right turn would otherwise reach the
			// quorum and advance the wrong transition (for example, a Show ready arriving after the
			// server already moved to Build). Both sides are in lockstep because the server drives
			// advancement via RpcAdvancePhase, so strict equality is the correct check.
			if (phase != (int)tm.CurrentPhase)
			{
				Debug.LogWarning($"[MatchSyncBridge] CmdSetReady descartado: fase {phase} não é a atual ({(int)tm.CurrentPhase}).");
				return;
			}

			_readySlots.Add(slot);
			Debug.Log($"[MatchSyncBridge] Server: slot {slot} pronto ({_readySlots.Count}/2)");

			if (_readySlots.Count >= RequiredPlayers)
			{
				_readySlots.Clear();
				RpcAdvancePhase();
			}
		}

		[ClientRpc]
		private void RpcAdvancePhase()
		{
			if (gameManager == null || gameManager.Session == null)
			{
				Debug.LogWarning("[MatchSyncBridge] RpcAdvancePhase ignorado: sessão não inicializada (provavelmente retornando ao menu).");
				return;
			}

			Debug.Log("[MatchSyncBridge] RpcAdvancePhase recebido — avançar TurnManager local.");
			gameManager.Session.TurnManager.ForceAdvancePhase();
		}

		private void OnEnable()
		{
			if (gameManager == null) return;

			gameManager.OnLocalPlayerReady += HandleLocalPlayerReady;
			gameManager.OnReturnToMenuRequested += HandleReturnToMenu;
		}

		private void OnDisable()
		{
			if (gameManager == null) return;

			gameManager.OnLocalPlayerReady -= HandleLocalPlayerReady;
			gameManager.OnReturnToMenuRequested -= HandleReturnToMenu;
		}

		private void OnDestroy()
		{
			if (_subscribedSession != null)
			{
				_subscribedSession.OnEvaluationCompleted -= HandleLocalEvaluationCompleted;
				_subscribedSession = null;
			}
		}

		private void HandleLocalPlayerReady()
		{
			if (gameManager == null || gameManager.Session == null) return;
			NotifyLocalPlayerReady(gameManager.Session.LocalSlot);
		}

		private void HandleReturnToMenu()
		{
			if (NetworkServer.active)
			{
				NetworkManager.singleton.StopHost();
			}
			else if (NetworkClient.isConnected)
			{
				NetworkManager.singleton.StopClient();
			}
		}

		private void HandleLocalEvaluationCompleted(EvaluationResult localResult)
		{
			if (gameManager == null || gameManager.Session == null) return;
			SmellWireCodec.Encode(localResult.DetectedSmells, out int[] smellTypes, out int[] smellSeverities, out string[] smellClasses);
			var tm = gameManager.Session.TurnManager;
			CmdSendEvaluationResult(tm.CurrentTurn, (int)tm.CurrentPhase, (int)gameManager.Session.LocalSlot,
				localResult.RawScore, localResult.PenaltyScore,
				smellTypes, smellSeverities, smellClasses);
		}

		[Command(requiresAuthority = false)]
		private void CmdSendEvaluationResult(int turnNumber, int phase, int slotIndex, int rawScore, int penaltyScore,
			int[] smellTypes, int[] smellSeverities, string[] smellClasses,
			NetworkConnectionToClient sender = null)
		{
			var slot = (PlayerSlot)slotIndex;
			if (!IsSenderAuthorizedForSlot(sender, slot))
			{
				Debug.LogWarning($"[MatchSyncBridge] CmdSendEvaluationResult rejeitado: conexão não autorizada para o slot {slot}.");
				return;
			}

			if (gameManager == null || gameManager.Session == null)
			{
				Debug.LogWarning("[MatchSyncBridge] CmdSendEvaluationResult rejeitado: sessão não inicializada.");
				return;
			}

			// Shape validation before the relay. The server does not recompute
            // scores today, but it must not pass obviously-tampered values to
            // the other peer, and must not relay unbounded arrays.
            if (rawScore < 0 || rawScore > MaxRawScorePerTurn * turnNumber)
            {
                Debug.LogWarning($"[MatchSyncBridge] CmdSendEvaluationResult rejected: raw {rawScore} out of range for turn {turnNumber}.");
                return;
           if (penaltyScore < 0 || penaltyScore > MaxPenaltyPerTurn)
            {
                Debug.LogWarning($"[MatchSyncBridge] CmdSendEvaluationResult rejected: penalty {penaltyScore} out of range.");
                return;
            }

            if (smellTypes == null || smellSeverities == null || smellClasses == null)
            {
                Debug.LogWarning("[MatchSyncBridge] CmdSendEvaluationResult rejected: null smell arrays.");
                return;
            }

            if (smellTypes.Length != smellSeverities.Length || smellTypes.Length != smellClasses.Length)
            {
                Debug.LogWarning($"[MatchSyncBridge] CmdSendEvaluationResult rejected: array length mismatch ({smellTypes.Length}, {smellSeverities.Length}, {smellClasses.Length}).");
                return;
            }

            if (smellTypes.Length > SmellWireCodec.MaxSmellsPerResult)
            {
                Debug.LogWarning($"[MatchSyncBridge] CmdSendEvaluationResult rejected: {smellTypes.Length} smells exceeds cap of {SmellWireCodec.MaxSmellsPerResult}.");
                return;
            }


			var tm = gameManager.Session.TurnManager;

			if (turnNumber != tm.CurrentTurn)
			{
				Debug.LogWarning($"[MatchSyncBridge] CmdSendEvaluationResult descartado: turno {turnNumber} não é o atual ({tm.CurrentTurn}).");
				return;
			}

			if (phase != (int)tm.CurrentPhase)
			{
				Debug.LogWarning($"[MatchSyncBridge] CmdSendEvaluationResult descartado: fase {phase} não é a atual ({(int)tm.CurrentPhase}).");
				return;
			}

			/// <summary>
			/// A sender counts as a known participant when Mirror populated it with
			/// the local connection (host self-call), an active server connection
			/// (remote client), or left it null (direct call from tests). Unknown
			/// connections are rejected.
			/// </summary>
			private static bool IsKnownMatchParticipant(NetworkConnectionToClient sender)
			{
		    if (sender == null) return true;
		    if (sender == NetworkServer.localConnection) return true;
		    return NetworkServer.connections.Values.Contains(sender);
			}

			/// <summary>
			/// Truncates a string before it crosses the wire, so a hostile client
			/// cannot force the server and the other peer to allocate or log an
			/// arbitrary amount of text.
			/// </summary>
			private static string TruncateForWire(string value, int maxLength)
			{
			    if (string.IsNullOrEmpty(value)) return string.Empty;
			    return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
			}

			RpcReceiveEvaluationResult(turnNumber, slotIndex, rawScore, penaltyScore, smellTypes, smellSeverities, smellClasses);

			_evaluatedSlots.Add(slot);
			Debug.Log($"[MatchSyncBridge] Server: avaliação do slot {slot} do turno {turnNumber} recebida ({_evaluatedSlots.Count}/2)");

			if (_evaluatedSlots.Count >= RequiredPlayers)
			{
				_evaluatedSlots.Clear();
				RpcAdvancePhase();
			}
		}

		[ClientRpc]
		private void RpcReceiveEvaluationResult(int turnNumber, int targetSlot, int rawScore, int penaltyScore,
			int[] smellTypes, int[] smellSeverities, string[] smellClasses)
		{
			if (gameManager == null || gameManager.Session == null) return;
			if ((int)gameManager.Session.LocalSlot == targetSlot) return;

			var rivalResult = new EvaluationResult(
				rawScore,
				penaltyScore,
				new List<string>(),
				SmellWireCodec(smellTypes, smellSeverities, smellClasses)
			);

			Debug.Log($"[MatchSyncBridge] Slot rival {targetSlot} (turno {turnNumber}) — raw:{rawScore} penalty:{penaltyScore} total:{rivalResult.TotalScore}");
			gameManager.ReceiveOpponentEvaluation(turnNumber, rivalResult);
		}
	}
}
