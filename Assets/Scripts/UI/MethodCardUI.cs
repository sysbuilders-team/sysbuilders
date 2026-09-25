using System;
using Core.Diagram;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class MethodCardUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _methodNameText;
        [SerializeField] private Button _button;

        public MethodData Method { get; private set; }
        public event Action<MethodData> OnCardClicked;

        private void Awake()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_button != null)
            {
                _button.onClick.AddListener(HandleClick);
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }
        }

        public void Setup(MethodData method)
        {
            Method = method;

            if (_methodNameText == null)
            {
                _methodNameText = GetComponentInChildren<TMP_Text>();
            }

            if (_methodNameText != null)
            {
                _methodNameText.text = method != null ? method.Name : string.Empty;
            }
        }

        private void HandleClick()
        {
            if (Method != null)
            {
                OnCardClicked?.Invoke(Method);
            }
        }
    }
}
