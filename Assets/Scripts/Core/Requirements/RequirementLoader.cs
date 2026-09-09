using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using System;
using Unity.Plastic.Newtonsoft.Json;

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

            var result = new Dictionary<string, List<Trail>>();

            foreach(var (theme, trailDtos) in rawData)
            {
                if(trailDtos == null || trailDtos.Count == 0)
                {
                    throw new FormatException($"O tema '{theme}' não possui nenhuma trilha configurada.");
                }

                var trails = new List<Trail>();

                foreach(var dto in trailDtos)
                {
                    ValidateTrailDto(dto, theme);
                    
                    var requirements = dto.RequirementsByTurn.Select(r =>
                    {
                        ValidateRequirementDto(r, dto.Id);

                        var methods = r.RequiredMethods?
                          .Select(m => new MethodData(m.Name, m.Category))
                          .ToList() ?? new List<MethodData>();

                          return new Requirement(r.Id, r.Description ?? string.Empty, methods);
                    }).ToList();

                    var trail = new Trail(dto.Id, theme, requirements);
                    trails.Add(trail);
                }
                result[theme] = trails;
            }
            return result;
        }
        
        private static void ValidateTrailDto(TrailDto dto, string theme)
        {
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
            if (string.IsNullOrEmpty(dto.Id))
            {
                throw new FormatException($"Encontrado um requisito sem ID na trilha '{trailId}'.");
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
            public List<MethodData> RequiredMethods { get; set;}
        }

        private class MethodDto
        {
            public string Name { get; set; }
            public string Category { get; set;}
        }
    }
}