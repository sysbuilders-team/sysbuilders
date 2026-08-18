using System.Linq;
using Core.Diagram;

namespace Core.Turns
{
    public class TurnManager
    {
        public enum TurnState
        {
            Active,
            Ended
        }
        public int CurrentTurn { get; private set; }

        public TurnState State { get; private set; }

        public TurnManager()
        {
            CurrentTurn = 1;
            State = TurnState.Active;
        }
        public bool AdvanceTurn(ClassDiagram playerDiagram)
        {
            if (playerDiagram == null)
            {
                return false;
            }

            bool unlockedClasses = playerDiagram.Classes.Any(node => !node.IsLocked);
            if (unlockedClasses)
            {
                return false;
            }

            CurrentTurn++;
            State = TurnState.Active;
            return true;
        }

    }
}