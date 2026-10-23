using System.Collections.Generic;
using Core.Diagram;
using Core.Requirements;

namespace Core.Evaluation 
{
    public interface IEvaluationService 
    {
        EvaluationResult Evaluate(
            IReadOnlyClassDiagram diagram, 
            IReadOnlyList<Requirement> activeRequirements
        );
    }
}
