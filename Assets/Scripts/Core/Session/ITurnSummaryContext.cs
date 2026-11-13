namespace Core.Session
{
    public interface ITurnSummaryContext
    {
        int RoundPointsEarned { get; }
        int RoundPenaltyPoints { get; }
        int RoundOpponentPointsEarned { get; }
        int RoundOpponentPenaltyPoints { get; }

        // Domain bonus and pending associations
        int RoundDomainPoints { get; }
        int PendingAssociationsCount { get; }
        int AssociationsTotalCount { get; }
    }
}
