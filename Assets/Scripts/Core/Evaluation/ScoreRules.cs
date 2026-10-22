using System.Collections.Generic;

namespace Core.Evaluation
{
    /// <summary>
    /// Single source of truth for the match score. This is the value that must be
    /// identical on client and server: the ADR requires it before tier-2
    /// (server-side evaluation) can ship, because any divergence between
    /// MatchScoreTracker.CalculateScore and EvaluationResult.TotalScore becomes a
    /// silent client/server desync once the server recomputes.
    ///
    /// Rule: RawScore of the latest EvaluationResult is cumulative across turns
    /// (each evaluation re-verifies all revealed requirements), and each turn
    /// contributes its own penalty. Match score = last raw - sum of all penalties,
    /// floored at 0. The floor keeps two heavily penalized players from producing
    /// a negative-score draw.
    /// </summary>
    public static class ScoreRules
    {
        public static int Score(IReadOnlyList<EvaluationResult> history)
        {
            if (history == null || history.Count == 0) return 0;

            int raw = history[history.Count - 1].RawScore;

            int penalties = 0;
            for (int i = 0; i < history.Count; i++)
            {
                penalties += history[i].PenaltyScore;
            }

            int net = raw - penalties;
            return net < 0 ? 0 : net;
        }
    }
}
