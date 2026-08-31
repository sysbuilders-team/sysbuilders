using System;
using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using Core.Session;

namespace Core.Evaluation
{
    public class EvaluationEngine
    {
        private const int ScorePerRequirement = 100;
        private const int PenaltyLargeClass = 30;
        private const int PenaltyLowCohesion = 40;
        private const int PenaltyDuplicateCode = 50;
        private const int LargeClassThreshold = 4;

        public EvaluationResult Evaluate(IReadOnlyClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements)
        {
            if (diagram == null || activeRequirements == null)
            {
                return new EvaluationResult(0, new List<string>(), new List<SmellOccurrence>());
            }

            var fulfilledRequirementIds = CheckRequirements(diagram, activeRequirements);
            var detectedSmells = DetectCodeSmells(diagram);
            var totalScore = CalculateScore(fulfilledRequirementIds.Count, detectedSmells);

            return new EvaluationResult(totalScore, fulfilledRequirementIds, detectedSmells);
        }

        private List<string> CheckRequirements(IReadOnlyClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements)
        {
            var fulfilledIds = new List<string>();

            foreach (var requirement in activeRequirements)
            {
                if (requirement.RequiredMethods.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Requirement '{requirement.Id}' has no required methods configured.");
                }

                bool isFulfilled = diagram.Classes.Any(classNode =>
                {
                    var methodNamesInClass = new HashSet<string>(classNode.Methods.Select(m => m.Name));
                    return requirement.RequiredMethods.All(required => methodNamesInClass.Contains(required.Name));
                });

                if (isFulfilled)
                {
                    fulfilledIds.Add(requirement.Id);
                }
            }

            return fulfilledIds;
        }

        private List<SmellOccurrence> DetectCodeSmells(IReadOnlyClassDiagram diagram)
        {
            var smells = new List<SmellOccurrence>();

            smells.AddRange(DetectLargeClasses(diagram));
            smells.AddRange(DetectLowCohesion(diagram));
            smells.AddRange(DetectDuplicateCode(diagram));

            return smells;
        }

        private IEnumerable<SmellOccurrence> DetectLargeClasses(IReadOnlyClassDiagram diagram)
        {
            foreach (var classNode in diagram.Classes)
            {
                if (classNode.Methods.Count > LargeClassThreshold)
                {
                    yield return new SmellOccurrence(CodeSmellType.LargeClass, new List<string> { classNode.Name });
                }
            }
        }

        private IEnumerable<SmellOccurrence> DetectLowCohesion(IReadOnlyClassDiagram diagram)
        {
            foreach (var classNode in diagram.Classes)
            {
                var distinctCategories = classNode.Methods
                    .Select(m => m.Category)
                    .Where(category => !string.IsNullOrEmpty(category))
                    .Distinct()
                    .Count();

                if (distinctCategories >= 2)
                {
                    yield return new SmellOccurrence(CodeSmellType.LowCohesion, new List<string> { classNode.Name });
                }
            }
        }

        private IEnumerable<SmellOccurrence> DetectDuplicateCode(IReadOnlyClassDiagram diagram)
        {
            var classesByMethodName = new Dictionary<string, List<string>>();

            foreach (var classNode in diagram.Classes)
            {
                foreach (var method in classNode.Methods)
                {
                    if (!classesByMethodName.TryGetValue(method.Name, out var classNames))
                    {
                        classNames = new List<string>();
                        classesByMethodName[method.Name] = classNames;
                    }

                    if (!classNames.Contains(classNode.Name))
                    {
                        classNames.Add(classNode.Name);
                    }
                }
            }

            foreach (var entry in classesByMethodName.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                if (entry.Value.Count > 1)
                {
                    yield return new SmellOccurrence(CodeSmellType.DuplicateCode, entry.Value);
                }
            }
        }

        private int CalculateScore(int fulfilledRequirementCount, IReadOnlyList<SmellOccurrence> smells)
        {
            int score = fulfilledRequirementCount * ScorePerRequirement;

            foreach (var smell in smells)
            {
                switch (smell.Type)
                {
                    case CodeSmellType.LargeClass:
                        score -= PenaltyLargeClass;
                        break;
                    case CodeSmellType.LowCohesion:
                        score -= PenaltyLowCohesion;
                        break;
                    case CodeSmellType.DuplicateCode:
                        score -= PenaltyDuplicateCode;
                        break;
                }
            }

            return Math.Max(0, score);
        }
    }
}