using System;
using System.Collections.Generic;

namespace Core.Requirements
{
    public interface IRequirementProvider
    {
        IReadOnlyList<Requirement> SelectTrailForMatch(string theme, string playerOneId, string playerTwoId);
    }
}