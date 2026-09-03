using System.Collections.Generic;
using Core.Evaluation;
using Core.Session;
using Managers.Evaluation;
using TMPro;
using UnityEngine;

namespace Sys.UI.Evaluation
{
    public class PassthroughRequirementLookup : IRequirementDescriptionLookup
    {
        public string GetDescription(string requirementId) => requirementId;
    }

    public class TurnShowUIPresenter : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private EvaluationManager _evaluationManager;
        [SerializeField] private PlayerSlot _viewerSlot;

        [Header("Score Texts")]
        [SerializeField] private TextMeshProUGUI _rawScoreText;
        [SerializeField] private TextMeshProUGUI _penaltyScoreText;
        [SerializeField] private TextMeshProUGUI _totalScoreText;
        [SerializeField] private TextMeshProUGUI _fulfilledRequirementsText;

        [Header("Smell List")]
        [SerializeField] private Transform _smellListContainer;
        [SerializeField] private SmellItemUI _smellItemPrefab;

        private IRequirementDescriptionLookup _requirementLookup = new PassthroughRequirementLookup();
        public void SetRequirementLookup(IRequirementDescriptionLookup lookup)
        {
            _requirementLookup = lookup ?? new PassthroughRequirementLookup();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (_evaluationManager == null) return;

            _evaluationManager.OnUIEvaluationReady += HandleEvaluationReady;
            
            var cached = _evaluationManager.GetLastResult(_viewerSlot);
            if (cached != null)
            {
                Render(cached);
            }
        }

        private void Unsubscribe()
        {
            if (_evaluationManager == null) return;

            _evaluationManager.OnUIEvaluationReady -= HandleEvaluationReady;
        }

        private void HandleEvaluationReady(PlayerSlot slot, EvaluationResult result)
        {
            if (slot != _viewerSlot) return;

            Render(result);
        }

        private void Render(EvaluationResult result)
        {
            _rawScoreText.text = $"+{result.RawScore}";
            _penaltyScoreText.text = $"-{result.PenaltyScore}";
            _totalScoreText.text = result.TotalScore.ToString();

            _fulfilledRequirementsText.text = FormatFulfilledRequirements(result.FulfilledRequirementIds);

            ClearSmellList();
            foreach (var smell in result.DetectedSmells)
            {
                var item = Instantiate(_smellItemPrefab, _smellListContainer);
                item.Setup(smell);
            }
        }

        private string FormatFulfilledRequirements(IReadOnlyList<string> requirementIds)
        {
            if (requirementIds == null || requirementIds.Count == 0)
            {
                return "Nenhum requisito cumprido";
            }

            var descriptions = new List<string>(requirementIds.Count);
            foreach (var id in requirementIds)
            {
                descriptions.Add(_requirementLookup.GetDescription(id));
            }

            return string.Join(", ", descriptions);
        }

        private void ClearSmellList()
        {
            for (int i = _smellListContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_smellListContainer.GetChild(i).gameObject);
            }
        }
    }
}