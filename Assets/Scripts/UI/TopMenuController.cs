using System;
using Core.Requirements;
using Core.Session;
using TMPro;
using UnityEngine;

namespace UI
{
    public class TopMenuController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text _turnInfoText;
        [SerializeField] private TMP_Text _requirementText;
        [SerializeField] private TMP_Text _timerText;

        private ITurnContext _turnContext;
        private IRequirementContext _requirementContext;
        private ITurnTimerContext _timerContext;

        public void Initialize(ITurnContext turnContext, IRequirementContext requirementContext, ITurnTimerContext timerContext = null)
        {
            UnsubscribeFromEvents();

            _turnContext = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
            _requirementContext = requirementContext ?? throw new ArgumentNullException(nameof(requirementContext));
            _timerContext = timerContext;

            _turnContext.OnPhaseChange += HandlePhaseChange;
            _requirementContext.OnRequirementChanged += HandleRequirementChanged;

            if (_timerContext != null)
            {
                _timerContext.OnTimeChanged += HandleTimeChanged;
                _timerContext.OnTimeEnded += HandleTimeEnded;
            }

            UpdateAllDisplays();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void UnsubscribeFromEvents()
        {
            if (_turnContext != null)
            {
                _turnContext.OnPhaseChange -= HandlePhaseChange;
            }

            if (_requirementContext != null)
            {
                _requirementContext.OnRequirementChanged -= HandleRequirementChanged;
            }

            if (_timerContext != null)
            {
                _timerContext.OnTimeChanged -= HandleTimeChanged;
                _timerContext.OnTimeEnded -= HandleTimeEnded;
            }
        }

        private void HandlePhaseChange(TurnPhase newPhase)
        {
            UpdateTurnInfoDisplay();
        }

        private void HandleRequirementChanged(Requirement newRequirement)
        {
            UpdateRequirementDisplay(newRequirement);
        }

        private void HandleTimeChanged()
        {
            UpdateTimerDisplay();
        }

        private void HandleTimeEnded()
        {
            if (_timerText != null)
            {
                _timerText.text = "00:00";
            }
        }

        private void UpdateAllDisplays()
        {
            UpdateTurnInfoDisplay();
            UpdateRequirementDisplay(_requirementContext?.CurrentRequirement);
            UpdateTimerDisplay();
        }

        private void UpdateTurnInfoDisplay()
        {
            if (_turnInfoText == null || _turnContext == null) return;

            string phaseName = FormatPhaseName(_turnContext.CurrentPhase);
            _turnInfoText.text = $"Turno {_turnContext.CurrentTurn}/{_turnContext.MaxTurns} — Fase: {phaseName}";
        }

        private void UpdateRequirementDisplay(Requirement requirement)
        {
            if (_requirementText == null) return;

            if (requirement != null && !string.IsNullOrWhiteSpace(requirement.Description))
            {
                _requirementText.text = $"<b>Requisito:</b> {requirement.Description}";
            }
            else
            {
                _requirementText.text = "Aguardando novos requisitos...";
            }
        }

        private void UpdateTimerDisplay()
        {
            if (_timerText == null) return;

            if (_timerContext == null)
            {
                _timerText.text = "--:--";
                return;
            }

            int totalSeconds = Mathf.Max(0, _timerContext.RemainingSeconds);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            _timerText.text = $"{minutes:00}:{seconds:00}";
        }

        private string FormatPhaseName(TurnPhase phase)
        {
            return phase switch
            {
                TurnPhase.Show => "Apresentação",
                TurnPhase.Build => "Construção",
                TurnPhase.Evaluate => "Avaliação",
                TurnPhase.Completed => "Partida Finalizada",
                _ => phase.ToString()
            };
        }
    }
}
