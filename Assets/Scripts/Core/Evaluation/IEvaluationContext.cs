using System;
using System.Collections.Generic;

namespace Core.Evaluation
{
    public interface IEvaluationContext
    {
        EvaluationResult LastResult { get; }
        IReadOnlyList<EvaluationResult> History { get; }
        
        event Action<EvaluationResult> OnEvaluationUpdated;
    }
}