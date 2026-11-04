using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class GlobalControlsUI : MonoBehaviour
    {
        [Header("Paletas")]
        [SerializeField] private Button[] _paletteButtons;
        [SerializeField] private Image[] _paletteRings;

        [Header("Modo Escuro / Claro")]
        [SerializeField] private Button _themeToggleButton;
        [SerializeField] private TMP_Text _themeToggleText;

        private void Start()
        {
            if (_paletteButtons != null)
            {
                for (int i = 0; i < _paletteButtons.Length; i++)
                {
                    int index = i;
                    if (_paletteButtons[i] == null) continue;
                    _paletteButtons[i].onClick.RemoveAllListeners();
                    _paletteButtons[i].onClick.AddListener(() => ThemeManager.Instance?.SetPalette(index));
                }
            }

            if (_themeToggleButton != null)
            {
                _themeToggleButton.onClick.RemoveAllListeners();
                _themeToggleButton.onClick.AddListener(() => ThemeManager.Instance?.ToggleLightMode());
            }

            if (ThemeManager.Instance != null)
            {
                ThemeManager.Instance.OnThemeChanged += Refresh;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (ThemeManager.Instance != null)
            {
                ThemeManager.Instance.OnThemeChanged -= Refresh;
            }
        }

        private void Refresh()
        {
            var tm = ThemeManager.Instance;
            if (tm == null) return;

            if (_themeToggleText != null)
            {
                // Como no HTML, o rótulo descreve o modo atual ("Modo Escuro" quando está escuro).
                _themeToggleText.text = tm.IsLightMode ? "Modo Claro" : "Modo Escuro";
            }

            if (_paletteRings == null) return;

            for (int i = 0; i < _paletteRings.Length; i++)
            {
                if (_paletteRings[i] == null) continue;
                _paletteRings[i].enabled = i == tm.CurrentPaletteIndex;
            }
        }
    }
}
