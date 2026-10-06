using System;
using System.Collections.Generic;

namespace Core.Evaluation
{
    public class SmellPenaltyCalculator
    {
        private const int PenaltyLargeClass = 30;
        private const int PenaltyLowCohesion = 40;
        private const int PenaltyDuplicateCode = 50;
        private const int PenaltyLowAffinityCoupling = 30;

        // Tabela exaustiva: um CodeSmellType novo (Fase 2: LazyClass, Over-engineering) sem entrada
        // aqui faz o calculo falhar alto em vez de penalizar 0 silenciosamente.
        private static readonly IReadOnlyDictionary<CodeSmellType, int> PenaltyBySmell =
            new Dictionary<CodeSmellType, int>
            {
                [CodeSmellType.LargeClass] = PenaltyLargeClass,
                [CodeSmellType.LowCohesion] = PenaltyLowCohesion,
                [CodeSmellType.DuplicateCode] = PenaltyDuplicateCode,
                [CodeSmellType.LowAffinityCoupling] = PenaltyLowAffinityCoupling,
            };

        public int CalculatePenalty(IReadOnlyList<SmellOccurrence> smells)
        {
            if (smells == null) return 0;

            int penalty = 0;

            foreach (var smell in smells)
            {
                if (smell == null) continue;

                if (!PenaltyBySmell.TryGetValue(smell.Type, out int basePenalty))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(smells),
                        smell.Type,
                        $"CodeSmellType '{smell.Type}' nao possui penalidade mapeada em SmellPenaltyCalculator.");
                }

                penalty += basePenalty * smell.Severity;
            }

            return penalty;
        }
    }
}