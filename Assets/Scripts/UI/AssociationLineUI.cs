using System;
using Core.Diagram;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    [RequireComponent(typeof(Image))]
    public class AssociationLineUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private float _thickness = 3f;
        [SerializeField] private Color _lineColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color _hoverColor = new Color(0.8f, 0.8f, 0.8f, 1f);

        private RectTransform _rectTransform;
        private Image _image;
        private RectTransform _origin;
        private RectTransform _destination;

        public RectTransform Origin => _origin;
        public RectTransform Destination => _destination;
        public ClassNode OriginNode { get; private set; }
        public ClassNode DestinationNode { get; private set; }

        public event Action<AssociationLineUI> OnLineClicked;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
            if (_image != null)
            {
                _image.color = _lineColor;
                _image.raycastTarget = true;
                _image.raycastPadding = new Vector4(0f, -8f, 0f, -8f);
            }
            if (_rectTransform != null)
            {
                _rectTransform.pivot = new Vector2(0f, 0.5f);
            }
        }

        public void Connect(RectTransform origin, RectTransform destination, ClassNode originNode = null, ClassNode destinationNode = null)
        {
            _origin = origin;
            _destination = destination;
            OriginNode = originNode;
            DestinationNode = destinationNode;
            UpdateLine();
        }

        private void LateUpdate()
        {
            if (_origin != null && _destination != null)
            {
                UpdateLine();
            }
        }

        public void UpdateLine()
        {
            if (_origin == null || _destination == null) return;
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();

            var parent = _rectTransform.parent;
            Vector3 startLocal = parent != null ? parent.InverseTransformPoint(_origin.position) : _origin.localPosition;
            Vector3 endLocal = parent != null ? parent.InverseTransformPoint(_destination.position) : _destination.localPosition;
            Vector3 diff = endLocal - startLocal;

            float distance = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            _rectTransform.localPosition = startLocal;
            _rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            _rectTransform.sizeDelta = new Vector2(distance, _thickness);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_image != null)
            {
                _image.color = _hoverColor;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_image != null)
            {
                _image.color = _lineColor;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
            {
                OnLineClicked?.Invoke(this);
            }
        }
    }
}
