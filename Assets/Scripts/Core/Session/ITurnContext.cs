using System;

namespace Core.Session
{
    // Interface responsavel pelo fluxo de estado de turno
    public interface ITurnContext
    {
        int CurrentTurn { get; }
        int MaxTurns { get; }
        TurnPhase CurrentPhase { get; }
        int ReadyPlayersCount { get;}
        int RequiredPlayers { get; }

        event Action<TurnPhase> OnPhaseChange;
        event Action<int, int> OnReadyPlayersChanged;
    }
}