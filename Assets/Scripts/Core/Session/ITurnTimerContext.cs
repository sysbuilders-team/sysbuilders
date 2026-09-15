using System;

namespace Core.Session
{
    // Interface a ser implementada pela camada Network, referente ao timer de turno
    public interface ITurnTimerContext
    {
        int RemainingSeconds { get; }
        
        event Action OnTimeChanged; // TODO: Pode ser passado o currenttime pelo payload do evento
        event Action OnTimeEnded;
    }
}