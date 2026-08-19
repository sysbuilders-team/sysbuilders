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

        public enum AdvanceTurnResult
        {
            Success,
            DiagramIsNull,
            ClassesStillUnlocked,
            MatchAlreadyEnded
        }

        private const int MaxTurns = 5;

        public int CurrentTurn { get; private set; }
        public TurnState State { get; private set; }

        public TurnManager()
        {
            CurrentTurn = 1;
            State = TurnState.Active;
        }

        public AdvanceTurnResult AdvanceTurn(ClassDiagram playerDiagram)
        {
            if (State == TurnState.Ended)
            {
                return AdvanceTurnResult.MatchAlreadyEnded;
            }

            if (playerDiagram == null)
            {
                return AdvanceTurnResult.DiagramIsNull;
            }

            bool unlockedClasses = playerDiagram.Classes.Any(node => !node.IsLocked);
            if (unlockedClasses)
            {
                return AdvanceTurnResult.ClassesStillUnlocked;
            }

            if (CurrentTurn >= MaxTurns)
            {
                State = TurnState.Ended;
            }
            else
            {
                CurrentTurn++;
            }

            return AdvanceTurnResult.Success;
        }
    }
}