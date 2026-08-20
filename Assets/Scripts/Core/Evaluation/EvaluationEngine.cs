using System.Collections.Generic;
using Core.Diagram;
using Core.Requirements;

namespace Core.Evaluation
{
    public class EvaluationEngine
    {
        public EvaluationResult Evaluate(ClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements)
        {
            int score = 0;
            var fulfilledRequirements = new List<string>();
            var smells = new List<CodeSmellType>();

            // 1. Verificar Requisitos Atendidos
            // 2. Detectar Code Smells (Large Class, Low Cohesion, Duplicate Code)
            // 3. Calcular Pontuação Final

            return new EvaluationResult(score, fulfilledRequirements, smells);
        }
    }
}