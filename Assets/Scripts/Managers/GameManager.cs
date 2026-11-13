using System;
using System.Collections;
using Core.Evaluation;
using Core.Session;
using UI;
using UnityEngine;

namespace Managers
{
    public class GameManager : MonoBehaviour, ISceneNavigationActions
    {
        [Header("Manager da Scene")]
        [SerializeField] private RequirementManager _requirementManager;

        [Header("UI Controllers")]
        [SerializeField] private TopMenuController _topMenu;
        [SerializeField] private RightBarController _rightBar;
        [SerializeField] private ToolboxController _toolBox;
        [SerializeField] private DiagramController _diagram;
        [SerializeField] private EvaluationEndTurnController _evaluationModal;
        [SerializeField] private EndGameModalController _endGameModal;

        [Header("Navegação")]
        [SerializeField] private string _menuSceneName = "Menu";

        [Header("Timer")]
        [SerializeField] private TurnTimerService _turnTimer;

        public MatchSession Session { get; private set; }

        /// <summary>
        /// True when the session must wait for the opponent before advancing the phase.
        /// Set only by StartMatch, so it can never disagree with the session it describes.
        /// </summary>
        public bool RequireOpponentResult { get; private set; }

        public event Action OnLocalPlayerReady;
        public event Action OnReturnToMenuRequested;

        private SceneNavigator _sceneNavigator;
        private bool _localEvaluated;
        private bool _advanceScheduled;

        private void Awake()
        {
            _sceneNavigator = new SceneNavigator(
                menuSceneName: _menuSceneName,
                isNetworkMatchProvider: () => RequireOpponentResult,
                onReturnToMenuRequested: () => OnReturnToMenuRequested?.Invoke()
            );
        }

        public void Initialize(string theme, string trailId, PlayerSlot localSlot = PlayerSlot.PlayerOne,
            bool isNetworkMatch = false)
        {
            StartMatch(theme, trailId, localSlot, isNetworkMatch);
        }

        public void StartMatch(string theme, string trailId, PlayerSlot localSlot, bool isNetworkMatch = false)
        {
            if (Session != null)
            {
                Debug.LogWarning("[GameManager] StartMatch ignorado: a partida já foi iniciada.");
                return;
            }

            RequireOpponentResult = isNetworkMatch;

            var trail = _requirementManager.GetTrailById(theme, trailId);
            Session = new MatchSession(
                "Jogador 1",
                "Jogador 2",
                trail.RequirementByTurn,
                localSlot,
                isNetworkMatch: isNetworkMatch,
                maxClassesPerMatch: trail.MaxClasses);

            Session.OnEvaluationCompleted += HandleLocalEvaluationCompleted;
            Session.TurnManager.OnPlayerReady += HandlePlayerReady;

            InitializeUI(localSlot);

            Session.TurnManager.ForceAdvancePhase();
        }

        private void InitializeUI(PlayerSlot localSlot)
        {
            var turnManager = Session.TurnManager;

            if (_turnTimer != null)
            {
                _turnTimer.Initialize(
                    turnContext: Session.TurnContext,
                    turnPlayerActions: turnManager,
                    localSlot: localSlot
                );
            }

            _topMenu.Initialize(
                turnContext: Session.TurnContext,
                requirementContext: Session.RequirementContext,
                timerContext: _turnTimer
            );

            _rightBar.Initialize(
                scoreContext: Session.ScoreContext,
                refactorContext: Session.RefactorContext,
                turnPlayerActions: turnManager,
                localSlot: localSlot,
                turnContext: Session.TurnContext,
                diagramActions: Session.DiagramActions,
                requirementContext: Session.RequirementContext
            );

            _toolBox.Initialize(
                requirementContext: Session.RequirementContext,
                diagramActions: Session.DiagramActions,
                turnContext: Session.TurnContext,
                diagram: Session.LocalPlayer.Diagram
            );

            _diagram.Initialize(
                diagram: Session.LocalPlayer.Diagram,
                diagramActions: Session.DiagramActions
            );

            _evaluationModal.Initialize(
                summaryContext: Session.TurnSummaryContext,
                turnPlayerActions: turnManager,
                turnContext: Session.TurnContext,
                localSlot: localSlot
            );

            _endGameModal.Initialize(
                endContext: Session.MatchEndContext,
                navigationActions: _sceneNavigator,
                turnContext: Session.TurnContext,
                localSlot: localSlot,
                requirementContext: Session.RequirementContext
            );
        }

        private void HandlePlayerReady(PlayerSlot slot)
        {
            if (Session != null && slot == Session.LocalSlot)
            {
                OnLocalPlayerReady?.Invoke();
            }
        }

        private void HandleLocalEvaluationCompleted(EvaluationResult result)
        {
            _localEvaluated = true;
            TryScheduleAdvance();
        }

        public void ReceiveOpponentEvaluation(int turnNumber, EvaluationResult result)
        {
            if (Session == null) return;

            if (!Session.ReceiveOpponentResult(turnNumber, result))
            {
                Debug.LogWarning($"[GameManager] Resultado do oponente ignorado (turno {turnNumber} fora de ordem ou duplicado).");
                return;
            }
        }

        private void TryScheduleAdvance()
        {
            if (_advanceScheduled || !_localEvaluated) return;
            if (RequireOpponentResult) return; // Em rede, o avanço é gerido pelo servidor Mirror via MatchSyncBridge

            _advanceScheduled = true;
            StartCoroutine(AdvanceAfterEvaluation());
        }

        private IEnumerator AdvanceAfterEvaluation()
        {
            yield return null;

            _localEvaluated = false;
            _advanceScheduled = false;

            if (Session != null && Session.TurnManager.CurrentPhase == TurnPhase.Evaluate)
            {
                Session.TurnManager.ForceAdvancePhase();
            }
        }

        #region ISceneNavigationActions Delegation
        public void ReturnToMainMenu() => _sceneNavigator?.ReturnToMainMenu();
        public void RestartMatch() => _sceneNavigator?.RestartMatch();
        #endregion

        private void OnDestroy()
        {
            if (Session != null)
            {
                Session.OnEvaluationCompleted -= HandleLocalEvaluationCompleted;
                Session.TurnManager.OnPlayerReady -= HandlePlayerReady;
                Session.Dispose();
                Session = null;
            }
        }
    }
}
