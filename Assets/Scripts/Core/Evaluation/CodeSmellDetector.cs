using System;
using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using Core.Requirements;

namespace Core.Evaluation
{   
    public class CodeSmellDetector
    {
        public const int LargeClassThreshold = 5;

        public List<SmellOccurrence> DetectCodeSmells(IReadOnlyClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements = null)
        {
            if (diagram == null)
            {
                return new List<SmellOccurrence>();
            }

            var smells = new List<SmellOccurrence>();

            smells.AddRange(DetectLargeClasses(diagram));
            smells.AddRange(DetectLowCohesion(diagram));
            smells.AddRange(DetectDuplicateCode(diagram));
            smells.AddRange(DetectLowAffinityCoupling(diagram, activeRequirements));

            return smells;
        }

        private IEnumerable<SmellOccurrence> DetectLargeClasses(IReadOnlyClassDiagram diagram)
        {
            foreach (var classNode in diagram.Classes)
            {
                if (classNode.Methods.Count > LargeClassThreshold)
                {
                    var severity = classNode.Methods.Count - LargeClassThreshold;
                    yield return new SmellOccurrence(CodeSmellType.LargeClass, new List<string> { classNode.Name }, severity);
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
            var classesByMethodIdentity = new Dictionary<(string Name, string Category), HashSet<string>>();

            foreach (var classNode in diagram.Classes)
            {
                foreach (var method in classNode.Methods)
                {
                    var identity = (method.Name, method.Category ?? string.Empty);

                    if (!classesByMethodIdentity.TryGetValue(identity, out var classNames))
                    {
                        classNames = new HashSet<string>();
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
                    // Severidade escala com o numero de classes que compartilham a mesma logica,
                    // igual a LargeClass e LowCohesion (2 classes => 1, 3 classes => 2).
                    yield return new SmellOccurrence(CodeSmellType.DuplicateCode, entry.Value.ToList(), entry.Value.Count - 1);
                }
            }
        }

        private IEnumerable<SmellOccurrence> DetectLowAffinityCoupling(
            IReadOnlyClassDiagram diagram,
            IReadOnlyList<Requirement> activeRequirements)
        {
            var seenPairs = new HashSet<(ClassNode, ClassNode)>();

            // Category pairs with recognized domain affinity in active requirements
            var cooccurringCategories = new HashSet<(string, string)>();
            if (activeRequirements != null)
            {
                foreach (var req in activeRequirements)
                {
                    // If the requirement explicitly specifies association rules, only those rules define affinity between different categories.
                    if (req?.RequiredAssociations != null && req.RequiredAssociations.Count > 0)
                    {
                        foreach (var rule in req.RequiredAssociations)
                        {
                            cooccurringCategories.Add((rule.SourceCategory, rule.TargetCategory));
                            cooccurringCategories.Add((rule.TargetCategory, rule.SourceCategory));
                        }
                    }
                    else if (req?.RequirementMethods != null)
                    {
                        // Fallback for requirements without domain association rules (legacy):
                        // methods co-occurring in the requirement have affinity.
                        var categories = req.RequirementMethods
                            .Select(m => m.Category)
                            .Where(c => !string.IsNullOrEmpty(c))
                            .Distinct()
                            .ToList();

                        for (int i = 0; i < categories.Count; i++)
                        {
                            for (int j = i + 1; j < categories.Count; j++)
                            {
                                cooccurringCategories.Add((categories[i], categories[j]));
                                cooccurringCategories.Add((categories[j], categories[i]));
                            }
                        }
                    }
                }
            }

            foreach (var relation in diagram.Relations)
            {
                if (relation.Origin == relation.Destination) continue;

                // Canonical key by reference identity: registering both directions prevents
                // depending on hash ordering (which collides and could report the same pair twice).
                var pairKey = (relation.Origin, relation.Destination);
                if (!seenPairs.Add(pairKey) || !seenPairs.Add((relation.Destination, relation.Origin))) continue;

                var sourceCategories = new HashSet<string>(
                    relation.Origin.Methods.Select(m => m.Category).Where(c => !string.IsNullOrEmpty(c)));
                var targetCategories = new HashSet<string>(
                    relation.Destination.Methods.Select(m => m.Category).Where(c => !string.IsNullOrEmpty(c)));

                if (sourceCategories.Count == 0 || targetCategories.Count == 0) continue;

                bool sharesCategory = sourceCategories.Overlaps(targetCategories);
                if (sharesCategory) continue;

                // If active requirements exist, check if any Origin category co-occurs with any Destination category
                bool hasRequirementAffinity = false;
                if (cooccurringCategories.Count > 0)
                {
                    foreach (var sCat in sourceCategories)
                    {
                        foreach (var tCat in targetCategories)
                        {
                            if (cooccurringCategories.Contains((sCat, tCat)))
                            {
                                hasRequirementAffinity = true;
                                break;
                            }
                        }
                        if (hasRequirementAffinity) break;
                    }
                }

                if (!hasRequirementAffinity)
                {
                    yield return new SmellOccurrence(
                        CodeSmellType.LowAffinityCoupling,
                        new List<string> { relation.Origin.Name, relation.Destination.Name });
                }
            }
        }
    }
}
