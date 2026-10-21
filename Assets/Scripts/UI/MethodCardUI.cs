using System;
using Core.Diagram;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    public class MethodCardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private TMP_Text _methodNameText;
        [SerializeField] private Button _button;

        private Canvas _canvas;
        private Canvas _rootCanvas;
        private RectTransform _rootCanvasRect;
        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private Transform _originalParent;
        private int _originalSiblingIndex;
        private Vector2 _originalSizeDelta;
        private Vector2 _originalAnchorMin;
        private Vector2 _originalAnchorMax;
        private Vector2 _originalPivot;
        private Vector2 _dragOffset;
        private bool _isDragging;

        public MethodData Method { get; private set; }
        public bool IsCurrentTurn { get; private set; }
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());
        public event Action<MethodData> OnCardClicked;

        /// <summary>
        /// Fallback factory used when no prefab is assigned in the Inspector.
        /// Keeps procedural UI hierarchy construction inside MethodCardUI rather than ToolboxController.
        /// </summary>
        public static GameObject CreateDefault(Transform parent, MethodData method)
        {
            string cardName = method != null ? $"MethodCard_{method.Name}" : "MethodCard";
            var cardGo = new GameObject(cardName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            cardGo.transform.SetParent(parent, false);

            var rt = cardGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220f, 36f);

            var layout = cardGo.GetComponent<LayoutElement>();
            layout.minHeight = 36f;
            layout.preferredHeight = 36f;
            layout.preferredWidth = 220f;
            layout.flexibleWidth = 1f;

            var image = cardGo.GetComponent<Image>();
            image.color = new Color(0.92f, 0.94f, 0.98f, 1f);

            var btn = cardGo.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.92f, 0.94f, 0.98f, 1f);
            colors.highlightedColor = new Color(0.82f, 0.88f, 1f, 1f);
            colors.pressedColor = new Color(0.72f, 0.80f, 0.95f, 1f);
            btn.colors = colors;

            var textGo = new GameObject("Text_MethodName", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(cardGo.transform, false);

            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 0f);
            textRt.offsetMax = new Vector2(-10f, 0f);

            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = method != null ? $"+ {method.Name}()" : string.Empty;
            tmp.fontSize = 14;
            tmp.color = new Color(0.12f, 0.12f, 0.18f, 1f);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            return cardGo;
        }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

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

        public void Setup(MethodData method, bool isCurrentTurn = true)
        {
            Method = method;
            IsCurrentTurn = isCurrentTurn;

            if (_methodNameText == null)
            {
                _methodNameText = GetComponentInChildren<TMP_Text>();
            }

            if (_methodNameText != null)
            {
                _methodNameText.text = method != null ? $"+ {method.Name}()" : string.Empty;
                _methodNameText.alpha = isCurrentTurn ? 1f : 0.45f;
            }
        }

        private void HandleClick()
        {
            if (!_isDragging && Method != null)
            {
                OnCardClicked?.Invoke(Method);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            _rootCanvas = _canvas != null ? _canvas.rootCanvas : GetComponentInParent<Canvas>()?.rootCanvas;
            _rootCanvasRect = _rootCanvas != null ? _rootCanvas.transform as RectTransform : null;

            _isDragging = true;
            _originalParent = transform.parent;
            _originalSiblingIndex = transform.GetSiblingIndex();
            _originalSizeDelta = RectTransform.sizeDelta;
            _originalAnchorMin = RectTransform.anchorMin;
            _originalAnchorMax = RectTransform.anchorMax;
            _originalPivot = RectTransform.pivot;

            Vector2 currentSize = RectTransform.rect.size;
            if (currentSize.x < 50f) currentSize.x = 220f;
            if (currentSize.y < 20f) currentSize.y = 36f;

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.alpha = 0.75f;
            }

            if (_rootCanvas != null && _rootCanvasRect != null)
            {
                Vector3 worldPos = transform.position;
                transform.SetParent(_rootCanvas.transform, false);
                transform.SetAsLastSibling();
                transform.localScale = Vector3.one;

                RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                RectTransform.pivot = new Vector2(0.5f, 0.5f);
                RectTransform.sizeDelta = currentSize;

                Camera cam = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : (eventData.pressEventCamera ?? _rootCanvas.worldCamera);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootCanvasRect, eventData.position, cam, out Vector2 localPointer))
                {
                    Vector2 cardLocalPos = _rootCanvasRect.InverseTransformPoint(worldPos);
                    _dragOffset = cardLocalPos - localPointer;
                    RectTransform.localPosition = cardLocalPos;
                }
                else
                {
                    _dragOffset = Vector2.zero;
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_rootCanvasRect != null && _rootCanvas != null)
            {
                Camera cam = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : (eventData.pressEventCamera ?? _rootCanvas.worldCamera);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootCanvasRect, eventData.position, cam, out Vector2 localPointer))
                {
                    RectTransform.localPosition = localPointer + _dragOffset;
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.alpha = 1f;
            }

            if (_originalParent != null && transform.parent != _originalParent)
            {
                transform.SetParent(_originalParent, false);
                transform.SetSiblingIndex(_originalSiblingIndex);
                transform.localScale = Vector3.one;
                RectTransform.anchorMin = _originalAnchorMin;
                RectTransform.anchorMax = _originalAnchorMax;
                RectTransform.pivot = _originalPivot;
                RectTransform.sizeDelta = _originalSizeDelta;

                if (_originalParent is RectTransform parentRect)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
                }
            }
        }
    }
}
