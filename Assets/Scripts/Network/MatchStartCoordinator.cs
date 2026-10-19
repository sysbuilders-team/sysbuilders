using System;

namespace Network
{
    /// <summary>Outcome of resolving the server-side match start.</summary>
    public readonly struct MatchStartPlan
    {
        public string Theme { get; }
        public string TrailId { get; }
        public string FailureReason { get; }

        public bool Succeeded => FailureReason == null;

        private MatchStartPlan(string theme, string trailId, string failureReason)
        {
            Theme = theme;
            TrailId = trailId;
            FailureReason = failureReason;
        }

        public static MatchStartPlan Started(string theme, string trailId) =>
            new MatchStartPlan(theme, trailId, null);

        public static MatchStartPlan Failed(string reason) =>
            new MatchStartPlan(null, null, reason);
    }

    /// <summary>
    /// Decides the server-side match start: which theme, which trail, or why it cannot start.
    ///
    /// Pure C# on purpose. MatchSyncBridge keeps the Mirror and Unity parts (network
    /// notification, abort, local session start); this class only decides, so the failure
    /// paths can be tested without a NetworkBehaviour.
    /// </summary>
    public static class MatchStartCoordinator
    {
        /// <summary>
        /// Resolves the plan. <paramref name="drawTrailId"/> is the RequirementManager lookup;
        /// null means the manager was not found in the scene.
        /// </summary>
        public static MatchStartPlan Resolve(string chosenTheme, Func<string, string> drawTrailId)
        {
            if (string.IsNullOrWhiteSpace(chosenTheme))
            {
                return MatchStartPlan.Failed("Tema da partida não definido.");
            }

            if (drawTrailId == null)
            {
                return MatchStartPlan.Failed("RequirementManager não encontrado para sortear a trilha.");
            }

            string trailId;
            try
            {
                trailId = drawTrailId(chosenTheme);
            }
            catch (Exception error)
            {
                return MatchStartPlan.Failed(error.Message);
            }

            if (string.IsNullOrEmpty(trailId))
            {
                return MatchStartPlan.Failed($"Nenhuma trilha encontrada para o tema '{chosenTheme}'.");
            }

            return MatchStartPlan.Started(chosenTheme, trailId);
        }
    }
}
