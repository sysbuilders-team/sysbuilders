using Mirror;
using UnityEngine;
using Core.Evaluation;
using Core.Session;
using System.Collections.Generic;

namespace Network
{
    public class MatchSyncBridge : NetworkBehaviour
    {
        [SerializeField] private GameplayManager _gameplayManager;

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
            if(!isClientOnly) return;
            if (string.IsNullOrEmpty(newTheme)) return;
            //TODO: _gameplayManager.Initialize(newTheme, _trailId) após GameplayManager expor esse método
            Debug.Log($"[MatchSyncBridge] Client receives theme: {newTheme} | trail: {_trailId}");
        }

        public void NotifyLocalPlayerReady(PlayerSlot slot)
        {
            CmdSetReady((int)slot);
        }
        
        [Command(requiresAuthority = false)]
        private void CmdSetReady(int slotIndex)
        {
            var slot = (PlayerSlot)slotIndex;
            
            //TODO: repassar para o TurnManager do servidor (caso exista sessao server-side), por enquanto conta aqui
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
            // TODO: _gameplayManager.Session.TurnManager.ForceAdvancePhase() após GameplayManager expor a Session.
            Debug.Log("[MatchSyncBridge] RpcAdvancePhase recebido — avançar TurnManager local.");
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

            // TODO: _gameplayManager.Session.GetRivalState(localSlot).AddEvaluationResult(rivalResult) após GameplayManager expor a Session e o slot local.
            Debug.Log($"[MatchSyncBridge] Rival — raw:{rawScore} penalty:{penaltyScore} total:{rivalResult.TotalScore}");
        }
    }

}