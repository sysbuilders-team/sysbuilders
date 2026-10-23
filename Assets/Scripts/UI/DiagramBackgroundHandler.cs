using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    public class DiagramBackgroundHandler : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public event Action OnBackgroundClicked;
        public event Action<Vector2> OnBackgroundDragged;

        [SerializeField] private RectTransform _targetContentToMove;
        private Canvas _canvas;
        private bool _isDragging;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_targetContentToMove == null)
            {
                _targetContentToMove = transform as RectTransform;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            if (_isDragging) return;

            OnBackgroundClicked?.Invoke();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            _isDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;

            float scaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;
            Vector2 scaledDelta = eventData.delta / scaleFactor;

            PanBy(scaledDelta);
            OnBackgroundDragged?.Invoke(scaledDelta);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
        }

        /// <summary>
        /// Move o diagrama por um deslocamento específico (usado tanto pelo arrasto quanto pelos botões).
        /// </summary>
        public void PanBy(Vector2 delta)
        {
            if (_targetContentToMove != null)
            {
                _targetContentToMove.anchoredPosition += delta;
            }
        }
    }
}
