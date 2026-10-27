using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Evaluation;
using Core.Requirements;

namespace UI
{
    public static class DomainReportFormatter
    {
        public static string FormatDomainReport(
            string header,
            IReadOnlyList<Requirement> activeRequirements,
            EvaluationResult lastResult)
        {
            if (activeRequirements == null || activeRequirements.Count == 0)
                return $"<b>{header}:</b>\nNenhum requisito avaliado.";

            // Deduplicate rules with shared helper
            var allRules = activeRequirements
                .Where(r => r?.RequiredAssociations != null)
                .SelectMany(r => r.RequiredAssociations)
                .GroupBy(r => AssociationRule.GetPairKey(r.SourceCategory, r.TargetCategory))
                .Select(g => g.First())
                .ToList();

            if (allRules.Count == 0)
                return $"<b>{header}:</b>\nNenhuma associação de domínio exigida.";

            var satisfied = lastResult?.SatisfiedAssociations ?? new List<AssociationRule>();
            var collocated = lastResult?.CollocatedAssociations ?? new List<AssociationRule>();

            var sb = new StringBuilder();
            sb.AppendLine($"<b>{header}:</b>");

            foreach (var rule in allRules)
            {
                var key = AssociationRule.GetPairKey(rule.SourceCategory, rule.TargetCategory);

                bool isSatisfied = satisfied.Any(s =>
                    AssociationRule.GetPairKey(s.SourceCategory, s.TargetCategory) == key);
                bool isCollocated = collocated.Any(s =>
                    AssociationRule.GetPairKey(s.SourceCategory, s.TargetCategory) == key);

                string status;
                string color;
                if (isSatisfied) { status = "Acertou"; color = "#4CAF50"; }
                else if (isCollocated) { status = "Mesma classe"; color = "#FFC107"; }
                else { status = "Faltou"; color = "#F44336"; }

                sb.AppendLine($"<color={color}>[{status}]</color> {rule.SourceCategory} e {rule.TargetCategory}");
                sb.AppendLine($"   Motivo: {rule.Reason}");

                if (status == "Mesma classe")
                    sb.AppendLine("   Separe as categorias em classes diferentes e conecte-as.");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
