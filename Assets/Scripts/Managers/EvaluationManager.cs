using System.Collections.Generic;
using Core.Diagram;
using Core.Evaluation;
using Core.Requirements;

namespace Managers.Evaluation
{
    public class EvaluationManager
    {
        private readonly EvaluationEngine _evaluationEngine;

        public EvaluationManager(EvaluationEngine evaluationEngine = null)
        {
            _evaluationEngine = evaluationEngine ?? new EvaluationEngine();
        }

        public EvaluationResult RunEvaluation(ClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements)
        {
            return _evaluationEngine.Evaluate(diagram, activeRequirements);
        }
    }
}