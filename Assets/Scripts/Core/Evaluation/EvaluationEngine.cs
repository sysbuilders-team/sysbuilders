using System.Collections.Generic;
using Core.Diagram;
using Core.Requirements;

namespace Core.Evaluation
{
    public class EvaluationEngine : IEvaluationService
    {
        private const int ScorePerRequirement = 100;

        private readonly RequirementChecker _requirementChecker = new RequirementChecker();
        private readonly CodeSmellDetector _smellDetector = new CodeSmellDetector();
        private readonly SmellPenaltyCalculator _penaltyCalculator = new SmellPenaltyCalculator();

        public EvaluationResult Evaluate(IReadOnlyClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements)
        {
            if (diagram == null || activeRequirements == null)
            {
                return new EvaluationResult(0, 0, new List<string>(), new List<SmellOccurrence>());
            }

            var fulfilledRequirementIds = _requirementChecker.CheckRequirements(diagram, activeRequirements);
            var detectedSmells = _smellDetector.DetectCodeSmells(diagram);
            var rawScore = fulfilledRequirementIds.Count * ScorePerRequirement;
            var penaltyScore = _penaltyCalculator.CalculatePenalty(detectedSmells);

            return new EvaluationResult(rawScore, penaltyScore, fulfilledRequirementIds, detectedSmells);
        }
    }
}