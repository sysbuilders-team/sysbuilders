using Mirror;

namespace Network
{
    public class MatchSyncBridge : NetworkBehaviour
    {
        [SyncVar] private int _currentTurn;
        [SyncVar] private byte _currentPhase;

        // TODO: assinar MatchSession.OnEvaluationCompleted no servidor

        [Command]
        private void CmdConfirmReady()
        {
            // TODO: repassar pro TurnManager.SetPlayerReady(slot) local
        }

        [ClientRpc]
        private void RpcReceiveEvaluationResult()
        {
            // TODO: reconstruir e chamar PlayerState.AddEvaluationResult(...)
        }
    }
}