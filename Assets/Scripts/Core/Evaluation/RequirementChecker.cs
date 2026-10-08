using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using Core.Requirements;

namespace Core.Evaluation
{
    public class RequirementChecker
    {
        public IReadOnlyList<string> CheckRequirements(IReadOnlyClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements)
        {
            if (diagram == null || activeRequirements == null)
            {
                return new List<string>();
            }

            // Uma entrada por requisito AVALIADO (posicao), nao por Id: o RawScore do EvaluationEngine
            // e calculado por posicao, entao dois requisitos com o mesmo Id precisam contar duas vezes.
            var fulfilledIds = new List<string>(activeRequirements.Count);
            var components = diagram.GetConnectedComponents();

            foreach (var requirement in activeRequirements)
            {
                if (requirement == null) continue;

                // Requisito sem metodos exigidos e dado invalido: nao pode ser cumprido por vacuidade.
                // O RequirementLoader rejeita essa trilha na carga; aqui a guarda e defesa em profundidade.
                if (requirement.RequirementMethods == null || requirement.RequirementMethods.Count == 0)
                {
                    continue;
                }

                bool isFulfilled = components.Any(component =>
                {
                    var methodsInComponent = new HashSet<(string Name, string Category)>(
                        component.SelectMany(c => c.Methods).Select(m => (m.Name, m.Category ?? string.Empty)));

                    return requirement.RequirementMethods.All(required =>
                        methodsInComponent.Contains((required.Name, required.Category ?? string.Empty)));
                });

                if (isFulfilled)
                {
                    fulfilledIds.Add(requirement.Id);
                }
            }

            return fulfilledIds;
        }
    }
}