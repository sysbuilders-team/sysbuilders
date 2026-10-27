using System;
using System.Collections.Generic;
using Core.Requirements;

namespace Core.Evaluation
{
    public class EvaluationResult
    {
        public int RawScore { get; }
        public int PenaltyScore { get; }
        /// <summary>
        /// Net score of this single evaluation: raw minus penalty, floored at zero.
        /// NOT the match score. The match score is <see cref="ScoreRules.Score"/>,
        /// because RawScore is cumulative across turns while penalties are per-turn;
        /// summing TotalScore over a match's history double-counts earlier turns.
        /// </summary>
        public int TotalScore { get; }
        public int CleanBonus { get; }
        public int UnallocatedPenalty { get; }
        public int DomainPoints { get; }
        public IReadOnlyList<AssociationRule> SatisfiedAssociations { get; }

        public IReadOnlyList<string> FulfilledRequirementIds { get; }
        public IReadOnlyList<SmellOccurrence> DetectedSmells { get; }

        public EvaluationResult(
            int rawScore,
            int penaltyScore,
            IReadOnlyList<string> fulfilledRequirementIds,
            IReadOnlyList<SmellOccurrence> detectedSmells,
            int cleanBonus = 0,
            int unallocatedPenalty = 0,
            int domainPoints = 0,
            IReadOnlyList<AssociationRule> satisfiedAssociations = null)
        {
            RawScore = rawScore;
            PenaltyScore = penaltyScore;
            CleanBonus = cleanBonus;
            UnallocatedPenalty = unallocatedPenalty;
            DomainPoints = domainPoints;
            TotalScore = Math.Max(0, rawScore - penaltyScore);

            // Defensive copies: the result remains in the player history (and crosses the network),
            // so it cannot share collections that the producer may still mutate.
            SatisfiedAssociations = satisfiedAssociations == null
                ? (IReadOnlyList<AssociationRule>)Array.Empty<AssociationRule>()
                : new List<AssociationRule>(satisfiedAssociations);

            FulfilledRequirementIds = fulfilledRequirementIds == null
                ? (IReadOnlyList<string>)Array.Empty<string>()
                : new List<string>(fulfilledRequirementIds);

            DetectedSmells = detectedSmells == null
                ? (IReadOnlyList<SmellOccurrence>)Array.Empty<SmellOccurrence>()
                : new List<SmellOccurrence>(detectedSmells);
        }
    }
}
