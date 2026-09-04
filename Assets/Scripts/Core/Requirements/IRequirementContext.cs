using System.Collections.Generic;
using Core.Diagram;

namespace Core.Requirements
{
    public interface IRequirementContext
    {
        Requirement CurrentRequirement { get; }
        IReadOnlyList<Requirement> UnlockedRequirements { get; }
    }
}