using System;

namespace Core.Session
{
    // Interface responsavel pelo fluxo de estado de turno
    public interface ITurnContext
    {
        int CurrentTurn { get; }
        int MaxTurns { get; }
        TurnPhase CurrentPhase { get; }

        event Action<TurnPhase> OnPhaseChange;
    }
}