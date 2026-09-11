using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using Core.Requirements;

namespace Core.Evaluation
{
    public class RequirementChecker
    {
        public IReadOnlyList<string> CheckRequirements(IReadOnlyClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements)
        {
            if (diagram == null || activeRequirements == null)
            {
                return new List<string>();
            }

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
    }
}