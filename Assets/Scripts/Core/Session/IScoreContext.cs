using System;

namespace Core.Session
{
    // Used by ui, implemented by matchsession(origin of the data)
    public interface IScoreContext
    {
        string PlayerOneName { get; }
        string PlayerTwoName { get; }
        
        int PlayerOneScore { get; }
        int PlayerTwoScore { get; }

        event Action OnScoreUpdated;
    }
}