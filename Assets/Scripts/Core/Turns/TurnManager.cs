using System.Collections.Generic;

namespace Core.Turns
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
    public class TurnManager
    {
        // look to inject via constructor, for more personalized TurnManager
        private const int MaxPlayers = 2;
        private const int MaxTurns = 5;
        public int CurrentTurn { get; private set; }
        public TurnPhase CurrentPhase { get; private set; }
        private readonly HashSet<PlayerSlot> _readyPlayers = new HashSet<PlayerSlot>();
        public IReadOnlyCollection<PlayerSlot> ReadyPlayers => _readyPlayers;

        public TurnManager()
        {
            CurrentTurn = 1;
            CurrentPhase = TurnPhase.Show;
        }

        public bool IsBothPlayersReady()
        {
            return _readyPlayers.Count >= MaxPlayers;
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
        }

        public void AdvancePhase()
        {
            if (!IsBothPlayersReady() || CurrentPhase == TurnPhase.Completed) return;
            
            _readyPlayers.Clear();
            
            AdvanceCurrentPhase();
        }

        public void ForceAdvancePhase()
        {
            if (CurrentPhase == TurnPhase.Completed) return;
            
            _readyPlayers.Clear();
            
            AdvanceCurrentPhase();
        }

        public void SetPlayerReady(PlayerSlot player)
        {
            if (CurrentPhase == TurnPhase.Completed) return;
            
            _readyPlayers.Add(player);

            if (IsBothPlayersReady()) AdvancePhase();
        }
        
        
        
    }
}