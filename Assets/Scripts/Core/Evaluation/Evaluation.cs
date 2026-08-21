using System.Collections.Generic;

namespace Core.Evaluation
{
    public enum CodeSmellType
    {
        LargeClass,
        DuplicateCode,
        LowCohesion
    }

    // Representa UMA ocorrência específica de um smell detectado no diagrama.
    // Por que "AffectedClassNames" é uma LISTA e não um único "ClassName":
    // Large Class e Baixa Coesão acontecem dentro de uma classe só (lista com 1 nome),
    // mas Duplicate Code acontece ESPALHADO entre várias classes ao mesmo tempo
    // (lista com N nomes). Usar sempre uma lista evita ter um campo que só faz
    // sentido para 2 dos 3 smells atuais, e continua funcionando se um smell futuro
    // envolver mais ou menos classes.
    public class SmellOccurrence
    {
        public CodeSmellType Type { get; }
        public IReadOnlyList<string> AffectedClassNames { get; }
        public string Description { get; }

        public SmellOccurrence(CodeSmellType type, IReadOnlyList<string> affectedClassNames, string description)
        {
            Type = type;
            AffectedClassNames = affectedClassNames ?? new List<string>();
            Description = description;
        }
    }

    public class EvaluationResult
    {
        public int TotalScore { get; }
        public IReadOnlyList<string> FulfilledRequirementIds { get; }

        // Antes era IReadOnlyList<CodeSmellType> — só sabia "quais tipos" ocorreram,
        // sem saber onde nem quantas vezes. Agora carrega SmellOccurrence, que tem
        // o Type (pontuação/lógica) e o Description + AffectedClassNames (feedback pro jogador).
        public IReadOnlyList<SmellOccurrence> DetectedSmells { get; }

        public EvaluationResult(
            int totalScore,
            IReadOnlyList<string> fulfilledRequirementIds,
            IReadOnlyList<SmellOccurrence> detectedSmells)
        {
            TotalScore = totalScore;
            FulfilledRequirementIds = fulfilledRequirementIds ?? new List<string>();
            DetectedSmells = detectedSmells ?? new List<SmellOccurrence>();
        }
    }
}