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
	/// Client/server sync bridge for a running match.
	///
	/// Trust model: the server validates shape (authorization, bounds, sequence),
	/// not values. Evaluation scores are computed client-side and relayed; a
	/// malicious client can still send any in-bound score and win. Closing that
	/// gap requires the server to recompute from the received diagram, tracked
	/// as future work.
	/// </summary>
	public class MatchSyncBridge : NetworkBehaviour
	{
		private const int RequiredPlayers = 2;

		[SerializeField] private GameManager gameManager;
		[SerializeField] private RequirementManager requirementManager;

		[SyncVar] private string _theme;
		[SyncVar(hook = nameof(OnTrailSynced))] private string _trailId;

		private readonly ReadyQuorum _quorum = new ReadyQuorum(RequiredPlayers);	
        private MatchSession _subscribedSession;
		private readonly MatchAbortCoordinator _abort = new MatchAbortCoordinator();
		private NetworkConnectionToClient _guest;

		public override void OnStartServer()
		{
			base.OnStartServer();
			_quorum.ClearAll();
			_guest = null;

			NetworkServer.OnDisconnectedEvent += HandleServerDisconnect;

			var roomManager = NetworkManager.singleton as GameRoomManager;

			if (requirementManager == null)
			{
				if (!TryGetComponent(out requirementManager))
				{
					requirementManager = FindFirstObjectByType<RequirementManager>();
				}
			}

			MatchStartPlan plan = MatchStartCoordinator.Resolve(
				roomManager?.ChosenTheme,
				requirementManager != null ? requirementManager.GetRandomTrailId : null);

			if (!plan.Succeeded)
			{
				Debug.LogError($"[MatchSyncBridge] Erro ao iniciar partida no servidor: {plan.FailureReason}");

				NotifyMatchStartFailed(plan.FailureReason);
				AbortLocalMatch(plan.FailureReason);
				return;
			}

			// Start the host session FIRST. If it throws, abort both sides with a diagnostic.
			// Only publish the SyncVars after the host has a working session — otherwise the
			// client advances while the host sits in a half-initialized state and every
			// CmdSetReady is rejected with "sessão não inicializada".
			try
			{
				StartLocalMatch(plan.Theme, plan.TrailId, PlayerSlot.PlayerOne);
			}
			catch (Exception ex)
			{
				Debug.LogException(ex, this);
				string reason = MatchAbortCoordinator.SanitizeReason(ex.Message);
				NotifyMatchStartFailed(reason);
				AbortLocalMatch(reason);
				return;
			}

			_theme = plan.Theme;
			_trailId = plan.TrailId;
			Debug.Log($"[MatchSyncBridge] Server: tema={_theme} | trail={_trailId}");
		}

		public override void OnStopServer()
		{
			NetworkServer.OnDisconnectedEvent -= HandleServerDisconnect;
			_quorum.ClearAll();
			_guest = null;
			base.OnStopServer();
		}

		/// <summary>Tells every client why the match could not start. Best effort: never throws.</summary>
		private void NotifyMatchStartFailed(string reason)
		{
			if (!NetworkServer.active) return;

			try
			{
				RpcNotifyMatchStartFailed(reason);
			}
			catch (Exception notifyEx)
			{
				Debug.LogError($"[MatchSyncBridge] Nao foi possivel notificar os clientes da falha: {notifyEx.Message}");
			}
		}

		[ClientRpc]
		private void RpcNotifyMatchStartFailed(string reason)
		{
			AbortLocalMatch(reason);
		}

		[Command(requiresAuthority = false)]
		private void CmdNotifyMatchStartFailed(string reason, NetworkConnectionToClient sender = null)
		{
			if (!IsKnownMatchParticipant(sender))
			{
				Debug.LogWarning("[MatchSyncBridge] CmdNotifyMatchStartFailed rejected: connection not authorized.");
				return;
			}

			string safeReason = MatchAbortCoordinator.SanitizeReason(reason);
			Debug.LogError($"[MatchSyncBridge] Cliente reportou falha ao iniciar a partida: {safeReason}");
			RpcNotifyMatchStartFailed(safeReason);
		}

		private void AbortLocalMatch(string reason)
		{
			if (!_abort.TryBeginAbort()) return;

			Debug.LogError($"[MatchSyncBridge] Partida abortada: {reason}");

			if (gameManager != null)
			{
				gameManager.ReturnToMainMenu();
			}
		}

		private void HandleServerDisconnect(NetworkConnectionToClient conn)
		{
			if (_guest == conn) _guest = null;
			
			if (_abort.HasAborted) return;
			if (gameManager == null || gameManager.Session == null) return;

			_quorum.ClearAll();

			AbortLocalMatch("O adversário desconectou.");
		}

		private void OnTrailSynced(string oldTrailId, string newTrailId)
		{
			if (!isClientOnly) return;
			if (string.IsNullOrEmpty(newTrailId)) return;

			Debug.Log($"[MatchSyncBridge] Cliente sincronizou trilha: {newTrailId} (Tema: {_theme})");

			try
			{
				StartLocalMatch(_theme, newTrailId, PlayerSlot.PlayerTwo);
			}
			catch (Exception ex)
			{
				RecoverFromMatchStartFailure(ex.Message);
			}
		}

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
			if (gameManager == null)
			{
				throw new InvalidOperationException("MatchSyncBridge.gameManager is not assigned in the Inspector.");
			}

			// Reset before StartMatch so a failure path can still call AbortLocalMatch:
			// TryBeginAbort returns false if a previous match left the coordinator armed.
			_abort.Reset();

			gameManager.StartMatch(theme, trailId, slot, isNetworkMatch: true);

			Debug.Log($"[MatchSyncBridge] StartLocalMatch slot={slot}: session={(gameManager.Session != null)}");

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
			if (slot == PlayerSlot.PlayerTwo) return EnsureGuestCaptured(sender);
			return false;
		}

		// Returns true only when sender is the connection bound as the guest. The first
		// non-host connection to reach this method claims the slot; every other
		// connection is rejected. Cleared by OnStopServer and HandleServerDisconnect so
		// the next match rebinds.
		private bool EnsureGuestCaptured(NetworkConnectionToClient sender)
		{
			if (sender == null || sender == NetworkServer.localConnection) return false;
			if (_guest == null) _guest = sender;
			return sender == _guest;
		}

		private bool IsKnownMatchParticipant(NetworkConnectionToClient sender)
		{
			if (sender == null || sender == NetworkServer.localConnection) return true;
			return EnsureGuestCaptured(sender);
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

			if (phase != (int)tm.CurrentPhase)
			{
				Debug.LogWarning($"[MatchSyncBridge] CmdSetReady descartado: fase {phase} não é a atual ({(int)tm.CurrentPhase}).");
				return;
			}

			_quorum.MarkReady(slot);
			Debug.Log($"[MatchSyncBridge] Server: slot {slot} pronto ({_quorum.ReadyCount}/{RequiredPlayers})");

			if (_quorum.IsReadyQuorumMet)
			{
				// Clear only the quorum this path owns. Evaluations are cleared by
				// CmdSendEvaluationResult; wiping them here would erase a late result
				// instead of letting the evaluation path decide.
				_quorum.ClearReady();
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

			var payload = new MatchResultPayload
			{
				TurnNumber = tm.CurrentTurn,
				Phase = (int)tm.CurrentPhase,
				SlotIndex = (int)gameManager.Session.LocalSlot,
				RawScore = localResult.RawScore,
				PenaltyScore = localResult.PenaltyScore,
				SmellTypes = smellTypes,
				SmellSeverities = smellSeverities,
				SmellClasses = smellClasses
			};

			CmdSendEvaluationResult(payload);
		}

		[Command(requiresAuthority = false)]
		private void CmdSendEvaluationResult(MatchResultPayload payload, NetworkConnectionToClient sender = null)
		{
			var slot = (PlayerSlot)payload.SlotIndex;
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

			if (!EvaluationPayloadValidator.TryValidate(payload, out string reason))
			{
				Debug.LogWarning($"[MatchSyncBridge] CmdSendEvaluationResult rejected: {reason}");
				return;
			}

			var tm = gameManager.Session.TurnManager;

			if (payload.TurnNumber != tm.CurrentTurn)
			{
				Debug.LogWarning($"[MatchSyncBridge] CmdSendEvaluationResult descartado: turno {payload.TurnNumber} não é o atual ({tm.CurrentTurn}).");
				return;
			}

			if (payload.Phase != (int)tm.CurrentPhase)
			{
				Debug.LogWarning($"[MatchSyncBridge] CmdSendEvaluationResult descartado: fase {payload.Phase} não é a atual ({(int)tm.CurrentPhase}).");
				return;
			}

			RpcReceiveEvaluationResult(payload);

			_quorum.MarkEvaluated(slot);
			Debug.Log($"[MatchSyncBridge] Server: avaliação do slot {slot} do turno {payload.TurnNumber} recebida ({_quorum.EvaluatedCount}/{RequiredPlayers})");

			if (_quorum.IsEvaluationQuorumMet)
			{
				_quorum.ClearEvaluated();
				RpcAdvancePhase();
			}
		}

		[ClientRpc]
		private void RpcReceiveEvaluationResult(MatchResultPayload payload)
		{
			if (gameManager == null || gameManager.Session == null) return;
			if ((int)gameManager.Session.LocalSlot == payload.SlotIndex) return;

			var rivalResult = new EvaluationResult(
				payload.RawScore,
				payload.PenaltyScore,
				new List<string>(),
				SmellWireCodec.Decode(payload.SmellTypes, payload.SmellSeverities, payload.SmellClasses)
			);

			Debug.Log($"[MatchSyncBridge] Slot rival {payload.SlotIndex} (turno {payload.TurnNumber}) | raw:{payload.RawScore} penalty:{payload.PenaltyScore} total:{rivalResult.TotalScore}");
			gameManager.ReceiveOpponentEvaluation(payload.TurnNumber, rivalResult);
		}
	}
}
