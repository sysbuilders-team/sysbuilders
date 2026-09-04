using System;

namespace Core.Session
{
    public interface ITurnContext
    {
        int CurrentTurn { get; }
        TurnPhase CurrentPhase { get; }
        
        event Action<TurnPhase> OnPhaseChange;
        
        SetPlayerReadyResult ConfirmReady();
    }
}