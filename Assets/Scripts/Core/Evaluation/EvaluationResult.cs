using System;
using System.Collections.Generic;

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
