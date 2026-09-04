using System;
using System.Collections.Generic;
using Core.Evaluation;
using Core.Session;
using UnityEngine;

namespace Managers.Evaluation
{
    public class EvaluationManager : MonoBehaviour
    {
        private MatchSession _matchSession;
        private readonly Dictionary<PlayerSlot, EvaluationResult> _lastResults =
            new Dictionary<PlayerSlot, EvaluationResult>();

        public event Action<PlayerSlot, EvaluationResult> OnUIEvaluationReady;

        public void Initialize(MatchSession matchSession)
        {
            if (_matchSession != null)
            {
                _matchSession.OnEvaluationCompleted -= HandleEvaluationCompleted;
            }

            _matchSession = matchSession;
            _matchSession.OnEvaluationCompleted += HandleEvaluationCompleted;
        }

        private void OnDestroy()
        {
            if (_matchSession != null)
            {
                _matchSession.OnEvaluationCompleted -= HandleEvaluationCompleted;
            }
        }

        private void HandleEvaluationCompleted(EvaluationResult resultOne, EvaluationResult resultTwo)
        {
            CacheAndNotify(PlayerSlot.PlayerOne, resultOne);
            CacheAndNotify(PlayerSlot.PlayerTwo, resultTwo);
        }

        private void CacheAndNotify(PlayerSlot slot, EvaluationResult result)
        {
            _lastResults[slot] = result;
            OnUIEvaluationReady?.Invoke(slot, result);
        }

        public EvaluationResult GetLastResult(PlayerSlot slot)
        {
            return _lastResults.TryGetValue(slot, out var result) ? result : null;
        }
//temporary code for teatateqattae
#if UNITY_EDITOR
        [ContextMenu("Debug/Inject Fake Result (PlayerOne)")]
        private void Debug_InjectFakeResult()
        {
            var fakeSmells = new List<SmellOccurrence>
            {
                new SmellOccurrence(CodeSmellType.LowCohesion, new List<string> { "Pedido" }, severity: 2),
                new SmellOccurrence(CodeSmellType.DuplicateCode, new List<string> { "Usuario", "UsuarioHelper" }),
            };
            var fakeResult = new EvaluationResult(200, 90, new List<string> { "req-fake-1", "req-fake-2" }, fakeSmells);
            CacheAndNotify(PlayerSlot.PlayerOne, fakeResult);
        }
#endif
    }
}