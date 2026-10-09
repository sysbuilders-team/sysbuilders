using System;
using System.Collections.Generic;

namespace Core.Evaluation
{
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
            // Copias defensivas: o resultado fica no historico do jogador (e cruza a rede), entao nao pode
            // compartilhar as colecoes que o produtor ainda pode mutar.
            FulfilledRequirementIds = fulfilledRequirementIds == null
                ? (IReadOnlyList<string>)Array.Empty<string>()
                : new List<string>(fulfilledRequirementIds);
            DetectedSmells = detectedSmells == null
                ? (IReadOnlyList<SmellOccurrence>)Array.Empty<SmellOccurrence>()
                : new List<SmellOccurrence>(detectedSmells);
        }
    }
}
