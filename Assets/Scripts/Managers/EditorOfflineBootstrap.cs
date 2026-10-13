#if UNITY_EDITOR
using System.Collections;
using Core.Session;
using UnityEngine;

namespace Managers
{
    [RequireComponent(typeof(GameManager))]
    public class EditorOfflineBootstrap : MonoBehaviour
    {
        [Tooltip("Quando ativado, inicia uma partida local automaticamente ao dar Play na GameScene se nenhuma partida online foi iniciada.")]
        [SerializeField] private bool _autoStartInEditor = true;
        [SerializeField] private string _editorTestTheme = "Biblioteca";
        [SerializeField] private string _editorTestTrailId = "trail_biblioteca_01";
        [SerializeField] private float _delayBeforeAutoStart = 0.2f;

        private GameManager _gameManager;
        private bool _isBootstrapped;

        private void Awake()
        {
            _gameManager = GetComponent<GameManager>();
        }

        private IEnumerator Start()
        {
            if (!_autoStartInEditor) yield break;

            // Aguarda um pequeno intervalo para dar tempo a adaptadores de rede iniciarem se houver rede ativa
            yield return new WaitForSeconds(_delayBeforeAutoStart);

            if (_gameManager == null || _gameManager.Session != null || _gameManager.RequireOpponentResult)
            {
                yield break;
            }

            _isBootstrapped = true;
            Debug.Log("[EditorOfflineBootstrap] Iniciando partida local no Editor para testes.");
            _gameManager.StartMatch(_editorTestTheme, _editorTestTrailId, PlayerSlot.PlayerOne);

            var session = _gameManager.Session;
            if (session != null)
            {
                session.OnEvaluationCompleted += HandleEvaluationCompleted;

                // Avança da fase Show para Build para permitir teste imediato da diagramação no Turno 1
                if (session.TurnManager.CurrentPhase == TurnPhase.Show)
                {
                    session.TurnManager.ForceAdvancePhase();
                }
            }
        }

        public void HandleEditorPlayerReady()
        {
            if (!_isBootstrapped || _gameManager == null || _gameManager.Session == null) return;
            _gameManager.Session.SetPlayerReady(PlayerSlot.PlayerTwo);
        }

        private void HandleEvaluationCompleted(Core.Evaluation.EvaluationResult result)
        {
            if (!_isBootstrapped || _gameManager == null || _gameManager.Session == null) return;
            if (_gameManager.Session.TurnManager.CurrentPhase == TurnPhase.Evaluate)
            {
                _gameManager.Session.TurnManager.ForceAdvancePhase();
            }
        }

        private void OnDestroy()
        {
            if (_isBootstrapped && _gameManager != null && _gameManager.Session != null)
            {
                _gameManager.Session.OnEvaluationCompleted -= HandleEvaluationCompleted;
            }
        }
    }
}
#endif
