using System.Collections.Generic;
using Core.Diagram;
using Core.Session;

namespace Core.Evaluation 
{
    public interface IEvaluationService 
    {
        EvaluationResult Evaluate(
            Core.Session.IReadOnlyClassDiagram diagram, 
            System.Collections.Generic.IReadOnlyList<Core.Requirements.Requirement> activeRequirements
        );
    }
}
