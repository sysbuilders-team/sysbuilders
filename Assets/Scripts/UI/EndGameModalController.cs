using System;
using System.Linq;
using Core.Requirements;
using Core.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class EndGameModalController : MonoBehaviour
    {
        [Header("Painel Raiz")]
        [SerializeField] private GameObject _modalRoot;

        [Header("Resultado")]
        [SerializeField] private TMP_Text _matchResultText;
        [SerializeField] private TMP_Text _finalScoreboardText;

        [Header("Relatório de Code Smells")]
        [SerializeField] private TMP_Text _playerSmellsReportText;
        [SerializeField] private TMP_Text _opponentSmellsReportText;

        [Header("Relatório de Domínio")]
        [SerializeField] private TMP_Text _playerDomainReportText;

        [Header("Navegação")]
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private Button _restartButton;

        private IMatchEndContext _endContext;
        private ISceneNavigationActions _navigationActions;
        private ITurnContext _turnContext;
        private PlayerSlot _localSlot;
        private IRequirementContext _requirementContext;

        public bool IsModalOpen => _modalRoot != null && _modalRoot.activeSelf;

        private void Awake()
        {
            SetupButtons();

            if (_modalRoot == null)
            {
                _modalRoot = gameObject;
            }
        }

        public void Initialize(
            IMatchEndContext endContext,
            ISceneNavigationActions navigationActions,
            ITurnContext turnContext = null,
            PlayerSlot localSlot = PlayerSlot.PlayerOne,
            IRequirementContext requirementContext = null)
        {
            UnsubscribeFromEvents();
            SetupButtons();

            _endContext = endContext ?? throw new ArgumentNullException(nameof(endContext));
            _navigationActions = navigationActions;
            _turnContext = turnContext;
            _localSlot = localSlot;
            _requirementContext = requirementContext;

            if (_turnContext != null)
            {
                _turnContext.OnPhaseChange += HandlePhaseChange;
            }

            HideModal();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();

            if (_mainMenuButton != null)
            {
                _mainMenuButton.onClick.RemoveListener(HandleMainMenuClicked);
            }

            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(HandleRestartClicked);
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
            if (_mainMenuButton != null)
            {
                _mainMenuButton.onClick.RemoveListener(HandleMainMenuClicked);
                _mainMenuButton.onClick.AddListener(HandleMainMenuClicked);
            }

            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(HandleRestartClicked);
                _restartButton.onClick.AddListener(HandleRestartClicked);
            }
        }

        private void HandlePhaseChange(TurnPhase newPhase)
        {
            if (newPhase == TurnPhase.Completed)
            {
                ShowModal();
            }
        }

        public void ShowModal()
        {
            UpdateDisplays();

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

        private void HandleMainMenuClicked()
        {
            _navigationActions?.ReturnToMainMenu();
        }

        private void HandleRestartClicked()
        {
            _navigationActions?.RestartMatch();
        }

        private void UpdateDisplays()
        {
            if (_endContext == null) return;

            // Placar oficial vindo do contexto (mesma regra do IScoreContext). Somar TotalScore do
            // historico somaria o RawScore acumulado varias vezes e divergiria do placar do jogo.
            int p1Total = _endContext.PlayerOneScore;
            int p2Total = _endContext.PlayerTwoScore;

            bool isPlayerOne = _localSlot == PlayerSlot.PlayerOne;
            int myTotal = isPlayerOne ? p1Total : p2Total;
            int oppTotal = isPlayerOne ? p2Total : p1Total;

            if (_matchResultText != null)
            {
                bool isDraw = _endContext.IsDraw || p1Total == p2Total;

                if (isDraw)
                {
                    _matchResultText.text = "EMPATE!";
                    _matchResultText.color = Color.yellow;
                }
                else
                {
                    bool won = myTotal > oppTotal;
                    _matchResultText.text = won ? "VITÓRIA!" : "DERROTA!";
                    _matchResultText.color = won ? Color.green : Color.red;
                }
            }

            if (_finalScoreboardText != null)
            {
                _finalScoreboardText.text = $"Sua Pontuação: {myTotal} pts  |  Adversário: {oppTotal} pts";
            }

            var myHistory = isPlayerOne ? _endContext.PlayerOneHistory : _endContext.PlayerTwoHistory;
            var oppHistory = isPlayerOne ? _endContext.PlayerTwoHistory : _endContext.PlayerOneHistory;

            if (_playerSmellsReportText != null)
            {
                _playerSmellsReportText.text = SmellReportFormatter.FormatSmellReport("Seus Code Smells (estado final do diagrama)", myHistory);
            }

            if (_opponentSmellsReportText != null)
            {
                _opponentSmellsReportText.text = SmellReportFormatter.FormatSmellReport("Code Smells do Adversário (estado final do diagrama)", oppHistory);
            }

            if (_playerDomainReportText != null && _requirementContext != null)
            {
                var lastResult = myHistory.LastOrDefault();
                _playerDomainReportText.text = DomainReportFormatter.FormatDomainReport(
                    "Sua Modelagem de Domínio",
                    _requirementContext.RequirementsHistory,
                    lastResult);
            }
        }
    }
}
