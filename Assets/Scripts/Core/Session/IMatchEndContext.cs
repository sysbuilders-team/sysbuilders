using System.Collections.Generic;
using Core.Evaluation;

namespace Core.Session
{
    public interface IMatchEndContext
    {
        IReadOnlyList<EvaluationResult> PlayerOneHistory { get; }
        IReadOnlyList<EvaluationResult> PlayerTwoHistory { get; }

        // Placar oficial da partida (mesma regra do IScoreContext). O modal final nao deve
        // recalcular somando TotalScore: o RawScore de cada turno ja e acumulado.
        int PlayerOneScore { get; }
        int PlayerTwoScore { get; }

        string WinnerName { get; }
        bool IsDraw { get; }
    }
}