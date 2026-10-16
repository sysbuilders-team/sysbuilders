using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Core.Evaluation;
using Core.Session;
using Managers;

namespace Network
{
	public class MatchSyncBridge : NetworkBehaviour
	{
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

		[ClientRpc]
		private void RpcNotifyMatchStartFailed(string reason)
		{
			AbortLocalMatch(reason);
		}

		// Um cliente que nao conseguiu montar a partida avisa o servidor: sem isso o Host seguiria
		// jogando sozinho, porque so o caminho de erro do servidor dispara RpcNotifyMatchStartFailed.
		[Command(requiresAuthority = false)]
		private void CmdNotifyMatchStartFailed(string reason)
		{
			Debug.LogError($"[MatchSyncBridge] Cliente reportou falha ao iniciar a partida: {reason}");
			RpcNotifyMatchStartFailed(reason);
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

			if (_readySlots.Count >= 2)
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

			RpcReceiveEvaluationResult(turnNumber, slotIndex, rawScore, penaltyScore, smellTypes, smellSeverities, smellClasses);

			_evaluatedSlots.Add(slot);
			Debug.Log($"[MatchSyncBridge] Server: avaliação do slot {slot} do turno {turnNumber} recebida ({_evaluatedSlots.Count}/2)");

			if (_evaluatedSlots.Count >= 2)
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
