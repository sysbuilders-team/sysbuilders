using System;
using System.Collections.Generic;
using Core.Requirements;

namespace Core.Evaluation
{
    public class EvaluationResult
    {
        public int RawScore { get; }
        public int PenaltyScore { get; }
        public int TotalScore { get; }
        public int CleanBonus { get; }
        public int UnallocatedPenalty { get; }
        public int DomainPoints { get; }

        // Rules actually satisfied with separated connected classes
        public IReadOnlyList<AssociationRule> SatisfiedAssociations { get; }
        // Rules whose categories are in the same class (do not score bonus, but counted as pending)
        public IReadOnlyList<AssociationRule> CollocatedAssociations { get; }

        public int AssociationsTotal { get; }
        public int AssociationsPending { get; }

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
            IReadOnlyList<AssociationRule> satisfiedAssociations = null,
            IReadOnlyList<AssociationRule> collocatedAssociations = null,
            int associationsTotal = 0,
            int associationsPending = 0)
        {
            RawScore = rawScore;
            PenaltyScore = penaltyScore;
            CleanBonus = cleanBonus;
            UnallocatedPenalty = unallocatedPenalty;
            DomainPoints = domainPoints;
            TotalScore = Math.Max(0, rawScore - penaltyScore);
            AssociationsTotal = associationsTotal;
            AssociationsPending = associationsPending;

            // Defensive copies: the result stays in player history (and crosses the network),
            // so it cannot share collections that the producer could still mutate.
            SatisfiedAssociations = satisfiedAssociations == null
                ? (IReadOnlyList<AssociationRule>)Array.Empty<AssociationRule>()
                : new List<AssociationRule>(satisfiedAssociations);

            CollocatedAssociations = collocatedAssociations == null
                ? (IReadOnlyList<AssociationRule>)Array.Empty<AssociationRule>()
                : new List<AssociationRule>(collocatedAssociations);

            FulfilledRequirementIds = fulfilledRequirementIds == null
                ? (IReadOnlyList<string>)Array.Empty<string>()
                : new List<string>(fulfilledRequirementIds);

            DetectedSmells = detectedSmells == null
                ? (IReadOnlyList<SmellOccurrence>)Array.Empty<SmellOccurrence>()
                : new List<SmellOccurrence>(detectedSmells);
        }
    }
}
