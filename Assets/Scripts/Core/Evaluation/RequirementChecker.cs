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

        public RequirementCheckResult(
            IReadOnlyList<string> fulfilledIds, 
            int domainPoints, 
            IReadOnlyList<AssociationRule> satisfiedAssociations)
        {
            FulfilledIds = fulfilledIds;
            DomainPoints = domainPoints;
            SatisfiedAssociations = satisfiedAssociations;
        }
    }

    public class RequirementChecker
    {
        public RequirementCheckResult CheckRequirements(
            IReadOnlyClassDiagram diagram, 
            IReadOnlyList<Requirement> activeRequirements)
        {
            if (diagram == null || activeRequirements == null)
                return new RequirementCheckResult(new List<string>(), 0, new List<AssociationRule>());

            // One entry per EVALUATED requirement (position), not by Id: EvaluationEngine's RawScore
            // is calculated by position, so two requirements with the same Id must count twice.
            var fulfilledIds = new List<string>(activeRequirements.Count);
            var components = diagram.GetConnectedComponents();

            // 1. Method verification
            foreach (var requirement in activeRequirements)
            {
                if (requirement == null) continue;

                // A requirement without required methods is invalid data: it cannot be fulfilled by vacuity.
                // RequirementLoader rejects this trail at loading; here the guard is defense in depth.
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

            // 2. Domain associations verification
            int totalDomainPoints = 0;
            var satisfiedRules = new List<AssociationRule>();
            var seenPairs = new HashSet<(string, string)>();

            foreach (var req in activeRequirements)
            {
                if (req?.RequiredAssociations == null) continue;

                foreach (var rule in req.RequiredAssociations)
                {
                    var key = string.CompareOrdinal(rule.SourceCategory, rule.TargetCategory) <= 0
                        ? (rule.SourceCategory, rule.TargetCategory)
                        : (rule.TargetCategory, rule.SourceCategory);
                    
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
                        satisfiedRules.Add(rule);
                    }
                }
            }

            return new RequirementCheckResult(fulfilledIds, totalDomainPoints, satisfiedRules);
        }
    }
}
