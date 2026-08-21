using System;
using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using Core.Requirements;

namespace Core.Evaluation
{
    public class EvaluationEngine
    {
        // Constantes no topo, isoladas: são os únicos números "mágicos" da classe,
        // e são exatamente os que vocês vão querer reequilibrar depois de testar o jogo.
        // Nenhum outro número solto deve aparecer no corpo dos métodos abaixo.
        private const int ScorePerRequirement = 100;
        private const int PenaltyLargeClass = 30;
        private const int PenaltyLowCohesion = 40;
        private const int PenaltyDuplicateCode = 50;
        private const int LargeClassThreshold = 4;

        public EvaluationResult Evaluate(ClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements)
        {
            if (diagram == null || activeRequirements == null)
            {
                return new EvaluationResult(0, new List<string>(), new List<SmellOccurrence>());
            }

            var fulfilledRequirementIds = CheckRequirements(diagram, activeRequirements);
            var detectedSmells = DetectCodeSmells(diagram);
            var totalScore = CalculateScore(fulfilledRequirementIds.Count, detectedSmells);

            return new EvaluationResult(totalScore, fulfilledRequirementIds, detectedSmells);
        }

        // Etapa 1: um requisito só conta como atendido se TODOS os seus métodos
        // exigidos estiverem dentro da MESMA ClassNode. Comparação é por Name
        // (não por referência, não por Category) — decisão de vocês, porque a
        // instância de MethodData dentro do Requirement nunca é a mesma instância
        // que acaba dentro do ClassNode quando o jogador adiciona o método.
        private List<string> CheckRequirements(ClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements)
        {
            var fulfilledIds = new List<string>();

            foreach (var requirement in activeRequirements)
            {
                if (requirement.RequiredMethods == null || requirement.RequiredMethods.Count == 0)
                {
                    continue;
                }

                bool isFulfilled = diagram.Classes.Any(classNode =>
                {
                    var methodNamesInClass = new HashSet<string>(classNode.Methods.Select(m => m.Name));
                    return requirement.RequiredMethods.All(required => methodNamesInClass.Contains(required.Name));
                });

                if (isFulfilled)
                {
                    fulfilledIds.Add(requirement.Id);
                }
            }

            return fulfilledIds;
        }

        // Etapa 2: roda os 3 smells de forma independente. Cada um gera 0 ou mais
        // SmellOccurrence — não paramos no primeiro smell encontrado, uma classe
        // pode ser Large Class E ter Baixa Coesão ao mesmo tempo, por exemplo.
        private List<SmellOccurrence> DetectCodeSmells(ClassDiagram diagram)
        {
            var smells = new List<SmellOccurrence>();

            smells.AddRange(DetectLargeClasses(diagram));
            smells.AddRange(DetectLowCohesion(diagram));
            smells.AddRange(DetectDuplicateCode(diagram));

            return smells;
        }

        private IEnumerable<SmellOccurrence> DetectLargeClasses(ClassDiagram diagram)
        {
            foreach (var classNode in diagram.Classes)
            {
                if (classNode.Methods.Count > LargeClassThreshold)
                {
                    yield return new SmellOccurrence(
                        CodeSmellType.LargeClass,
                        new List<string> { classNode.Name },
                        $"'{classNode.Name}' tem {classNode.Methods.Count} métodos (limite: {LargeClassThreshold}).");
                }
            }
        }

        private IEnumerable<SmellOccurrence> DetectLowCohesion(ClassDiagram diagram)
        {
            foreach (var classNode in diagram.Classes)
            {
                var distinctCategories = classNode.Methods
                    .Select(m => m.Category)
                    .Where(category => !string.IsNullOrEmpty(category))
                    .Distinct()
                    .Count();

                if (distinctCategories >= 2)
                {
                    yield return new SmellOccurrence(
                        CodeSmellType.LowCohesion,
                        new List<string> { classNode.Name },
                        $"'{classNode.Name}' mistura {distinctCategories} categorias diferentes de responsabilidade.");
                }
            }
        }

        private IEnumerable<SmellOccurrence> DetectDuplicateCode(ClassDiagram diagram)
        {
            // Agrupa por nome de método, coletando em quais classes cada um aparece.
            // Se um nome aparece em mais de uma classe, é uma ocorrência de duplicidade
            // que afeta TODAS essas classes ao mesmo tempo (por isso AffectedClassNames
            // como lista faz sentido aqui, diferente dos outros dois smells).
            var classesByMethodName = new Dictionary<string, List<string>>();

            foreach (var classNode in diagram.Classes)
            {
                foreach (var method in classNode.Methods)
                {
                    if (!classesByMethodName.TryGetValue(method.Name, out var classNames))
                    {
                        classNames = new List<string>();
                        classesByMethodName[method.Name] = classNames;
                    }

                    if (!classNames.Contains(classNode.Name))
                    {
                        classNames.Add(classNode.Name);
                    }
                }
            }

            foreach (var entry in classesByMethodName)
            {
                if (entry.Value.Count > 1)
                {
                    yield return new SmellOccurrence(
                        CodeSmellType.DuplicateCode,
                        entry.Value,
                        $"Método '{entry.Key}' duplicado em: {string.Join(", ", entry.Value)}.");
                }
            }
        }

        // Etapa 3: switch centralizado no Type do enum, não em comparação de string
        // (esse era o problema do código que veio do Gemini — string.StartsWith
        // quebra silenciosamente se o texto do Description mudar; enum não quebra).
        private int CalculateScore(int fulfilledRequirementCount, IReadOnlyList<SmellOccurrence> smells)
        {
            int score = fulfilledRequirementCount * ScorePerRequirement;

            foreach (var smell in smells)
            {
                switch (smell.Type)
                {
                    case CodeSmellType.LargeClass:
                        score -= PenaltyLargeClass;
                        break;
                    case CodeSmellType.LowCohesion:
                        score -= PenaltyLowCohesion;
                        break;
                    case CodeSmellType.DuplicateCode:
                        score -= PenaltyDuplicateCode;
                        break;
                }
            }

            return Math.Max(0, score);
        }
    }
}