namespace Core.Session
{
    public interface IRefactorPointPolicy
    {
        int InitialPoints { get; }
        int GetPointsForTurn(int turnNumber);
    }

    public class DefaultRefactorPointPolicy : IRefactorPointPolicy
    {
        public const int DefaultInitialPoints = 1;
        public const int DefaultPointsPerTurn = 1;
        public const int DefaultMaxStoredPoints = 2;

        public int InitialPoints { get; }
        public int MaxStoredPoints { get; }
        private readonly int _pointsPerTurn;

        public DefaultRefactorPointPolicy(
            int initialPoints = DefaultInitialPoints,
            int pointsPerTurn = DefaultPointsPerTurn,
            int maxStoredPoints = DefaultMaxStoredPoints)
        {
            InitialPoints = initialPoints;
            _pointsPerTurn = pointsPerTurn;
            MaxStoredPoints = maxStoredPoints;
        }

        public int GetPointsForTurn(int turnNumber)
        {
            if (turnNumber <= 1) return 0;
            return _pointsPerTurn;
        }
    }
}
