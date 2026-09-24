using System.Collections.Generic;
using Core.Evaluation;

namespace Core.Session
{
    public interface IMatchEndContext
    {
        IReadOnlyList<EvaluationResult> PlayerOneHistory { get; }
        IReadOnlyList<EvaluationResult> PlayerTwoHistory { get; }
        string WinnerName { get; }
        bool IsDraw { get; }
    }
}