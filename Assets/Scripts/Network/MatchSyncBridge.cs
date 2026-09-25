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

        public override void OnStartServer()
        {
            base.OnStartServer();

            var roomManager = NetworkManager.singleton as GameRoomManager;
            _theme = roomManager != null && !string.IsNullOrEmpty(roomManager.ChosenTheme)
                ? roomManager.ChosenTheme
                : PlayerPrefs.GetString("SelectedTheme", "Biblioteca");

            if (requirementManager == null)
            {
                requirementManager = GetComponent<RequirementManager>() ?? FindFirstObjectByType<RequirementManager>();
            }

            if (requirementManager == null)
            {
                Debug.LogError("[MatchSyncBridge] RequirementManager não encontrado para sortear a trilha.");
                return;
            }

            _trailId = requirementManager.GetRandomTrailId(_theme);

            Debug.Log($"[MatchSyncBridge] Server: tema={_theme} | trail={_trailId}");
            
            gameManager.StartMatch(_theme, _trailId, PlayerSlot.PlayerOne);
        }

        private void OnTrailSynced(string oldTrailId, string newTrailId)
        {
            if (!isClientOnly) return; 
            if (string.IsNullOrEmpty(newTrailId)) return;

            Debug.Log($"[MatchSyncBridge] Cliente sincronizou trilha: {newTrailId} (Tema: {_theme})");
            gameManager.StartMatch(_theme, newTrailId, PlayerSlot.PlayerTwo);
        }

        public void NotifyLocalPlayerReady(PlayerSlot slot) => CmdSetReady((int)slot);

        [Command(requiresAuthority = false)]
        private void CmdSetReady(int slotIndex)
        {
            var slot = (PlayerSlot)slotIndex;
            
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
            Debug.Log("[MatchSyncBridge] RpcAdvancePhase recebido — avançar TurnManager local.");
            gameManager.Session.TurnManager.ForceAdvancePhase();
        }
        
        private void OnEnable()
        {
            if (gameManager != null && gameManager.Session != null)
            {
                gameManager.Session.OnEvaluationCompleted += HandleLocalEvaluationCompleted;
            }
        }

        private void OnDisable()
        {
            if (gameManager != null && gameManager.Session != null)
            {
                gameManager.Session.OnEvaluationCompleted -= HandleLocalEvaluationCompleted;
            }
        }

        private void HandleLocalEvaluationCompleted(EvaluationResult localResult)
        {
            if (gameManager == null || gameManager.Session == null) return;
            CmdSendEvaluationResult((int)gameManager.Session.LocalSlot, localResult.RawScore, localResult.PenaltyScore);
        }

        [Command(requiresAuthority = false)]
        private void CmdSendEvaluationResult(int slotIndex, int rawScore, int penaltyScore)
        {
            RpcReceiveEvaluationResult(slotIndex, rawScore, penaltyScore);
        }

        [ClientRpc]
        private void RpcReceiveEvaluationResult(int targetSlot, int rawScore, int penaltyScore)
        {
            if (gameManager == null || gameManager.Session == null) return;
            if ((int)gameManager.Session.LocalSlot == targetSlot) return;

            var rivalResult = new EvaluationResult(
                rawScore,
                penaltyScore,
                new List<string>(),
                new List<SmellOccurrence>()
            );

            Debug.Log($"[MatchSyncBridge] Slot rival {targetSlot} — raw:{rawScore} penalty:{penaltyScore} total:{rivalResult.TotalScore}");
            gameManager.Session.GetPlayerBySlot((PlayerSlot)targetSlot).AddEvaluationResult(rivalResult);
        }
    }
}