namespace Core.Session
{
    public interface IRefactorPointPolicy
    {
        int InitialPoints { get; }
        int GetPointsForTurn(int turnNumber);
    }

    public class DefaultRefactorPointPolicy : IRefactorPointPolicy
    {
        public int InitialPoints { get; }
        private readonly int _pointsPerTurn;

        public DefaultRefactorPointPolicy(int initialPoints = 2, int pointsPerTurn = 0)
        {
            InitialPoints = initialPoints;
            _pointsPerTurn = pointsPerTurn;
        }

        public int GetPointsForTurn(int turnNumber)
        {
            return _pointsPerTurn;
        }
    }
}
