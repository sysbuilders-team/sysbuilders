using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Requirements
{
    public class RequirementCatalog : IRequirementProvider
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<Trail>> _trailByTheme;
        private readonly Random _random;

        public RequirementCatalog(IReadOnlyDictionary<string, IReadOnlyList<Trail>> trailByTheme, Random random = null)
        {
            _trailByTheme = trailByTheme ?? throw new ArgumentNullException(nameof(trailByTheme));
            _random = random ?? new Random();
        }

        public Trail SelectTrail(string theme)
        {   
            if (string.IsNullOrWhiteSpace(theme))
                throw new ArgumentException("O tema não pode ser vazio.", nameof(theme));

            if (!_trailByTheme.TryGetValue(theme, out var candidateTrails) || candidateTrails.Count == 0)
            {
                throw new InvalidOperationException($"Nenhuma trilha configurada para o tema '{theme}'.");
            }

            return candidateTrails[_random.Next(candidateTrails.Count)];
        }

        public Trail GetTrailById(string theme, string trailId)
        {
            if (string.IsNullOrWhiteSpace(theme))
                throw new ArgumentException("O tema não pode ser vazio.", nameof(theme));

            if (string.IsNullOrWhiteSpace(trailId))
                throw new ArgumentException("O ID da trilha não pode ser vazio.", nameof(trailId));

            if (!_trailByTheme.TryGetValue(theme, out var candidateTrails) || candidateTrails.Count == 0)
            {
                throw new InvalidOperationException($"Nenhuma trilha configurada para o tema '{theme}'.");
            }

            var trail = candidateTrails.FirstOrDefault(t => t.Id == trailId);
            if (trail == null)
            {
                throw new KeyNotFoundException($"Trilha '{trailId}' não encontrada no tema '{theme}'.");
            }

            return trail;
        }

        public IReadOnlyList<Requirement> GetRequirementsForMatch(string theme, string trailId)
        {
            return GetTrailById(theme, trailId).RequirementByTurn;
        }
    }
}