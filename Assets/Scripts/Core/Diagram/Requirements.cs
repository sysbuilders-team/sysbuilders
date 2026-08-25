using System.Collections.Generic;
using Core.Diagram;

namespace Core.Requirements
{
    public class Requirement
    {
        private readonly List<MethodData> _requiredMethods;

        public string Description { get; }
        public IReadOnlyList<MethodData> RequiredMethods => _requiredMethods;

        public Requirement(string description, List<MethodData> requiredMethods)
        {
            Description = description;
            _requiredMethods = requiredMethods ?? new List<MethodData>();
        }
    }
}