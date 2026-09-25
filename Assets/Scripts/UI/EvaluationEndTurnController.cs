using System;
using Core.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class EvaluationEndTurnController : MonoBehaviour
    {
        [Header("Painel Raiz")]
        [SerializeField] private GameObject _modalRoot;

        [Header("Cabeçalho")]
        [SerializeField] private TMP_Text _titleText;

        [Header("Pontuação do Jogador")]
        [SerializeField] private TMP_Text _playerPointsEarnedText;
        [SerializeField] private TMP_Text _playerPenaltiesText;
        [SerializeField] private TMP_Text _playerTotalText;

        [Header("Pontuação do Adversário")]
        [SerializeField] private TMP_Text _opponentPointsEarnedText;
        [SerializeField] private TMP_Text _opponentPenaltiesText;
        [SerializeField] private TMP_Text _opponentTotalText;

        [Header("Avançar")]
        [SerializeField] private Button _advanceButton;
        [SerializeField] private TMP_Text _advanceButtonText;

        private ITurnSummaryContext _summaryContext;
        private ITurnPlayerActions _turnPlayerActions;
        private ITurnContext _turnContext;
        private PlayerSlot _localSlot;
        private TurnPhase _previousPhase = TurnPhase.Show;

        public bool IsModalOpen => _modalRoot != null && _modalRoot.activeSelf;
        public event Action OnAdvanceClicked;

        private void Awake()
        {
            SetupButtons();

            if (_modalRoot == null)
            {
                _modalRoot = gameObject;
            }
        }

        public void Initialize(
            ITurnSummaryContext summaryContext,
            ITurnPlayerActions turnPlayerActions,
            ITurnContext turnContext,
            PlayerSlot localSlot)
        {
            UnsubscribeFromEvents();
            SetupButtons();

            _summaryContext = summaryContext ?? throw new ArgumentNullException(nameof(summaryContext));
            _turnPlayerActions = turnPlayerActions ?? throw new ArgumentNullException(nameof(turnPlayerActions));
            _turnContext = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
            _localSlot = localSlot;

            _turnContext.OnPhaseChange += HandlePhaseChange;
            _previousPhase = _turnContext.CurrentPhase;

            HideModal();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();

            if (_advanceButton != null)
            {
                _advanceButton.onClick.RemoveListener(HandleAdvanceButtonClicked);
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_turnContext != null)
            {
                _turnContext.OnPhaseChange -= HandlePhaseChange;
            }
        }

        private void SetupButtons()
        {
            if (_advanceButton != null)
            {
                _advanceButton.onClick.RemoveListener(HandleAdvanceButtonClicked);
                _advanceButton.onClick.AddListener(HandleAdvanceButtonClicked);

                if (_advanceButtonText == null)
                {
                    _advanceButtonText = _advanceButton.GetComponentInChildren<TMP_Text>();
                }
            }
        }

        private void HandlePhaseChange(TurnPhase newPhase)
        {
            if (newPhase == TurnPhase.Show && (_previousPhase == TurnPhase.Evaluate || _turnContext.CurrentTurn > 1))
            {
                ShowModal();
            }
            else if (newPhase == TurnPhase.Build || newPhase == TurnPhase.Completed)
            {
                HideModal();
            }

            _previousPhase = newPhase;
        }

        public void ShowModal()
        {
            UpdateDisplays();

            if (_advanceButton != null)
            {
                _advanceButton.interactable = true;
            }

            if (_advanceButtonText != null)
            {
                _advanceButtonText.text = "Avançar para o Próximo Turno";
            }

            if (_modalRoot != null)
            {
                _modalRoot.SetActive(true);
            }
        }

        public void HideModal()
        {
            if (_modalRoot != null)
            {
                _modalRoot.SetActive(false);
            }
        }

        private void HandleAdvanceButtonClicked()
        {
            if (_turnPlayerActions == null) return;

            var result = _turnPlayerActions.SetPlayerReady(_localSlot);
            if (result == SetPlayerReadyResult.Success || result == SetPlayerReadyResult.AlreadyReady)
            {
                if (_advanceButton != null)
                {
                    _advanceButton.interactable = false;
                }

                if (_advanceButtonText != null)
                {
                    _advanceButtonText.text = "Aguardando adversário...";
                }

                OnAdvanceClicked?.Invoke();
            }
        }

        private void UpdateDisplays()
        {
            if (_summaryContext == null) return;

            int completedTurn = _turnContext != null ? Mathf.Max(1, _turnContext.CurrentTurn - 1) : 1;
            int maxTurns = _turnContext != null ? _turnContext.MaxTurns : 5;

            if (_titleText != null)
            {
                _titleText.text = $"Resumo da Rodada — Turno {completedTurn}/{maxTurns}";
            }

            int pEarned = _summaryContext.RoundPointsEarned;
            int pPenalty = _summaryContext.RoundPenaltyPoints;
            int pTotal = pEarned - pPenalty;

            if (_playerPointsEarnedText != null) _playerPointsEarnedText.text = $"Pontos Obtidos: +{pEarned}";
            if (_playerPenaltiesText != null) _playerPenaltiesText.text = $"Penalidades: -{pPenalty}";
            if (_playerTotalText != null) _playerTotalText.text = $"Total da Rodada: {pTotal} pts";

            int oppEarned = _summaryContext.RoundOpponentPointsEarned;
            int oppPenalty = _summaryContext.RoundOpponentPenaltyPoints;
            int oppTotal = oppEarned - oppPenalty;

            if (_opponentPointsEarnedText != null) _opponentPointsEarnedText.text = $"Adversário Obtidos: +{oppEarned}";
            if (_opponentPenaltiesText != null) _opponentPenaltiesText.text = $"Adversário Penalidades: -{oppPenalty}";
            if (_opponentTotalText != null) _opponentTotalText.text = $"Total do Adversário: {oppTotal} pts";
        }
    }
}
