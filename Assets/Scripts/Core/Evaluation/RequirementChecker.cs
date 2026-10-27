using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using Core.Requirements;

namespace Core.Evaluation
{
    public class RequirementCheckResult
    {
        public IReadOnlyList<string> FulfilledIds { get; }
        public int DomainPoints { get; }
        public IReadOnlyList<AssociationRule> SatisfiedAssociations { get; }
        public IReadOnlyList<AssociationRule> CollocatedAssociations { get; }
        public int AssociationsTotal { get; }
        public int AssociationsPending { get; }

        public RequirementCheckResult(
            IReadOnlyList<string> fulfilledIds,
            int domainPoints,
            IReadOnlyList<AssociationRule> satisfiedAssociations,
            IReadOnlyList<AssociationRule> collocatedAssociations,
            int associationsTotal,
            int associationsPending)
        {
            FulfilledIds = fulfilledIds;
            DomainPoints = domainPoints;
            SatisfiedAssociations = satisfiedAssociations;
            CollocatedAssociations = collocatedAssociations;
            AssociationsTotal = associationsTotal;
            AssociationsPending = associationsPending;
        }
    }

    public class RequirementChecker
    {
        public RequirementCheckResult CheckRequirements(
            IReadOnlyClassDiagram diagram,
            IReadOnlyList<Requirement> activeRequirements)
        {
            if (diagram == null || activeRequirements == null)
                return new RequirementCheckResult(
                    new List<string>(), 0, new List<AssociationRule>(), new List<AssociationRule>(), 0, 0);

            // One entry per EVALUATED requirement (position), not by Id.
            var fulfilledIds = new List<string>(activeRequirements.Count);
            var components = diagram.GetConnectedComponents();

            foreach (var requirement in activeRequirements)
            {
                if (requirement == null) continue;
                if (requirement.RequirementMethods == null || requirement.RequirementMethods.Count == 0)
                    continue;

                bool isFulfilled = components.Any(component =>
                {
                    var methodsInComponent = new HashSet<(string Name, string Category)>(
                        component.SelectMany(c => c.Methods)
                                 .Select(m => (m.Name, m.Category ?? string.Empty)));

                    return requirement.RequirementMethods.All(required =>
                        methodsInComponent.Contains((required.Name, required.Category ?? string.Empty)));
                });

                if (isFulfilled) fulfilledIds.Add(requirement.Id);
            }

            // Domain associations
            int totalDomainPoints = 0;
            var satisfiedRules = new List<AssociationRule>();
            var collocatedRules = new List<AssociationRule>();
            var seenPairs = new HashSet<(string, string)>();

            foreach (var req in activeRequirements)
            {
                if (req?.RequiredAssociations == null) continue;

                foreach (var rule in req.RequiredAssociations)
                {
                    var key = AssociationRule.GetPairKey(rule.SourceCategory, rule.TargetCategory);
                    if (!seenPairs.Add(key)) continue;

                    var sources = diagram.Classes
                        .Where(c => c.Methods.Any(m => m.Category == rule.SourceCategory))
                        .ToList();

                    var targets = diagram.Classes
                        .Where(c => c.Methods.Any(m => m.Category == rule.TargetCategory))
                        .ToList();

                    if (sources.Count == 0 || targets.Count == 0) continue;

                    bool connected = diagram.Relations.Any(r => r.Origin != r.Destination &&
                        ((sources.Contains(r.Origin) && targets.Contains(r.Destination)) ||
                         (targets.Contains(r.Origin) && sources.Contains(r.Destination))));

                    if (connected)
                    {
                        totalDomainPoints += rule.Points;
                        satisfiedRules.Add(rule);
                    }
                    else if (sources.Intersect(targets).Any())
                    {
                        // Same class: satisfied without bonus (discourages God Class)
                        collocatedRules.Add(rule);
                    }
                }
            }

            int total = seenPairs.Count;
            int pending = total - satisfiedRules.Count;

            return new RequirementCheckResult(
                fulfilledIds, totalDomainPoints, satisfiedRules, collocatedRules, total, pending);
        }
    }
}
