using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    public enum ThemeColorRole
    {
        Bg,
        Panel,
        Raised,
        Line,
        Fg,
        Muted,
        Accent,
        AccentInk,
        Ok,
        Danger,
        Grid
    }

    [System.Serializable]
    public class ColorPaletteDefinition
    {
        public string name;
        // Dark colors
        public Color darkBg;
        public Color darkPanel;
        public Color darkRaised;
        public Color darkLine;
        public Color darkFg;
        public Color darkMuted;
        public Color darkAccent;
        public Color darkAccentInk;
        public Color darkOk;
        public Color darkDanger;
        public Color darkGrid;

        // Light colors
        public Color lightBg;
        public Color lightPanel;
        public Color lightRaised;
        public Color lightLine;
        public Color lightFg;
        public Color lightMuted;
        public Color lightAccent;
        public Color lightAccentInk;
        public Color lightOk;
        public Color lightDanger;
        public Color lightGrid;
    }

    [DisallowMultipleComponent]
    public class ThemeManager : MonoBehaviour
    {
        public static ThemeManager Instance { get; private set; }

        public event Action OnThemeChanged;

        [SerializeField] private int _currentPaletteIndex = 0; // 0 to 4
        [SerializeField] private bool _isLightMode = false;

        public int CurrentPaletteIndex => _currentPaletteIndex;
        public bool IsLightMode => _isLightMode;

        private static readonly ColorPaletteDefinition[] Palettes = new ColorPaletteDefinition[]
        {
            // 1: Graphite & Amber
            new ColorPaletteDefinition
            {
                name = "Graphite & Amber",
                darkBg = Hex("#26231f"), darkPanel = Hex("#2f2b26"), darkRaised = Hex("#38332d"), darkLine = Hex("#4a443b"),
                darkFg = Hex("#ece5d8"), darkMuted = Hex("#a39a89"), darkAccent = Hex("#e0a23b"), darkAccentInk = Hex("#1f1a12"),
                darkOk = Hex("#8fbf7a"), darkDanger = Hex("#d9705a"), darkGrid = new Color(0.92f, 0.90f, 0.85f, 0.08f),

                lightBg = Hex("#ece6d8"), lightPanel = Hex("#f5f1e6"), lightRaised = Hex("#fbf8f0"), lightLine = Hex("#bdb39d"),
                lightFg = Hex("#2a251d"), lightMuted = Hex("#6e6552"), lightAccent = Hex("#b3701a"), lightAccentInk = Hex("#fbf5e8"),
                lightOk = Hex("#3f7a35"), lightDanger = Hex("#b2442d"), lightGrid = new Color(0.16f, 0.15f, 0.11f, 0.07f)
            },
            // 2: Midnight Slate (VS Code)
            new ColorPaletteDefinition
            {
                name = "Midnight Slate",
                darkBg = Hex("#1e1e24"), darkPanel = Hex("#25252d"), darkRaised = Hex("#2d2d38"), darkLine = Hex("#3f3f4a"),
                darkFg = Hex("#e2e2e9"), darkMuted = Hex("#8b8b9e"), darkAccent = Hex("#3b82f6"), darkAccentInk = Hex("#ffffff"),
                darkOk = Hex("#10b981"), darkDanger = Hex("#ef4444"), darkGrid = new Color(1f, 1f, 1f, 0.08f),

                lightBg = Hex("#f1f5f9"), lightPanel = Hex("#f8fafc"), lightRaised = Hex("#ffffff"), lightLine = Hex("#cbd5e1"),
                lightFg = Hex("#0f172a"), lightMuted = Hex("#64748b"), lightAccent = Hex("#2563eb"), lightAccentInk = Hex("#ffffff"),
                lightOk = Hex("#059669"), lightDanger = Hex("#dc2626"), lightGrid = new Color(0f, 0f, 0f, 0.05f)
            },
            // 3: Blueprint
            new ColorPaletteDefinition
            {
                name = "Blueprint",
                darkBg = Hex("#0f172a"), darkPanel = Hex("#1e293b"), darkRaised = Hex("#334155"), darkLine = Hex("#475569"),
                darkFg = Hex("#f8fafc"), darkMuted = Hex("#94a3b8"), darkAccent = Hex("#38bdf8"), darkAccentInk = Hex("#000000"),
                darkOk = Hex("#10b981"), darkDanger = Hex("#ef4444"), darkGrid = new Color(1f, 1f, 1f, 0.08f),

                lightBg = Hex("#e9eef5"), lightPanel = Hex("#f4f7fb"), lightRaised = Hex("#ffffff"), lightLine = Hex("#bdc5d2"),
                lightFg = Hex("#1f2530"), lightMuted = Hex("#64748b"), lightAccent = Hex("#1d4ed8"), lightAccentInk = Hex("#ffffff"),
                lightOk = Hex("#059669"), lightDanger = Hex("#dc2626"), lightGrid = new Color(0.11f, 0.31f, 0.85f, 0.08f)
            },
            // 4: Cyberdeck
            new ColorPaletteDefinition
            {
                name = "Cyberdeck",
                darkBg = Hex("#1a0b1c"), darkPanel = Hex("#241226"), darkRaised = Hex("#2f1833"), darkLine = Hex("#4a2650"),
                darkFg = Hex("#fce7ff"), darkMuted = Hex("#b28cb8"), darkAccent = Hex("#ff007f"), darkAccentInk = Hex("#ffffff"),
                darkOk = Hex("#00ffcc"), darkDanger = Hex("#ff3366"), darkGrid = new Color(1f, 0f, 0.5f, 0.1f),

                lightBg = Hex("#fdf2ff"), lightPanel = Hex("#fae8fc"), lightRaised = Hex("#ffffff"), lightLine = Hex("#e9c6ef"),
                lightFg = Hex("#3b0754"), lightMuted = Hex("#8a4c99"), lightAccent = Hex("#d9006c"), lightAccentInk = Hex("#ffffff"),
                lightOk = Hex("#00b38f"), lightDanger = Hex("#e6004c"), lightGrid = new Color(0.85f, 0f, 0.42f, 0.05f)
            },
            // 5: Terminal Green
            new ColorPaletteDefinition
            {
                name = "Terminal Green",
                darkBg = Hex("#0d1117"), darkPanel = Hex("#161b22"), darkRaised = Hex("#21262d"), darkLine = Hex("#30363d"),
                darkFg = Hex("#c9d1d9"), darkMuted = Hex("#8b949e"), darkAccent = Hex("#3fb950"), darkAccentInk = Hex("#000000"),
                darkOk = Hex("#3fb950"), darkDanger = Hex("#f85149"), darkGrid = new Color(0.25f, 0.73f, 0.31f, 0.08f),

                lightBg = Hex("#f0fdf4"), lightPanel = Hex("#dcfce7"), lightRaised = Hex("#ffffff"), lightLine = Hex("#bbf7d0"),
                lightFg = Hex("#14532d"), lightMuted = Hex("#166534"), lightAccent = Hex("#16a34a"), lightAccentInk = Hex("#ffffff"),
                lightOk = Hex("#15803d"), lightDanger = Hex("#dc2626"), lightGrid = new Color(0.09f, 0.64f, 0.29f, 0.08f)
            }
        };

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _currentPaletteIndex = PlayerPrefs.GetInt("SysPaletteIndex", 0);
            _isLightMode = PlayerPrefs.GetInt("SysIsLightMode", 0) == 1;
        }

        private void Start()
        {
            ApplyTheme();
        }

        public void SetPalette(int paletteIndex)
        {
            if (paletteIndex < 0 || paletteIndex >= Palettes.Length) return;
            _currentPaletteIndex = paletteIndex;
            PlayerPrefs.SetInt("SysPaletteIndex", _currentPaletteIndex);
            PlayerPrefs.Save();
            ApplyTheme();
        }

        public void ToggleLightMode()
        {
            _isLightMode = !_isLightMode;
            PlayerPrefs.SetInt("SysIsLightMode", _isLightMode ? 1 : 0);
            PlayerPrefs.Save();
            ApplyTheme();
        }

        public void SetLightMode(bool lightMode)
        {
            _isLightMode = lightMode;
            PlayerPrefs.SetInt("SysIsLightMode", _isLightMode ? 1 : 0);
            PlayerPrefs.Save();
            ApplyTheme();
        }

        public Color GetColor(ThemeColorRole role)
        {
            var p = Palettes[Mathf.Clamp(_currentPaletteIndex, 0, Palettes.Length - 1)];
            if (_isLightMode)
            {
                return role switch
                {
                    ThemeColorRole.Bg => p.lightBg,
                    ThemeColorRole.Panel => p.lightPanel,
                    ThemeColorRole.Raised => p.lightRaised,
                    ThemeColorRole.Line => p.lightLine,
                    ThemeColorRole.Fg => p.lightFg,
                    ThemeColorRole.Muted => p.lightMuted,
                    ThemeColorRole.Accent => p.lightAccent,
                    ThemeColorRole.AccentInk => p.lightAccentInk,
                    ThemeColorRole.Ok => p.lightOk,
                    ThemeColorRole.Danger => p.lightDanger,
                    ThemeColorRole.Grid => p.lightGrid,
                    _ => Color.white
                };
            }
            else
            {
                return role switch
                {
                    ThemeColorRole.Bg => p.darkBg,
                    ThemeColorRole.Panel => p.darkPanel,
                    ThemeColorRole.Raised => p.darkRaised,
                    ThemeColorRole.Line => p.darkLine,
                    ThemeColorRole.Fg => p.darkFg,
                    ThemeColorRole.Muted => p.darkMuted,
                    ThemeColorRole.Accent => p.darkAccent,
                    ThemeColorRole.AccentInk => p.darkAccentInk,
                    ThemeColorRole.Ok => p.darkOk,
                    ThemeColorRole.Danger => p.darkDanger,
                    ThemeColorRole.Grid => p.darkGrid,
                    _ => Color.white
                };
            }
        }

        public void ApplyTheme()
        {
            var elements = FindObjectsByType<ThemedUIElement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var elem in elements)
            {
                elem.ApplyTheme(this);
            }
            OnThemeChanged?.Invoke();
        }

        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
                return color;
            return Color.white;
        }
    }
}
