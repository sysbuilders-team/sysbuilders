using System.Collections.Generic;

namespace Core.Evaluation
{
    public class SmellPenaltyCalculator
    {
        private const int PenaltyLargeClass = 30;
        private const int PenaltyLowCohesion = 40;
        private const int PenaltyDuplicateCode = 50;
        private const int PenaltyLowAffinityCoupling = 30;

        public int CalculatePenalty(IReadOnlyList<SmellOccurrence> smells)
        {
            if (smells == null) return 0;

            int penalty = 0;

            foreach (var smell in smells)
            {
                switch (smell.Type)
                {
                    case CodeSmellType.LargeClass:
                        penalty += PenaltyLargeClass * smell.Severity;
                        break;
                    case CodeSmellType.LowCohesion:
                        penalty += PenaltyLowCohesion * smell.Severity;
                        break;
                    case CodeSmellType.DuplicateCode:
                        penalty += PenaltyDuplicateCode * smell.Severity;
                        break;
                    case CodeSmellType.LowAffinityCoupling:
                        penalty += PenaltyLowAffinityCoupling * smell.Severity;
                        break;
                }
            }

            return penalty;
        }
    }
}