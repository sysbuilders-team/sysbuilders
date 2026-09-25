using System;

namespace Core.Session
{
    // Interface a ser implementada pela camada Network, referente ao timer de turno
    // TODO: Nao sei se ficaria nessa camada, poderia ficar na camada de rede talvez ou em ui direto e uma classe timer implementaria
    public interface ITurnTimerContext
    {
        int RemainingSeconds { get; }
        
        event Action OnTimeChanged; // TODO: Pode ser passado o currenttime pelo payload do evento
        event Action OnTimeEnded;
    }
}