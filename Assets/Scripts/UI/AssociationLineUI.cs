using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [RequireComponent(typeof(Image))]
    public class AssociationLineUI : MonoBehaviour
    {
        [SerializeField] private float _thickness = 3f;
        [SerializeField] private Color _lineColor = new Color(0.2f, 0.2f, 0.2f, 1f);

        private RectTransform _rectTransform;
        private Image _image;
        private RectTransform _origin;
        private RectTransform _destination;

        public RectTransform Origin => _origin;
        public RectTransform Destination => _destination;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
            if (_image != null)
            {
                _image.color = _lineColor;
            }
            if (_rectTransform != null)
            {
                _rectTransform.pivot = new Vector2(0f, 0.5f);
            }
        }

        public void Connect(RectTransform origin, RectTransform destination)
        {
            _origin = origin;
            _destination = destination;
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

            Vector3 startPos = _origin.position;
            Vector3 endPos = _destination.position;
            Vector3 diff = endPos - startPos;

            float distance = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            _rectTransform.position = startPos;
            _rectTransform.rotation = Quaternion.Euler(0, 0, angle);
            _rectTransform.sizeDelta = new Vector2(distance, _thickness);
        }
    }
}
