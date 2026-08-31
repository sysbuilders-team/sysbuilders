using System.Collections.Generic;
using Core.Diagram;
using Core.Requirements;
using Core.Session;

namespace Core.Evaluation
{
    public interface IEvaluationService
    {
        EvaluationResult Evaluate(IReadOnlyClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements);
    }
}
