using System.Collections.Generic;

namespace Core.Requirements
{
    public interface IRequirementProvider
    {
        Trail SelectTrail(string theme);
        Trail GetTrailById(string theme, string trailId);
        IReadOnlyList<Requirement> GetRequirementsForMatch(string theme, string trailId);
    }
}