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
                _playerSmellsReportText.text = FormatSmellReport("Seus Code Smells (estado final do diagrama)", myHistory);
            }

            if (_opponentSmellsReportText != null)
            {
                _opponentSmellsReportText.text = FormatSmellReport("Code Smells do Adversário (estado final do diagrama)", oppHistory);
            }
        }

        private string FormatSmellReport(string header, IReadOnlyList<EvaluationResult> history)
        {
            if (history == null || history.Count == 0)
            {
                return $"<b>{header}:</b>\nNenhum turno avaliado.";
            }

            // O detector roda sobre o diagrama INTEIRO a cada turno, entao concatenar o historico
            // contaria o mesmo smell persistente uma vez por turno ("Ocorrencias: 5" para um unico
            // problema nunca corrigido). O que interessa ao jogador e o estado final do diagrama:
            // a ultima avaliacao. Um smell corrigido ao longo da partida deixa de ser reportado.
            var finalSmells = history[history.Count - 1]?.DetectedSmells;

            if (finalSmells == null || finalSmells.Count == 0)
            {
                return $"<b>{header}:</b>\nNenhum Code Smell detectado! Arquitetura Limpa.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"<b>{header}:</b>");

            foreach (var group in finalSmells.GroupBy(s => s.Type).OrderBy(g => g.Key))
            {
                // Agrupa pelo conjunto de classes afetadas para nao repetir a mesma linha no texto.
                var occurrenceLines = group
                    .GroupBy(occ => string.Join(", ", occ.AffectedClassNames))
                    .Select(occGroup => occGroup.Key)
                    .OrderBy(classes => classes, StringComparer.Ordinal)
                    .ToList();

                sb.AppendLine($"• {TranslateSmell(group.Key)} (Ocorrências: {occurrenceLines.Count})");

                foreach (var classes in occurrenceLines)
                {
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
