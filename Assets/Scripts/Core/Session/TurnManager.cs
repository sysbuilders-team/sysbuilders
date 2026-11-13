using System;
using System.Collections.Generic;
using Core.Requirements;

namespace Core.Session
{
    public enum PlayerSlot
    {
        PlayerOne,
        PlayerTwo
    }

    public enum TurnPhase
    {
        Show,
        Build,
        Evaluate,
        Completed
    }

    public enum SetPlayerReadyResult
    {
        Success,
        AlreadyReady,
        MatchAlreadyCompleted
    }

    // Only TurnManager is allowed to instantiate this Class
    class PlayerReadinessTracker
    {
        private readonly int _maxPlayers;
        private readonly HashSet<PlayerSlot> _readyPlayers = new HashSet<PlayerSlot>();
        public IReadOnlyCollection<PlayerSlot> ReadyPlayers => _readyPlayers;
        
        public PlayerReadinessTracker(int maxPlayers)
        {
            _maxPlayers = maxPlayers;
        }
        
        public bool IsBothPlayersReady()
        {
            return _readyPlayers.Count == _maxPlayers;
        }

        public void SetPlayerReady(PlayerSlot player)
        {
            _readyPlayers.Add(player);
        }

        public void ClearPlayers()
        {
            _readyPlayers.Clear();
        }

        public bool IsPlayerReady(PlayerSlot player)
        {
            return _readyPlayers.Contains(player);
        }
    }
    // used by UI things, pass this instead of a full TurnManager object
    public interface ITurnPlayerActions
    {
        public SetPlayerReadyResult SetPlayerReady(PlayerSlot player);
    }

    public class TurnManager : ITurnPlayerActions, ITurnContext
    {
        public int MaxTurns { get; }
        
        public int CurrentTurn { get; private set; }
        public TurnPhase CurrentPhase { get; private set; }
        private readonly PlayerReadinessTracker _playerReadinessTracker;
        
        // some requirementmanager will subscribe to this, maybe also pass turn number, see this
        public event Action<TurnPhase> OnPhaseChange;
        public event Action<PlayerSlot> OnPlayerReady;

        // requiredPlayers: 2 numa partida de rede, 1 numa partida solo/local (o unico jogador avanca sozinho).
        // maxTurns: por padrao o tamanho de uma trilha, para nao existir uma segunda fonte de verdade para "5 turnos".
        public TurnManager(int requiredPlayers = 2, int maxTurns = Trail.TurnCount)
        {
            if (maxTurns < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTurns), maxTurns, "A partida precisa de pelo menos um turno.");
            }

            MaxTurns = maxTurns;
            CurrentTurn = 1;
            CurrentPhase = TurnPhase.Show;
            _playerReadinessTracker = new PlayerReadinessTracker(Math.Max(1, requiredPlayers));
        }
        

        private void AdvanceCurrentPhase()
        {
            switch (CurrentPhase)
            {
                case TurnPhase.Show:
                    CurrentPhase = TurnPhase.Build;
                    break;
                case TurnPhase.Build:
                    CurrentPhase = TurnPhase.Evaluate;
                    break;
                case TurnPhase.Evaluate:
                    if (CurrentTurn >= MaxTurns)
                    {
                        CurrentPhase = TurnPhase.Completed;
                    }
                    else
                    {
                        CurrentTurn++;
                        CurrentPhase = TurnPhase.Show;
                    }
                    break;
            }
            OnPhaseChange?.Invoke(CurrentPhase);
        }

        public void AdvancePhase()
        {
            if (!_playerReadinessTracker.IsBothPlayersReady() || CurrentPhase == TurnPhase.Completed) return;

            _playerReadinessTracker.ClearPlayers();

            AdvanceCurrentPhase();
        }
        
        public void ForceAdvancePhase()
        {
            if (CurrentPhase == TurnPhase.Completed) return;

            _playerReadinessTracker.ClearPlayers();

            AdvanceCurrentPhase();
        }
        
        public SetPlayerReadyResult SetPlayerReady(PlayerSlot player)
        {
            if (CurrentPhase == TurnPhase.Completed)
            {
                return SetPlayerReadyResult.MatchAlreadyCompleted;
            }

            if (_playerReadinessTracker.IsPlayerReady(player))
            {
                return SetPlayerReadyResult.AlreadyReady;
            }

            _playerReadinessTracker.SetPlayerReady(player);
            OnPlayerReady?.Invoke(player);

            AdvancePhase();

            return SetPlayerReadyResult.Success;
        }

    
        public bool IsBothPlayersReady()
        {
            return _playerReadinessTracker.IsBothPlayersReady();
        }
        
    }
}
