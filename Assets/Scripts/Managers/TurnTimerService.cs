using System;
using Core.Session;
using UnityEngine;

namespace Managers
{
    public class TurnTimerService : MonoBehaviour, ITurnTimerContext
    {
        private const float TurnBaseSeconds = 120f;
        private const float TurnIncrementTimer = 30f;

        private ITurnContext _turnContext;
        private ITurnPlayerActions _turnPlayerActions;
        private PlayerSlot _localSlot;

        private float _remainingTime;
        private bool _running;
        private int _lastNotifiedSecond = -1;

        public int RemainingSeconds => Mathf.CeilToInt(_remainingTime);
        public event Action OnTimeChanged;
        public event Action OnTimeEnded;

        public void Initialize(
            ITurnContext turnContext,
            ITurnPlayerActions turnPlayerActions = null,
            PlayerSlot localSlot = PlayerSlot.PlayerOne)
        {
            UnsubscribeFromEvents();

            _turnContext = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
            _turnPlayerActions = turnPlayerActions;
            _localSlot = localSlot;

            _turnContext.OnPhaseChange += HandlePhaseChange;
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void UnsubscribeFromEvents()
        {
            if (_turnContext != null)
            {
                _turnContext.OnPhaseChange -= HandlePhaseChange;
            }
        }

        private void HandlePhaseChange(TurnPhase phase)
        {
            if (phase == TurnPhase.Build && _turnContext != null)
            {
                StartTurn(_turnContext.CurrentTurn);
            }
            else
            {
                Stop();
            }
        }

        public void StartTurn(int turnNumber)
        {
            _remainingTime = TurnBaseSeconds + ((turnNumber - 1) * TurnIncrementTimer);
            _lastNotifiedSecond = -1;
            _running = true;
            NotifyChange();
        }

        public void Pause() => _running = false;
        public void Resume() => _running = true;

        public void Stop()
        {
            _running = false;
            _remainingTime = 0f;
            NotifyChange();
        }

        private void Update()
        {
            if (!_running || _remainingTime <= 0f) return;

            _remainingTime -= Time.deltaTime;

            if (_remainingTime <= 0f)
            {
                _remainingTime = 0f;
                _running = false;
                NotifyChange();
                OnTimeEnded?.Invoke();
                _turnPlayerActions?.SetPlayerReady(_localSlot);
                return;
            }

            int currentSecond = Mathf.CeilToInt(_remainingTime);

            if (currentSecond != _lastNotifiedSecond)
            {
                _lastNotifiedSecond = currentSecond;
                NotifyChange();
            }
        }

        private void NotifyChange() => OnTimeChanged?.Invoke();
    }
}
