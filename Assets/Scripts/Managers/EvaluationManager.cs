using System.Collections.Generic;
using Core.Evaluation;
using Core.Session;
using Core.Diagram;

namespace Managers.Evaluation
{ 
    public class EvaluationManager
    {
        private readonly IEvaluationService _evaluationService;

        public EvaluationManager(IEvaluationService evaluationService = null)
        {
            _evaluationService = evaluationService ?? new EvaluationEngine();
        }

        public EvaluationResult RunEvaluation(IReadOnlyClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements)
        {
            return _evaluationService.Evaluate(diagram, activeRequirements);
        }
    }
}
