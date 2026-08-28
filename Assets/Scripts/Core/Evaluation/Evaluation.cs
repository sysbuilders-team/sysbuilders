using System.Collections.Generic;

namespace Core.Evaluation
{
    public enum CodeSmellType
    {
        LargeClass,
        DuplicateCode,
        LowCohesion
    }
    public class SmellOccurrence
    {
        public CodeSmellType Type { get; }
        public IReadOnlyList<string> AffectedClassNames { get; }

        public SmellOccurrence(CodeSmellType type, IReadOnlyList<string> affectedClassNames)
        {
            Type = type;
            AffectedClassNames = affectedClassNames ?? new List<string>();
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