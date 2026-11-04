using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [DisallowMultipleComponent]
    public class ThemedUIElement : MonoBehaviour
    {
        [System.Serializable]
        public struct ImageColorBinding
        {
            public Graphic targetGraphic;
            public ThemeColorRole role;
        }

        [System.Serializable]
        public struct TextColorBinding
        {
            public TMP_Text targetText;
            public ThemeColorRole role;
        }

        [SerializeField] private ImageColorBinding[] _imageBindings;
        [SerializeField] private TextColorBinding[] _textBindings;

        public void SetBindings(ImageColorBinding[] images, TextColorBinding[] texts)
        {
            _imageBindings = images;
            _textBindings = texts;
        }

        private void OnEnable()
        {
            if (ThemeManager.Instance != null)
            {
                ApplyTheme(ThemeManager.Instance);
            }
        }

        public void ApplyTheme(ThemeManager manager)
        {
            if (manager == null) return;

            if (_imageBindings != null)
            {
                for (int i = 0; i < _imageBindings.Length; i++)
                {
                    if (_imageBindings[i].targetGraphic != null)
                    {
                        _imageBindings[i].targetGraphic.color = manager.GetColor(_imageBindings[i].role);
                    }
                }
            }

            if (_textBindings != null)
            {
                for (int i = 0; i < _textBindings.Length; i++)
                {
                    if (_textBindings[i].targetText != null)
                    {
                        _textBindings[i].targetText.color = manager.GetColor(_textBindings[i].role);
                    }
                }
            }
        }
    }
}
