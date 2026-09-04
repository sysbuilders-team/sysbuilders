using System;
using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using Core.Session;
using Core.Requirements;


namespace Core.Evaluation
{
    public class EvaluationEngine : IEvaluationService
    {
        private const int ScorePerRequirement = 100;
        private const int PenaltyLargeClass = 30;
        private const int PenaltyLowCohesion = 40;
        private const int PenaltyDuplicateCode = 50;
        private const int PenaltyLowAffinityCoupling = 30;
        private const int LargeClassThreshold = 4;

        public EvaluationResult Evaluate(IReadOnlyClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements)
        {
            if (diagram == null || activeRequirements == null)
            {
                return new EvaluationResult(0, 0, new List<string>(), new List<SmellOccurrence>());
            }

            var fulfilledRequirementIds = CheckRequirements(diagram, activeRequirements);
            var detectedSmells = DetectCodeSmells(diagram);
            var rawScore = fulfilledRequirementIds.Count * ScorePerRequirement;
            var penaltyScore = CalculatePenalty(detectedSmells);

            return new EvaluationResult(rawScore, penaltyScore, fulfilledRequirementIds, detectedSmells);
        }
        
        private List<string> CheckRequirements(IReadOnlyClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements)
        {
            var fulfilledIds = new HashSet<string>();
            var components = diagram.GetConnectedComponents();

            foreach (var requirement in activeRequirements)
            {
                if (requirement.RequirementMethods == null || requirement.RequirementMethods.Count == 0)
                {
                    fulfilledIds.Add(requirement.Id);
                    continue;
                }

                bool isFulfilled = components.Any(component =>
                {
                    var methodNamesInComponent = new HashSet<string>(
                        component.SelectMany(c => c.Methods).Select(m => m.Name));
                    return requirement.RequirementMethods.All(required => methodNamesInComponent.Contains(required.Name));
                });

                if (isFulfilled)
                {
                    fulfilledIds.Add(requirement.Id);
                }
            }

            return fulfilledIds.ToList();
        }

        private List<SmellOccurrence> DetectCodeSmells(IReadOnlyClassDiagram diagram)
        {
            var smells = new List<SmellOccurrence>();

            smells.AddRange(DetectLargeClasses(diagram));
            smells.AddRange(DetectLowCohesion(diagram));
            smells.AddRange(DetectDuplicateCode(diagram));
            smells.AddRange(DetectLowAffinityCoupling(diagram));

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
                    var severity = distinctCategories - 1;
                    yield return new SmellOccurrence(CodeSmellType.LowCohesion, new List<string> { classNode.Name }, severity);
                }
            }
        }

        private IEnumerable<SmellOccurrence> DetectDuplicateCode(IReadOnlyClassDiagram diagram)
        {
            var classesByMethodIdentity = new Dictionary<(string Name, string Category), List<string>>();

            foreach (var classNode in diagram.Classes)
            {
                foreach (var method in classNode.Methods)
                {
                    var identity = (method.Name, method.Category ?? string.Empty);

                    if (!classesByMethodIdentity.TryGetValue(identity, out var classNames))
                    {
                        classNames = new List<string>();
                        classesByMethodIdentity[identity] = classNames;
                    }

                    classNames.Add(classNode.Name);
                }
            }

            foreach (var entry in classesByMethodIdentity.OrderBy(e => e.Key.Name, StringComparer.Ordinal)
                                                          .ThenBy(e => e.Key.Category, StringComparer.Ordinal))
            {
                if (entry.Value.Count > 1)
                {
                    yield return new SmellOccurrence(CodeSmellType.DuplicateCode, entry.Value);
                }
            }
        }

        private IEnumerable<SmellOccurrence> DetectLowAffinityCoupling(IReadOnlyClassDiagram diagram)
        {
            var seenPairs = new HashSet<(ClassNode, ClassNode)>();

            foreach (var relation in diagram.Relations)
            {
                var pairKey = NormalizePair(relation.Origin, relation.Destination);
                if (!seenPairs.Add(pairKey))
                {
                    continue; 
                }

                var sourceCategories = new HashSet<string>(
                    relation.Origin.Methods.Select(m => m.Category).Where(c => !string.IsNullOrEmpty(c)));
                var targetCategories = new HashSet<string>(
                    relation.Destination.Methods.Select(m => m.Category).Where(c => !string.IsNullOrEmpty(c)));

                bool sharesCategory = sourceCategories.Overlaps(targetCategories);

                if (!sharesCategory)
                {
                    yield return new SmellOccurrence(
                        CodeSmellType.LowAffinityCoupling,
                        new List<string> { relation.Origin.Name, relation.Destination.Name });
                }
            }
        }

        private static (ClassNode, ClassNode) NormalizePair(ClassNode a, ClassNode b)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a)
                   <= System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(b)
                ? (a, b)
                : (b, a);
        }

        private int CalculatePenalty(IReadOnlyList<SmellOccurrence> smells)
        {
            int penalty = 0;

            foreach (var smell in smells)
            {
                switch (smell.Type)
                {
                    case CodeSmellType.LargeClass:
                        penalty += PenaltyLargeClass * smell.Severity;
                        break;
                    case CodeSmellType.LowCohesion:
                        penalty += PenaltyLowCohesion * smell.Severity;
                        break;
                    case CodeSmellType.DuplicateCode:
                        penalty += PenaltyDuplicateCode * smell.Severity;
                        break;
                    case CodeSmellType.LowAffinityCoupling:
                        penalty += PenaltyLowAffinityCoupling * smell.Severity;
                        break;
                }
            }

            return penalty;
        }
    }
}
