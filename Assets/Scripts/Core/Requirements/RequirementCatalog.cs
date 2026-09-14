using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Requirements
{
    public class RequirementCatalog : IRequirementProvider
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<Trail>> _trailByTheme;
        private readonly ITrailHistoryStore _historyStore;
        private readonly Random _random;

        public RequirementCatalog(IReadOnlyDictionary<string, IReadOnlyList<Trail>> trailByTheme, ITrailHistoryStore historyStore, Random random = null)
        {
            _trailByTheme = trailByTheme ?? throw new ArgumentNullException(nameof(trailByTheme));
            _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
            _random = random ?? new Random();
        }

        public IReadOnlyList<Requirement> SelectTrailForMatch(string theme, string playerOneId, string playerTwoId)
        {   
            if (string.IsNullOrWhiteSpace(theme))
                throw new ArgumentException("O tema não pode ser vazio.", nameof(theme));

            if (string.IsNullOrWhiteSpace(playerOneId))
                throw new ArgumentException("O ID do jogador 1 não pode ser vazio.", nameof(playerOneId));

            if (string.IsNullOrWhiteSpace(playerTwoId))
                throw new ArgumentException("O ID do jogador 2 não pode ser vazio.", nameof(playerTwoId));

            if (!_trailByTheme.TryGetValue(theme, out var candidateTrails) || candidateTrails.Count == 0)
            {
                throw new InvalidOperationException($"Nenhuma trilha configurada para o tema '{theme}'.");
            }

            var lastP1 = _historyStore.GetLastTrailId(playerOneId, theme);
            var lastP2 = _historyStore.GetLastTrailId(playerTwoId, theme);

            var qualifiedTrails = candidateTrails.Where(t=> t.Id != lastP1 && t.Id != lastP2).ToList();

            var pool = qualifiedTrails.Count > 0 ? qualifiedTrails : candidateTrails;

            var chosenTrail = pool[_random.Next(pool.Count)];

            _historyStore.SaveLastTrailId(playerOneId, theme, chosenTrail.Id);
            _historyStore.SaveLastTrailId(playerTwoId, theme, chosenTrail.Id);

            return chosenTrail.RequirementByTurn;
        }
    }
}