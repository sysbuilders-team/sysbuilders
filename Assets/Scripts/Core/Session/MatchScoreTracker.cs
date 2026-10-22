using System;
using System.Collections.Generic;
using System.Linq;
using Core.Evaluation;

namespace Core.Session
{
    public class MatchScoreTracker : IScoreContext, ITurnSummaryContext, IMatchEndContext
    {
        private readonly PlayerState _playerOne;
        private readonly PlayerState _playerTwo;
        private readonly PlayerSlot _localSlot;

        public event Action OnScoreUpdated;

        private PlayerState LocalPlayer => _localSlot == PlayerSlot.PlayerOne ? _playerOne : _playerTwo;
        private PlayerState OpponentPlayer => _localSlot == PlayerSlot.PlayerOne ? _playerTwo : _playerOne;

        public string PlayerOneName => _playerOne.Name;
        public string PlayerTwoName => _playerTwo.Name;

        // Regra B: o RawScore de cada avaliação já é acumulado (re-verifica todos os requisitos
        // revelados) e a penalidade é recalculada por turno.
        // Placar = RawScore da última avaliação - soma das penalidades de cada turno.
        public int PlayerOneScore => CalculateScore(_playerOne);
        public int PlayerTwoScore => CalculateScore(_playerTwo);

        public int RoundPointsEarned => RoundRawDelta(LocalPlayer);
        public int RoundPenaltyPoints => LocalPlayer.EvaluationHistory.LastOrDefault()?.PenaltyScore ?? 0;

        public int RoundOpponentPointsEarned => RoundRawDelta(OpponentPlayer);
        public int RoundOpponentPenaltyPoints => OpponentPlayer.EvaluationHistory.LastOrDefault()?.PenaltyScore ?? 0;

        public IReadOnlyList<EvaluationResult> PlayerOneHistory => _playerOne.EvaluationHistory;
        public IReadOnlyList<EvaluationResult> PlayerTwoHistory => _playerTwo.EvaluationHistory;

        public bool IsDraw => PlayerOneScore == PlayerTwoScore;
        public string WinnerName => IsDraw ? null : (PlayerOneScore > PlayerTwoScore) ? PlayerOneName : PlayerTwoName;

        public MatchScoreTracker(PlayerState playerOne, PlayerState playerTwo, PlayerSlot localSlot)
        {
            _playerOne = playerOne ?? throw new ArgumentNullException(nameof(playerOne));
            _playerTwo = playerTwo ?? throw new ArgumentNullException(nameof(playerTwo));
            _localSlot = localSlot;
        }

        public void RecordEvaluation(PlayerState player, EvaluationResult result)
        {
            if (player == null || result == null) return;

            player.AddEvaluationResult(result);
            OnScoreUpdated?.Invoke();
        }

        public bool ReceiveOpponentResult(int turnNumber, EvaluationResult result)
        {
            if (result == null || turnNumber <= 0) return false;
            if (turnNumber != OpponentPlayer.EvaluationHistory.Count + 1) return false;

            OpponentPlayer.AddEvaluationResult(result);
            OnScoreUpdated?.Invoke();
            return true;
        }

        public static int RoundRawDelta(PlayerState player)
        {
            if (player == null) return 0;
            var history = player.EvaluationHistory;
            if (history.Count == 0) return 0;

            int previousRaw = history.Count > 1 ? history[history.Count - 2].RawScore : 0;
            return history[history.Count - 1].RawScore - previousRaw;
        }

        public static int CalculateScore(PlayerState player)
        {
            if (player == null) return 0;
            return ScoreRules.Score(player.EvaluationHistory);
        }
    }
}
