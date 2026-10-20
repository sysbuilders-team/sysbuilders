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
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());
        public event Action<MethodData> OnCardClicked;

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

        public void Setup(MethodData method)
        {
            Method = method;

            if (_methodNameText == null)
            {
                _methodNameText = GetComponentInChildren<TMP_Text>();
            }

            if (_methodNameText != null)
            {
                _methodNameText.text = method != null ? $"+ {method.Name}()" : string.Empty;
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
