using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using Core.Requirements;

namespace Core.Evaluation
{
    public class EvaluationEngine : IEvaluationService
    {
        public const int ScorePerRequirement = 100;
        public const int CleanArchitectureBonus = 50;
        public const int PenaltyPerUnallocatedMethod = 25;

        private readonly RequirementChecker _requirementChecker;
        private readonly CodeSmellDetector _smellDetector;
        private readonly SmellPenaltyCalculator _penaltyCalculator;

        public EvaluationEngine(
            RequirementChecker requirementChecker = null,
            CodeSmellDetector smellDetector = null,
            SmellPenaltyCalculator penaltyCalculator = null)
        {
            _requirementChecker = requirementChecker ?? new RequirementChecker();
            _smellDetector = smellDetector ?? new CodeSmellDetector();
            _penaltyCalculator = penaltyCalculator ?? new SmellPenaltyCalculator();
        }

        public EvaluationResult Evaluate(IReadOnlyClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements)
        {
            if (diagram == null || activeRequirements == null)
            {
                return new EvaluationResult(0, 0, new List<string>(), new List<SmellOccurrence>());
            }

            var fulfilledRequirementIds = _requirementChecker.CheckRequirements(diagram, activeRequirements);
            var detectedSmells = _smellDetector.DetectCodeSmells(diagram, activeRequirements);

            // Bônus Clean Architecture: entregou ao menos 1 requisito e zero smells
            int cleanBonus = (fulfilledRequirementIds.Count > 0 && detectedSmells.Count == 0)
                ? CleanArchitectureBonus
                : 0;

            int rawScore = (fulfilledRequirementIds.Count * ScorePerRequirement) + cleanBonus;

            // Anti-Inércia: métodos exigidos pelos requisitos ativos que não foram alocados em nenhuma classe
            var requiredMethods = new HashSet<(string Name, string Category)>();
            foreach (var req in activeRequirements)
            {
                if (req?.RequirementMethods == null) continue;
                foreach (var method in req.RequirementMethods)
                {
                    if (method != null)
                    {
                        requiredMethods.Add((method.Name, method.Category ?? string.Empty));
                    }
                }
            }

            var allocatedMethods = new HashSet<(string Name, string Category)>();
            foreach (var classNode in diagram.Classes)
            {
                if (classNode?.Methods == null) continue;
                foreach (var method in classNode.Methods)
                {
                    if (method != null)
                    {
                        allocatedMethods.Add((method.Name, method.Category ?? string.Empty));
                    }
                }
            }

            int unallocatedCount = 0;
            foreach (var required in requiredMethods)
            {
                if (!allocatedMethods.Contains(required))
                {
                    unallocatedCount++;
                }
            }

            int unallocatedPenalty = unallocatedCount * PenaltyPerUnallocatedMethod;
            int smellPenalty = _penaltyCalculator.CalculatePenalty(detectedSmells);
            int totalPenalty = smellPenalty + unallocatedPenalty;

            return new EvaluationResult(
                rawScore,
                totalPenalty,
                fulfilledRequirementIds,
                detectedSmells,
                cleanBonus,
                unallocatedPenalty);
        }
    }
}
