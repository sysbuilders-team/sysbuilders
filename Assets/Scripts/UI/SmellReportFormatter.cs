using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Evaluation;

namespace UI
{
    public static class SmellReportFormatter
    {
        public static string FormatSmellReport(string header, IReadOnlyList<EvaluationResult> history)
        {
            if (history == null || history.Count == 0)
            {
                return $"<b>{header}:</b>\nNenhum turno avaliado.";
            }

            // O detector roda sobre o diagrama INTEIRO a cada turno, entao concatenar o historico
            // contaria o mesmo smell persistente uma vez por turno ("Ocorrencias: 5" para um unico
            // problema nunca corrigido). O que interessa ao jogador e o estado final do diagrama:
            // a ultima avaliacao. Um smell corrigido ao longo da partida deixa de ser reportado.
            var finalSmells = history[history.Count - 1]?.DetectedSmells;

            if (finalSmells == null || finalSmells.Count == 0)
            {
                return $"<b>{header}:</b>\nNenhum Code Smell detectado! Arquitetura Limpa.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"<b>{header}:</b>");

            foreach (var group in finalSmells.GroupBy(s => s.Type).OrderBy(g => g.Key))
            {
                // Agrupa pelo conjunto de classes afetadas para nao repetir a mesma linha no texto.
                var occurrenceLines = group
                    .GroupBy(occ => string.Join(", ", occ.AffectedClassNames))
                    .Select(occGroup => occGroup.Key)
                    .OrderBy(classes => classes, StringComparer.Ordinal)
                    .ToList();

                sb.AppendLine($"• {TranslateSmell(group.Key)} (Ocorrências: {occurrenceLines.Count})");

                foreach (var classes in occurrenceLines)
                {
                    sb.AppendLine($"   - Classes: {classes}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        public static string TranslateSmell(CodeSmellType type)
        {
            return type switch
            {
                CodeSmellType.LargeClass => "Large Class (Classe Muito Grande)",
                CodeSmellType.DuplicateCode => "Duplicate Code (Código Duplicado)",
                CodeSmellType.LowCohesion => "Baixa Coesão",
                CodeSmellType.LowAffinityCoupling => "Acoplamento de Baixa Afinidade",
                _ => type.ToString()
            };
        }
    }
}
