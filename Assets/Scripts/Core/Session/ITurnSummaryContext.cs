namespace Core.Session
{
    public interface ITurnSummaryContext
    {
        int RoundPointsEarned { get; }
        int RoundPenaltyPoints { get; }
        int RoundOpponentPointsEarned { get; }
        int RoundOpponentPenaltyPoints { get; }
    }
}