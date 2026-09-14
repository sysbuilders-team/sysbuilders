using Core.Diagram;

namespace Core.Session
{
    public class RefactorHandler
    {
        public bool TryRefactor(ClassNode classNode, PlayerState playerState)
        {
            if (classNode == null || playerState == null) return false;

            if (!classNode.IsLocked)
            {
                return true;
            }

            if (playerState.TryDeductPoints(playerState.RefactorCost))
            {
                classNode.Unlock();
                return true;
            }
            
            return false;
        }
    }
}