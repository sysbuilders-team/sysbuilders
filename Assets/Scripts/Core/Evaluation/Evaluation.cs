using System.Collections.Generic;

namespace Core.Evaluation
{
    public enum CodeSmellType
    {
        LargeClass,
        DuplicateCode,
        LowCohesion
    }

    public class EvaluationResult
    {
        public int TotalScore { get; }
        public IReadOnlyList<string> FulfilledRequirementIds { get; }
        public IReadOnlyList<CodeSmellType> DetectedSmells { get; }

        public EvaluationResult(
            int totalScore, 
            IReadOnlyList<string> fulfilledRequirementIds, 
            IReadOnlyList<CodeSmellType> detectedSmells)
        {
            TotalScore = totalScore;
            FulfilledRequirementIds = fulfilledRequirementIds ?? new List<string>();
            DetectedSmells = detectedSmells ?? new List<CodeSmellType>();
        }
    }
}