using System;
using System.Collections;
using Core.Session;
using UnityEngine;

namespace Managers
{
    public class TurnTimerService: MonoBehaviour, ITurnTimerContext
    {
        private const float TurnBaseSeconds = 120f;
        private const float TurnIncrementTimer = 30f;

        //Internal State
        private float _remainingTime;
        private bool  _running;
        private int   _lastNotifiedSecond = -1; 

        //INTERFACE ITurnTImerContext
        public int RemainingSeconds => Mathf.CeilToInt(_remainingTime);
        public event Action OnTimeChanged;
        public event Action OnTimeEnded;

        public void StartTurn(int turnNumber)
        {
            _remainingTime = TurnBaseSeconds + ((turnNumber - 1) * TurnIncrementTimer);
            _lastNotifiedSecond = -1;
            _running = true;
            NotifyChange();
        }

        public void Pause() => _running = false;
        public void Resume() => _running = true;

        public void Stop(){
            _running = false;
            _remainingTime = 0;
            NotifyChange();
        }

        private void Update()
        {
            if(!_running || _remainingTime <= 0f) return;

            _remainingTime -= Time.deltaTime;
            
            if (_remainingTime <= 0f)
            {
                _remainingTime = 0f;
                _running       = false;
                NotifyChange();
                OnTimeEnded?.Invoke();
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