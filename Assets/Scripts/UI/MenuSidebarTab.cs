using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Aba lateral no formato "pasta": corpo, aba superior, caixa de ícone e rótulo.
    /// A seleção desloca o conteúdo 10px para a direita (o LayoutGroup controla o root,
    /// então o deslocamento é aplicado no filho "Visual", que não é controlado por ele).
    /// </summary>
    public class MenuSidebarTab : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private RectTransform _visualRoot;
        [SerializeField] private Image _bodyImage;
        [SerializeField] private Image _tabImage;
        [SerializeField] private Image _iconBoxImage;
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _labelText;

        [SerializeField] private bool _isExit;

        private bool _isSelected;
        private bool _subscribed;

        public void Setup(Button button, Image body, Image tab, Image icon, TMP_Text label,
                          bool isExit, Action onClick, RectTransform visualRoot = null, Image iconBox = null)
        {
            _button = button;
            _bodyImage = body;
            _tabImage = tab;
            _iconImage = icon;
            _labelText = label;
            _isExit = isExit;
            _visualRoot = visualRoot;
            _iconBoxImage = iconBox;

            if (_button == null) return;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onClick?.Invoke());
        }

        public void SetSelected(bool selected)
        {
            if (_isSelected == selected) return;
            _isSelected = selected;
            UpdateVisuals();
        }

        private void OnEnable()
        {
            TrySubscribe();
            UpdateVisuals();
        }

        /// <summary>
        /// O OnEnable pode rodar antes do Awake do ThemeManager (ordem de inicialização da
        /// cena). O Start garante a inscrição para a aba reagir às trocas de paleta.
        /// </summary>
        private void Start()
        {
            TrySubscribe();
            UpdateVisuals();
        }

        private void TrySubscribe()
        {
            if (_subscribed || ThemeManager.Instance == null) return;
            ThemeManager.Instance.OnThemeChanged += UpdateVisuals;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed && ThemeManager.Instance != null)
            {
                ThemeManager.Instance.OnThemeChanged -= UpdateVisuals;
            }

            _subscribed = false;
        }

        public void UpdateVisuals()
        {
            var tm = ThemeManager.Instance;
            if (tm == null) return;

            if (_visualRoot != null)
            {
                float shift = _isSelected ? 10f : 0f;
                var min = _visualRoot.offsetMin;
                var max = _visualRoot.offsetMax;
                min.x = shift;
                max.x = shift;
                _visualRoot.offsetMin = min;
                _visualRoot.offsetMax = max;
            }

            if (_isSelected)
            {
                var accent = tm.GetColor(ThemeColorRole.Accent);
                var ink = tm.GetColor(ThemeColorRole.AccentInk);

                if (_bodyImage != null) _bodyImage.color = accent;
                if (_tabImage != null) _tabImage.color = accent;
                if (_iconBoxImage != null) _iconBoxImage.color = ink;
                if (_iconImage != null) _iconImage.color = accent;
                if (_labelText != null)
                {
                    _labelText.color = ink;
                    _labelText.fontStyle = FontStyles.Bold;
                }

                return;
            }

            var raised = tm.GetColor(ThemeColorRole.Raised);
            var muted = tm.GetColor(ThemeColorRole.Muted);

            if (_bodyImage != null) _bodyImage.color = raised;
            if (_tabImage != null) _tabImage.color = raised;
            if (_iconBoxImage != null) _iconBoxImage.color = tm.GetColor(ThemeColorRole.Panel);
            if (_iconImage != null)
                _iconImage.color = _isExit ? tm.GetColor(ThemeColorRole.Danger) : tm.GetColor(ThemeColorRole.Accent);
            if (_labelText != null)
            {
                _labelText.color = muted;
                _labelText.fontStyle = FontStyles.Normal;
            }
        }
    }
}
