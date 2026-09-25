using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Evaluation;
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

        [Header("Navegação")]
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private Button _restartButton;

        private IMatchEndContext _endContext;
        private ISceneNavigationActions _navigationActions;
        private ITurnContext _turnContext;
        private PlayerSlot _localSlot;

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
            PlayerSlot localSlot = PlayerSlot.PlayerOne)
        {
            UnsubscribeFromEvents();
            SetupButtons();

            _endContext = endContext ?? throw new ArgumentNullException(nameof(endContext));
            _navigationActions = navigationActions;
            _turnContext = turnContext;
            _localSlot = localSlot;

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

            int p1Total = _endContext.PlayerOneHistory.Sum(r => r.TotalScore);
            int p2Total = _endContext.PlayerTwoHistory.Sum(r => r.TotalScore);

            bool isPlayerOne = _localSlot == PlayerSlot.PlayerOne;
            int myTotal = isPlayerOne ? p1Total : p2Total;
            int oppTotal = isPlayerOne ? p2Total : p1Total;

            if (_matchResultText != null)
            {
                if (_endContext.IsDraw)
                {
                    _matchResultText.text = "EMPATE!";
                    _matchResultText.color = Color.yellow;
                }
                else
                {
                    bool won = isPlayerOne ? p1Total > p2Total : p2Total > p1Total;
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
                _playerSmellsReportText.text = FormatSmellReport("Seus Code Smells", myHistory);
            }

            if (_opponentSmellsReportText != null)
            {
                _opponentSmellsReportText.text = FormatSmellReport("Code Smells do Adversário", oppHistory);
            }
        }

        private string FormatSmellReport(string header, IReadOnlyList<EvaluationResult> history)
        {
            if (history == null || history.Count == 0)
            {
                return $"<b>{header}:</b>\nNenhum turno avaliado.";
            }

            var allSmells = history.SelectMany(h => h.DetectedSmells).ToList();

            if (allSmells.Count == 0)
            {
                return $"<b>{header}:</b>\nNenhum Code Smell detectado! Arquitetura Limpa.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"<b>{header}:</b>");

            var groupedByType = allSmells.GroupBy(s => s.Type);
            foreach (var group in groupedByType)
            {
                string smellName = TranslateSmell(group.Key);
                int count = group.Count();
                sb.AppendLine($"• {smellName} (Ocorrências: {count})");

                foreach (var occ in group)
                {
                    string classes = string.Join(", ", occ.AffectedClassNames);
                    sb.AppendLine($"   - Classes: {classes}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        private string TranslateSmell(CodeSmellType type)
        {
            return type switch
            {
                CodeSmellType.LargeClass => "Large Class (Classe Muito Grande)",
                CodeSmellType.DuplicateCode => "Duplicate Code (Código Duplicado)",
                CodeSmellType.LowCohesion => "Baixa Coesão",
                CodeSmellType.LowAffinityCoupling => "Acoplamento de Baixa Afinidade",
                _ => type.ToString()
            };
        }
    }
}
