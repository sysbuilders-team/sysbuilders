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
        public int Severity { get; }

        public SmellOccurrence(CodeSmellType type, IReadOnlyList<string> affectedClassNames, int severity = 1)
        {
            if (affectedClassNames == null || affectedClassNames.Count == 0)
            {
                throw new ArgumentException("A smell occurrence must affect at least one class.", nameof(affectedClassNames));
            }

            if (type == CodeSmellType.DuplicateCode && affectedClassNames.Count < 2)
            {
                throw new ArgumentException("DuplicateCode requires at least two affected classes.", nameof(affectedClassNames));
            }

            if (severity < 1)
            {
                throw new ArgumentException("Severity must be at least 1.", nameof(severity));
            }

            Type = type;
            AffectedClassNames = affectedClassNames;
            Severity = severity;
        }
    }

    public class EvaluationResult
    {
        public int RawScore { get; }
        public int PenaltyScore { get; }
        public int TotalScore { get; }

        public IReadOnlyList<string> FulfilledRequirementIds { get; }
        public IReadOnlyList<SmellOccurrence> DetectedSmells { get; }

        public EvaluationResult(
            int rawScore,
            int penaltyScore,
            IReadOnlyList<string> fulfilledRequirementIds,
            IReadOnlyList<SmellOccurrence> detectedSmells)
        {
            RawScore = rawScore;
            PenaltyScore = penaltyScore;
            TotalScore = Math.Max(0, rawScore - penaltyScore);
            FulfilledRequirementIds = fulfilledRequirementIds ?? new List<string>();
            DetectedSmells = detectedSmells ?? new List<SmellOccurrence>();
        }
    }
}
