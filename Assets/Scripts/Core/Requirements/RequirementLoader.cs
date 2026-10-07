using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using System;
using Newtonsoft.Json;

namespace Core.Requirements
{
    public static class RequirementLoader
    {
        public static IReadOnlyDictionary<string, IReadOnlyList<Trail>> LoadFromJson(string jsonContent){

            if(string.IsNullOrWhiteSpace(jsonContent)){
                throw new ArgumentException("O conteúdo JSON não pode ser nulo ou vazio.", nameof(jsonContent));
            }

            Dictionary<string, List<TrailDto>> rawData;
            try
            {
                rawData = JsonConvert.DeserializeObject<Dictionary<string, List<TrailDto>>>(jsonContent);
            }catch (JsonException ex)
            {
                throw new FormatException("Estrutura do JSON de requisitos é invalida.", ex);
            }

            if(rawData == null)
            {
                throw new FormatException("O JSON desserializado retornou um resultado nulo.");
            }

            var result = new Dictionary<string, IReadOnlyList<Trail>>();

            foreach(var (theme, trailDtos) in rawData)
            {
                if(trailDtos == null || trailDtos.Count == 0)
                {
                    throw new FormatException($"O tema '{theme}' não possui nenhuma trilha configurada.");
                }

                var trails = new List<Trail>();
                var trailIds = new HashSet<string>(StringComparer.Ordinal);

                foreach(var dto in trailDtos)
                {
                    ValidateTrailDto(dto, theme);

                    if (!trailIds.Add(dto.Id))
                    {
                        throw new FormatException($"A trilha '{dto.Id}' aparece mais de uma vez no tema '{theme}'.");
                    }

                    var requirements = dto.RequirementsByTurn.Select(r =>
                    {
                        ValidateRequirementDto(r, dto.Id);

                        var methods = r.RequiredMethods
                            .Select(m => new MethodData(m.Name, m.Category))
                            .ToList();

                          return new Requirement(r.Id, r.Description ?? string.Empty, methods);
                    }).ToList();

                    // A duracao da partida e o tamanho da trilha (TurnManager.MaxTurns usa Trail.TurnCount):
                    // um numero diferente de requisitos deixaria turnos sem requisito ou requisitos ignorados.
                    if (requirements.Count != Trail.TurnCount)
                    {
                        throw new FormatException(
                            $"A trilha '{dto.Id}' no tema '{theme}' tem {requirements.Count} requisitos; o esperado e {Trail.TurnCount}.");
                    }

                    var requirementIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var requirement in requirements)
                    {
                        if (!requirementIds.Add(requirement.Id))
                        {
                            throw new FormatException(
                                $"O requisito '{requirement.Id}' aparece mais de uma vez na trilha '{dto.Id}'.");
                        }
                    }

                    trails.Add(new Trail(dto.Id, theme, requirements));
                }
                result[theme] = trails;
            }
            return result;
        }
        
        private static void ValidateTrailDto(TrailDto dto, string theme)
        {
            if (dto == null)
            {
                throw new FormatException($"Encontrada uma trilha nula no tema '{theme}'.");
            }
            if (string.IsNullOrEmpty(dto.Id))
            {
                throw new FormatException($"Encontrada uma trilha sem ID no tema '{theme}'.");
            }
            if(dto.RequirementsByTurn == null)
            {
                throw new FormatException($"A trilha '{dto.Id}' no tema '{theme}' não contém a lista de requisitos.");
            }
        }

        private static void ValidateRequirementDto(RequirementDto dto, string trailId)
        {
            if (dto == null)
            {
                throw new FormatException($"Encontrado um requisito nulo na trilha '{trailId}'.");
            }
            if (string.IsNullOrEmpty(dto.Id))
            {
                throw new FormatException($"Encontrado um requisito sem ID na trilha '{trailId}'.");
            }
            // Requisito sem metodos exigidos nao e cumprivel: falhar na carga evita a pontuacao
            // automatica de 100 por turno sem o jogador construir nada.
            if (dto.RequiredMethods == null || dto.RequiredMethods.Count == 0)
            {
                throw new FormatException(
                    $"O requisito '{dto.Id}' da trilha '{trailId}' nao define nenhum metodo exigido em 'RequiredMethods'.");
            }
            if (dto.RequiredMethods.Any(m => m == null))
            {
                throw new FormatException(
                    $"O requisito '{dto.Id}' da trilha '{trailId}' contem um metodo nulo em 'RequiredMethods'.");
            }
        }
        private class TrailDto
        {
            public string Id { get; set; }
            public List<RequirementDto> RequirementsByTurn { get; set; }
        }

        private class RequirementDto
        {
            public string Id { get; set; }
            public string Description { get; set; }
            public List<MethodDto> RequiredMethods { get; set;}
        }

        private class MethodDto
        {
            public string Name { get; set; }
            public string Category { get; set;}
        }
    }
}