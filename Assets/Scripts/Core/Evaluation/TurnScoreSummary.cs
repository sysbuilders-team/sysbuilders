using System;

namespace Core.Evaluation
{
    public interface ITurnSummaryContext
    {
        event Action<TurnScoreSummary, TurnScoreSummary> OnTurnEvaluationCompleted;
    }

    public readonly struct TurnScoreSummary
    {
        public string PlayerName { get; }
        public int TurnGainedPoints { get; }
        public int TurnPenaltyPoints { get; }
        public int TurnNetScore => TurnGainedPoints - TurnPenaltyPoints;
        public int TotalScore { get; }

        public TurnScoreSummary(string playerName, int gained, int penalty, int total)
        {
            PlayerName = playerName;
            TurnGainedPoints = gained;
            TurnPenaltyPoints = penalty;
            TotalScore = total;
        }
    }
}