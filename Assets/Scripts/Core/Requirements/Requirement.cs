using System;
using System.Collections.Generic;
using Core.Diagram;

namespace Core.Requirements
{
    public class Requirement
    {
        public string Id { get; }
        public String Description { get; }
        private readonly List<MethodData> _requiredMethods = new List<MethodData>();
        public IReadOnlyList<MethodData> RequiredMethods => _requiredMethods;

        public Requirement(String description)
        {
            Id = Guid.NewGuid().ToString();
            Description = description;
        }

        public void AddRequirementMethods(MethodData method)
        {
            _requiredMethods.Add(method);
        }
    }
}