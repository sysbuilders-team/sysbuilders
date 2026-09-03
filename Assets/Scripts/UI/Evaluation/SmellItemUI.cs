using System.Collections.Generic;
using Core.Evaluation;
using TMPro;
using UnityEngine;

namespace Sys.UI.Evaluation
{
    public class SmellItemUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _affectedClassesText;
        [SerializeField] private TextMeshProUGUI _severityText;

        private static readonly Dictionary<CodeSmellType, string> FriendlyNames = new Dictionary<CodeSmellType, string>
        {
            { CodeSmellType.LargeClass, "Classe Grande" },
            { CodeSmellType.LowCohesion, "Baixa Coesão" },
            { CodeSmellType.DuplicateCode, "Código Duplicado" },
            { CodeSmellType.LowAffinityCoupling, "Acoplamento Sem Afinidade" },
        };

        public void Setup(SmellOccurrence smell)
        {
            _titleText.text = FriendlyNames.TryGetValue(smell.Type, out var name) ? name : smell.Type.ToString();
            _affectedClassesText.text = string.Join(", ", smell.AffectedClassNames);
            
            if (smell.Severity > 1)
            {
                _severityText.text = $"x{smell.Severity}";
                _severityText.gameObject.SetActive(true);
            }
            else
            {
                _severityText.gameObject.SetActive(false);
            }
        }
    }
}