using System;
using System.Collections;
using System.Linq;
using Core.Diagram;
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
            Session.TurnManager.OnPhaseChange += HandlePhaseChange;

            InitializeUI(localSlot);
            WireUI();

            Session.TurnManager.ForceAdvancePhase();
        }

        private void InitializeUI(PlayerSlot localSlot)
        {
            var turnManager = Session.TurnManager;

            _topMenu.Initialize(
                turnContext: Session.TurnContext,
                requirementContext: Session.RequirementContext
            );

            _rightBar.Initialize(
                scoreContext: Session.ScoreContext,
                refactorContext: Session.RefactorContext,
                turnPlayerActions: turnManager,
                localSlot: localSlot,
                turnContext: Session.TurnContext
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
                localSlot: localSlot
            );
        }

        private void WireUI()
        {
            if (_rightBar != null) _rightBar.OnEndTurnTriggered += HandleEndTurnTriggered;
            if (_evaluationModal != null) _evaluationModal.OnAdvanceClicked += HandleAdvanceClicked;

            if (_rightBar != null && _toolBox != null)
            {
                _rightBar.SetCanEndTurnPredicate(() => !_toolBox.HasPendingMethods);
                _toolBox.OnPendingMethodsChanged += HandlePendingMethodsChanged;
            }

            if (_toolBox != null && _diagram != null)
            {
                _toolBox.OnAssociationModeToggled += _diagram.SetAssociationMode;
                _toolBox.OnMethodCardSelected += HandleMethodCardSelected;
            }

            if (_diagram != null)
            {
                _diagram.OnAssociationCreated += HandleAssociationCreated;
            }
        }

        private void UnwireUI()
        {
            if (_rightBar != null) _rightBar.OnEndTurnTriggered -= HandleEndTurnTriggered;
            if (_evaluationModal != null) _evaluationModal.OnAdvanceClicked -= HandleAdvanceClicked;

            if (_toolBox != null)
            {
                _toolBox.OnPendingMethodsChanged -= HandlePendingMethodsChanged;
                _toolBox.OnMethodCardSelected -= HandleMethodCardSelected;
                if (_diagram != null)
                {
                    _toolBox.OnAssociationModeToggled -= _diagram.SetAssociationMode;
                }
            }

            if (_diagram != null)
            {
                _diagram.OnAssociationCreated -= HandleAssociationCreated;
            }
        }

        private void HandlePendingMethodsChanged(bool hasPending)
        {
            if (_rightBar != null)
            {
                _rightBar.RefreshEndTurnButton();
            }
        }

        private void HandleEndTurnTriggered()
        {
            if (_toolBox != null && _toolBox.HasPendingMethods)
            {
                return;
            }
            OnLocalPlayerReady?.Invoke();
        }

        private void HandleAdvanceClicked()
        {
            OnLocalPlayerReady?.Invoke();
        }

        private void HandleMethodCardSelected(MethodData method)
        {
            if (method == null || _diagram.IsAssociationMode) return;

            var selected = _diagram.SelectedNode;
            if (selected == null || selected.Node == null) return;
            if (selected.Node.Methods.Contains(method)) return;

            _diagram.TryAddMethodToClass(selected.Node, method);
        }

        private void HandleAssociationCreated(Relation relation)
        {
            if (_toolBox != null)
            {
                _toolBox.SetAssociationMode(false);
            }
        }

        private void HandlePhaseChange(TurnPhase phase)
        {
            if (phase == TurnPhase.Evaluate && _diagram != null)
            {
                _diagram.RenderDiagram();
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
            UnwireUI();

            if (Session != null)
            {
                Session.OnEvaluationCompleted -= HandleLocalEvaluationCompleted;
                Session.TurnManager.OnPhaseChange -= HandlePhaseChange;
                Session.Dispose();
                Session = null;
            }
        }
    }
}
