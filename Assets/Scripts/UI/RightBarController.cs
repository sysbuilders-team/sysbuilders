using System;
using Core.Diagram;
using Core.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class RightBarController : MonoBehaviour
    {
        [Header("Placar")]
        [SerializeField] private TMP_Text _playerScoreText;
        [SerializeField] private TMP_Text _enemyScoreText;

        [Header("Refatoração")]
        [SerializeField] private TMP_Text _refactorPointsText;
        [SerializeField] private Button _refactorClassButton;

        [Header("Finalizar Turno")]
        [SerializeField] private Button _endTurnButton;
        [SerializeField] private TMP_Text _endTurnButtonText;

        private IScoreContext _scoreContext;
        private IRefactorContext _refactorContext;
        private ITurnPlayerActions _turnPlayerActions;
        private ITurnContext _turnContext;
        private PlayerSlot _localSlot;
        private IDiagramActions _diagramActions;

        public event Action OnRefactorButtonClicked;
        public event Action OnEndTurnTriggered;

        private void Awake()
        {
            SetupButtons();
        }

        public void Initialize(
            IScoreContext scoreContext,
            IRefactorContext refactorContext,
            ITurnPlayerActions turnPlayerActions,
            PlayerSlot localSlot,
            ITurnContext turnContext = null,
            IDiagramActions diagramActions = null)
        {
            UnsubscribeFromEvents();
            SetupButtons();

            _scoreContext = scoreContext ?? throw new ArgumentNullException(nameof(scoreContext));
            _refactorContext = refactorContext ?? throw new ArgumentNullException(nameof(refactorContext));
            _turnPlayerActions = turnPlayerActions ?? throw new ArgumentNullException(nameof(turnPlayerActions));
            _localSlot = localSlot;
            _turnContext = turnContext;
            _diagramActions = diagramActions;
            if (_diagramActions != null)
                _diagramActions.OnClassSelected += HandleClassSelectionChanged;

            _scoreContext.OnScoreUpdated += HandleScoreUpdated;
            _refactorContext.OnRefactorPointsChanged += HandleRefactorPointsChanged;

            if (_turnContext != null)
            {
                _turnContext.OnPhaseChange += HandlePhaseChange;
            }

            UpdateAllDisplays();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();

            if (_refactorClassButton != null)
            {
                _refactorClassButton.onClick.RemoveListener(HandleRefactorClicked);
            }

            if (_endTurnButton != null)
            {
                _endTurnButton.onClick.RemoveListener(HandleEndTurnClicked);
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_scoreContext != null)
            {
                _scoreContext.OnScoreUpdated -= HandleScoreUpdated;
            }

            if (_refactorContext != null)
            {
                _refactorContext.OnRefactorPointsChanged -= HandleRefactorPointsChanged;
            }

            if (_turnContext != null)
            {
                _turnContext.OnPhaseChange -= HandlePhaseChange;
            }

            if (_diagramActions != null)
            {
                _diagramActions.OnClassSelected -= HandleClassSelectionChanged;
            }
        }

        private void SetupButtons()
        {
            if (_refactorClassButton != null)
            {
                _refactorClassButton.onClick.RemoveListener(HandleRefactorClicked);
                _refactorClassButton.onClick.AddListener(HandleRefactorClicked);
            }

            if (_endTurnButton != null)
            {
                _endTurnButton.onClick.RemoveListener(HandleEndTurnClicked);
                _endTurnButton.onClick.AddListener(HandleEndTurnClicked);

                if (_endTurnButtonText == null)
                {
                    _endTurnButtonText = _endTurnButton.GetComponentInChildren<TMP_Text>();
                }
            }
        }

        private void HandleScoreUpdated()
        {
            UpdateScoreDisplay();
        }

        private void HandleRefactorPointsChanged(int newPoints)
        {
            UpdateRefactorDisplay(newPoints);
        }

        private void HandlePhaseChange(TurnPhase newPhase)
        {
            UpdateEndTurnButtonState(newPhase);
        }

        private void HandleClassSelectionChanged(ClassNode selectedNode)
        {
            UpdateRefactorDisplay(_refactorContext != null ? _refactorContext.RefactorPoints : 0);
        }

        private void HandleRefactorClicked()
        {
            if (_refactorContext != null)
            {
                _refactorContext.TryRefactorSelectedClass();
            }

            OnRefactorButtonClicked?.Invoke();
        }

        private void HandleEndTurnClicked()
        {
            if (_turnPlayerActions == null) return;

            var result = _turnPlayerActions.SetPlayerReady(_localSlot);
            if (result == SetPlayerReadyResult.Success || result == SetPlayerReadyResult.AlreadyReady)
            {
                SetEndTurnWaitingState();
                OnEndTurnTriggered?.Invoke();
            }
        }

        private void SetEndTurnWaitingState()
        {
            if (_endTurnButton != null)
            {
                _endTurnButton.interactable = false;
            }

            if (_endTurnButtonText != null)
            {
                _endTurnButtonText.text = "Aguardando adversário...";
            }
        }

        private void UpdateAllDisplays()
        {
            UpdateScoreDisplay();
            UpdateRefactorDisplay(_refactorContext != null ? _refactorContext.RefactorPoints : 0);
            UpdateEndTurnButtonState(_turnContext != null ? _turnContext.CurrentPhase : TurnPhase.Build);
        }

        private void UpdateScoreDisplay()
        {
            if (_scoreContext == null) return;

            bool isPlayerOne = _localSlot == PlayerSlot.PlayerOne;
            string myName = isPlayerOne ? _scoreContext.PlayerOneName : _scoreContext.PlayerTwoName;
            int myScore = isPlayerOne ? _scoreContext.PlayerOneScore : _scoreContext.PlayerTwoScore;

            string enemyName = isPlayerOne ? _scoreContext.PlayerTwoName : _scoreContext.PlayerOneName;
            int enemyScore = isPlayerOne ? _scoreContext.PlayerTwoScore : _scoreContext.PlayerOneScore;

            if (_playerScoreText != null)
            {
                _playerScoreText.text = $"{myName}: {myScore} pts";
            }

            if (_enemyScoreText != null)
            {
                _enemyScoreText.text = $"{enemyName}: {enemyScore} pts";
            }
        }

        private void UpdateRefactorDisplay(int currentPoints)
        {
            int cost = _refactorContext != null ? _refactorContext.RefactorCost : 1;

            if (_refactorPointsText != null)
            {
                _refactorPointsText.text = $"Pontos de Refatoração: {currentPoints} (Custo: {cost})";
            }

            if (_refactorClassButton != null)
            {
                _refactorClassButton.interactable = _refactorContext != null && _refactorContext.CanRefactorSelected();
            }
        }

        private Func<bool> _canEndTurnPredicate;

        public void SetCanEndTurnPredicate(Func<bool> predicate)
        {
            _canEndTurnPredicate = predicate;
            UpdateEndTurnButtonState(_turnContext != null ? _turnContext.CurrentPhase : TurnPhase.Build);
        }

        public void RefreshEndTurnButton()
        {
            UpdateEndTurnButtonState(_turnContext != null ? _turnContext.CurrentPhase : TurnPhase.Build);
        }

        private void UpdateEndTurnButtonState(TurnPhase phase)
        {
            if (_endTurnButton == null) return;

            if (phase == TurnPhase.Build)
            {
                bool canEnd = _canEndTurnPredicate == null || _canEndTurnPredicate();
                _endTurnButton.interactable = canEnd;
                if (_endTurnButtonText != null)
                {
                    _endTurnButtonText.text = canEnd ? "Finalizar Turno" : "Distribua os Métodos";
                }
            }
            else
            {
                _endTurnButton.interactable = false;
                if (_endTurnButtonText != null && phase == TurnPhase.Completed)
                {
                    _endTurnButtonText.text = "Partida Concluída";
                }
            }
        }
    }
}
