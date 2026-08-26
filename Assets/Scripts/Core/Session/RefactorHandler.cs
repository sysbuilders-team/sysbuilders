using Core.Diagram;

namespace Core.Session
{
    public class RefactorHandler
    {
        public bool TryRefactor(ClassNode classNode, PlayerState playerState)
        {
            // TODO: tratar edge case se a classe ja estiver destravada
            if (playerState.TryDeductPoints(playerState.RefactorCost))
            {
                classNode.Unlock();
                return true;
            }
            
            return false;
        }
    }
}