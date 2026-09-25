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
        [SerializeField] private GameManager _gameManager;

        [SyncVar(hook = nameof(OnThemeReceived))] private string _theme;
        [SyncVar] private string _trailId;

        private readonly HashSet<PlayerSlot> _readySlots = new HashSet<PlayerSlot>();

        public override void OnStartServer()
        {
            base.OnStartServer();

            var roomManager = NetworkManager.singleton as GameRoomManager;
            if (roomManager == null)
            {
                Debug.LogError("[MatchSyncBridge] NetworkManager não é GameRoomManager.");
                return;
            }

            _theme   = roomManager.ChosenTheme;
            _trailId = roomManager.ChosenTrailId;

            Debug.Log($"[MatchSyncBridge] Server: tema={_theme} trail={_trailId}");
            
            _gameManager.Initialize(_theme, _trailId);
        }

        private void OnThemeReceived(string oldTheme, string newTheme)
        {
            if (!isClientOnly) return; 
            if (string.IsNullOrEmpty(newTheme)) return;

            Debug.Log($"[MatchSyncBridge] Cliente recebeu tema: {newTheme} | trail: {_trailId}");
            _gameManager.Initialize(newTheme, _trailId);
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
            _gameManager.Session.TurnManager.ForceAdvancePhase();
        }
        
        private void OnEnable()
        {
            if (_gameManager != null && _gameManager.Session != null)
            {
                _gameManager.Session.OnEvaluationCompleted += HandleLocalEvaluationCompleted;
            }
        }

        private void OnDisable()
        {
            if (_gameManager != null && _gameManager.Session != null)
            {
                _gameManager.Session.OnEvaluationCompleted -= HandleLocalEvaluationCompleted;
            }
        }

        private void HandleLocalEvaluationCompleted(EvaluationResult resultOne, EvaluationResult resultTwo)
        {
            if (isServer)
            {
                RpcReceiveEvaluationResult((int)PlayerSlot.PlayerOne, resultOne.RawScore, resultOne.PenaltyScore);
                RpcReceiveEvaluationResult((int)PlayerSlot.PlayerTwo, resultTwo.RawScore, resultTwo.PenaltyScore);
            }
        }

        [ClientRpc]
        private void RpcReceiveEvaluationResult(int targetSlot, int rawScore, int penaltyScore)
        {
            var rivalResult = new EvaluationResult(
                rawScore,
                penaltyScore,
                new List<string>(),
                new List<SmellOccurrence>()
            );

            Debug.Log($"[MatchSyncBridge] Slot {targetSlot} — raw:{rawScore} penalty:{penaltyScore} total:{rivalResult.TotalScore}");
            _gameManager.Session.GetPlayerBySlot((PlayerSlot)targetSlot).AddEvaluationResult(rivalResult);
        }
    }
}