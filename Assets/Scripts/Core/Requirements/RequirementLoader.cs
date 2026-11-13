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

                        var associations = new List<AssociationRule>();
                        if (r.RequiredAssociations != null)
                        {
                            foreach (var assoc in r.RequiredAssociations)
                            {
                                ValidateAssociationDto(assoc, r, dto.RequirementsByTurn);
                                associations.Add(new AssociationRule(
                                    assoc.SourceCategory, 
                                    assoc.TargetCategory, 
                                    assoc.Points, 
                                    assoc.Reason, 
                                    assoc.Hint));
                            }
                        }

                        return new Requirement(r.Id, r.Description ?? string.Empty, methods, associations);
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

                    int maxClasses = dto.MaxClasses ?? Trail.DefaultMaxClasses;
                    trails.Add(new Trail(dto.Id, theme, requirements, maxClasses));
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
            if (dto.MaxClasses.HasValue && dto.MaxClasses.Value < 1)
            {
                throw new FormatException(
                    $"A trilha '{dto.Id}' no tema '{theme}' define um MaxClasses inválido ({dto.MaxClasses.Value}). O valor deve ser pelo menos 1.");
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

        private static void ValidateAssociationDto(AssociationDto assoc, RequirementDto currentReq, List<RequirementDto> allTrailReqs)
        {
            if (assoc == null) throw new FormatException($"Regra de associação nula no requisito '{currentReq.Id}'.");
            if (string.IsNullOrWhiteSpace(assoc.SourceCategory) || string.IsNullOrWhiteSpace(assoc.TargetCategory))
                throw new FormatException($"Regra de associação no requisito '{currentReq.Id}' tem categoria vazia.");
            if (assoc.Points <= 0)
                throw new FormatException($"Regra de associação no requisito '{currentReq.Id}' tem pontos inválidos ({assoc.Points}).");

            bool CategoryExists(string c) => allTrailReqs.Any(req =>
                req?.RequiredMethods != null &&
                req.RequiredMethods.Any(m => m != null && m.Category == c));

            bool sourceExists = CategoryExists(assoc.SourceCategory);
            bool targetExists = CategoryExists(assoc.TargetCategory);

            if (!sourceExists || !targetExists)
                throw new FormatException($"A regra '{assoc.SourceCategory} -> {assoc.TargetCategory}' no requisito '{currentReq.Id}' aponta para categoria inexistente na trilha.");
        }

        private class TrailDto
        {
            public string Id { get; set; }
            public int? MaxClasses { get; set; }
            public List<RequirementDto> RequirementsByTurn { get; set; }
        }

        private class RequirementDto
        {
            public string Id { get; set; }
            public string Description { get; set; }
            public List<MethodDto> RequiredMethods { get; set;}
            public List<AssociationDto> RequiredAssociations { get; set; }
        }

        private class MethodDto
        {
            public string Name { get; set; }
            public string Category { get; set;}
        }

        private class AssociationDto
        {
            public string SourceCategory { get; set; }
            public string TargetCategory { get; set; }
            public int Points { get; set; } = 25;
            public string Reason { get; set; }
            public string Hint { get; set; }
        }
    }
}
