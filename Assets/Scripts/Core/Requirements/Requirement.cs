using System;
using System.Collections.Generic;
using Core.Diagram;

namespace Core.Requirements
{
    public class Requirement
    {
        public string Id { get; }
        public string Description { get; }
        public IReadOnlyList<MethodData> RequirementMethods { get; }

        public Requirement(string id, string description, IReadOnlyList<MethodData> requirementMethods)
        {
            Id = id;
            Description = description;
            RequirementMethods = requirementMethods ?? new List<MethodData>();
        }

    }

    public class Trail
    {
        public const int TurnCount = 5;
        public const int DefaultMaxClasses = 7;

        public string Id { get; }
        public string Theme { get; }
        public IReadOnlyList<Requirement> RequirementByTurn { get; }
        public int MaxClasses { get; }

        public Trail(string id, string theme, IReadOnlyList<Requirement> requirementByTurn, int maxClasses = DefaultMaxClasses)
        {
            if (requirementByTurn == null || requirementByTurn.Count != TurnCount)
            {
                throw new ArgumentException(
                    $"Trilha precisa ter {TurnCount} requisitos (um por turno)",
                    nameof(requirementByTurn));
            }
            if (maxClasses < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxClasses),
                    "O limite máximo de classes da trilha deve ser pelo menos 1.");
            }
            Id = id;
            Theme = theme;
            RequirementByTurn = requirementByTurn;
            MaxClasses = maxClasses;
        }

    }
}
