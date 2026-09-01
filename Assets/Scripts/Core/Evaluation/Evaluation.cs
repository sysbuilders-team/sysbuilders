using System;
using System.Collections.Generic;

namespace Core.Evaluation
{
    public enum CodeSmellType
    {
        LargeClass,
        DuplicateCode,
        LowCohesion,
        LowAffinityCoupling
    }
    public class SmellOccurrence
    {
        public CodeSmellType Type { get; }
        public IReadOnlyList<string> AffectedClassNames { get; }

        public SmellOccurrence(CodeSmellType type, IReadOnlyList<string> affectedClassNames)
        {
            if (affectedClassNames == null || affectedClassNames.Count == 0)
            {
                throw new ArgumentException("A smell occurrence must affect at least one class.", nameof(affectedClassNames));
            }

            if (type == CodeSmellType.DuplicateCode && affectedClassNames.Count < 2)
            {
                throw new ArgumentException("DuplicateCode requires at least two affected classes.", nameof(affectedClassNames));
            }

            Type = type;
            AffectedClassNames = affectedClassNames;
        }
    }

    public class EvaluationResult
    {
        public int TotalScore { get; }
        public IReadOnlyList<string> FulfilledRequirementIds { get; }
        public IReadOnlyList<SmellOccurrence> DetectedSmells { get; }

        public EvaluationResult(
            int totalScore,
            IReadOnlyList<string> fulfilledRequirementIds,
            IReadOnlyList<SmellOccurrence> detectedSmells)
        {
            TotalScore = totalScore;
            FulfilledRequirementIds = fulfilledRequirementIds ?? new List<string>();
            DetectedSmells = detectedSmells ?? new List<SmellOccurrence>();
        }
    }
}