using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Core.Evaluation;
using Core.Session;

namespace Network
{
    public class MatchSyncBridge : NetworkBehaviour
    {
        public event Action<string, string> OnMatchConfigReceived;
        public event Action OnPhaseAdvanceRequested;
        public event Action<int, EvaluationResult> OnRivalEvaluationReceived;

        [SyncVar(hook = nameof(OnThemeReceived))] private string _theme;
        [SyncVar] private string _trailId;

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
        }

        private void OnThemeReceived(string oldTheme, string newTheme)
        {
            if (!isClientOnly) return;
            if (string.IsNullOrEmpty(newTheme)) return;

            Debug.Log($"[MatchSyncBridge] Cliente recebeu tema: {newTheme} | trail: {_trailId}");
            OnMatchConfigReceived?.Invoke(newTheme, _trailId);
        }

        public void NotifyLocalPlayerReady(PlayerSlot slot)
        {
            CmdSetReady((int)slot);
        }
        
        [Command(requiresAuthority = false)]
        private void CmdSetReady(int slotIndex)
        {
            var slot = (PlayerSlot)slotIndex;
            
            _readyCount++;
            Debug.Log($"[MatchSyncBridge] Server: slot {slot} pronto ({_readyCount}/2)");
            
            if (_readyCount >= 2)
            {
                _readyCount = 0;
                RpcAdvancePhase();
            }   
        }
        
        private int _readyCount;
        
        [ClientRpc]
        private void RpcAdvancePhase()
        {
            Debug.Log("[MatchSyncBridge] RpcAdvancePhase recebido — avançar TurnManager local.");
            OnPhaseAdvanceRequested?.Invoke();
        }
        
        [Server]
        public void SendEvaluationResult(int targetSlot, int rawScore, int penaltyScore)
        {
            RpcReceiveEvaluationResult(targetSlot, rawScore, penaltyScore);
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

            Debug.Log($"[MatchSyncBridge] Rival — raw:{rawScore} penalty:{penaltyScore} total:{rivalResult.TotalScore}");
            OnRivalEvaluationReceived?.Invoke(targetSlot, rivalResult);
        }
    }
}
