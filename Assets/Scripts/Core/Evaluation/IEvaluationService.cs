using System.Collections.Generic;
using Core.Diagram;

namespace Core.Evaluation
{
    public interface IEvaluationService
    {
        EvaluationResult Evaluate(IReadOnlyClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements);
    }
}