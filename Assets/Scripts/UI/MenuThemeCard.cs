using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>Card de tema (Biblioteca / Hospital / E-Commerce).</summary>
    public class MenuThemeCard : MonoBehaviour
    {
        [SerializeField] private string _themeName;
        [SerializeField] private Button _button;
        [SerializeField] private Image _cardBackground;
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private GameObject _tagSelected;
        [SerializeField] private Image _outlineImage;

        private bool _isSelected;
        private bool _subscribed;

        public string ThemeName => _themeName;

        public void SetThemeName(string themeName)
        {
            if (!string.IsNullOrWhiteSpace(themeName)) _themeName = themeName;
        }

        public void Setup(string themeName, Button button, Image bg, Image icon, TMP_Text title,
                          TMP_Text desc, GameObject tag, Image outline, Action<string> onSelect)
        {
            if (!string.IsNullOrWhiteSpace(themeName)) _themeName = themeName;
            _button = button;
            _cardBackground = bg;
            _iconImage = icon;
            _titleText = title;
            _descriptionText = desc;
            _tagSelected = tag;
            _outlineImage = outline;

            if (_button == null) return;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onSelect?.Invoke(_themeName));
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;

            if (_tagSelected != null) _tagSelected.SetActive(_isSelected);
            if (_cardBackground != null)
            {
                // Borda de 2px no card selecionado (o HTML troca 1px por 2px).
                float inset = _isSelected ? 2f : 1f;
                var surface = _cardBackground.rectTransform;
                surface.offsetMin = new Vector2(inset, inset);
                surface.offsetMax = new Vector2(-inset, -inset);
            }

            UpdateVisuals();
        }

        private void OnEnable()
        {
            TrySubscribe();
            UpdateVisuals();
        }

        /// <summary>
        /// A inscrição também é tentada no Start porque o OnEnable pode rodar antes do Awake
        /// do ThemeManager; sem isso o card não reagiria à troca de paleta.
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

            if (tm == null)
            {
                // No editor fora do Play, mantém a seleção visível usando as cores serializadas.
                if (_tagSelected != null) _tagSelected.SetActive(_isSelected);
                return;
            }

            if (_isSelected)
            {
                if (_cardBackground != null) _cardBackground.color = tm.GetColor(ThemeColorRole.Panel);
                if (_iconImage != null) _iconImage.color = tm.GetColor(ThemeColorRole.Accent);
                if (_titleText != null) _titleText.color = tm.GetColor(ThemeColorRole.Fg);
                if (_descriptionText != null) _descriptionText.color = tm.GetColor(ThemeColorRole.Muted);
                if (_outlineImage != null) _outlineImage.color = tm.GetColor(ThemeColorRole.Accent);
            }
            else
            {
                if (_cardBackground != null) _cardBackground.color = tm.GetColor(ThemeColorRole.Raised);
                if (_iconImage != null) _iconImage.color = tm.GetColor(ThemeColorRole.Muted);
                if (_titleText != null) _titleText.color = tm.GetColor(ThemeColorRole.Fg);
                if (_descriptionText != null) _descriptionText.color = tm.GetColor(ThemeColorRole.Muted);
                if (_outlineImage != null) _outlineImage.color = tm.GetColor(ThemeColorRole.Line);
            }
        }
    }
}
