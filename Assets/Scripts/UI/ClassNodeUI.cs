using System;
using Core.Diagram;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    public enum NodeSelectionState { None, Selected, AssociationOrigin }

    public class ClassNodeUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IDropHandler
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text _classNameText;
        [SerializeField] private TMP_InputField _classNameInput;
        [SerializeField] private RectTransform _headerDragArea;
        [SerializeField] private Transform _methodsContainer;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _lockIndicator;

        [Header("Colors / Visuals")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _lockedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        [SerializeField] private Color _selectedColor = new Color(0.6f, 0.8f, 1f, 1f);
        [SerializeField] private Color _associationOriginColor = new Color(1f, 0.85f, 0.2f, 1f); // gold

        private RectTransform _rectTransform;
        private Canvas _canvas;
        private NodeSelectionState _selectionState;

        public ClassNode Node { get; private set; }
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        public event Action<ClassNodeUI> OnNodeClicked;
        public event Action<ClassNodeUI, string> OnRenameRequested;
        public event Action<ClassNodeUI, MethodData> OnRemoveMethodRequested;
        public event Action<ClassNodeUI, MethodData> OnMethodDropped;

        public bool CanRename => Node != null && !Node.IsLocked;
        public NodeSelectionState SelectionState => _selectionState;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();

            if (_classNameText == null)
            {
                var textObj = transform.Find("Header_DragArea/Text_ClassName");
                if (textObj != null) _classNameText = textObj.GetComponent<TMP_Text>();
            }

            if (_classNameInput == null)
            {
                var inputObj = transform.Find("Header_DragArea/Input_ClassName");
                if (inputObj != null) _classNameInput = inputObj.GetComponent<TMP_InputField>();
            }

            if (_classNameInput != null)
            {
                _classNameInput.onEndEdit.RemoveListener(HandleInputEndEdit);
                _classNameInput.onEndEdit.AddListener(HandleInputEndEdit);
            }

            if (_methodsContainer == null)
            {
                var containerObj = transform.Find("Methods_Container");
                if (containerObj != null) _methodsContainer = containerObj;
            }

            if (_backgroundImage == null)
            {
                _backgroundImage = GetComponent<Image>();
            }

            if (_lockIndicator == null)
            {
                var lockObj = transform.Find("Header_DragArea/Lock_Indicator");
                if (lockObj != null) _lockIndicator = lockObj.GetComponent<Image>();
            }
        }

        private void OnDestroy()
        {
            if (_classNameInput != null)
            {
                _classNameInput.onEndEdit.RemoveListener(HandleInputEndEdit);
            }
        }

        public void Bind(ClassNode node)
        {
            Node = node;
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            if (Node == null) return;

            if (_classNameText != null)
            {
                _classNameText.text = Node.Name;
            }

            if (_classNameInput != null)
            {
                _classNameInput.text = Node.Name;
                _classNameInput.interactable = !Node.IsLocked;
            }

            UpdateLockVisuals();
            UpdateMethodsDisplay();
        }

        public void SetSelectionState(NodeSelectionState state)
        {
            _selectionState = state;
            UpdateLockVisuals();
        }

        private void UpdateLockVisuals()
        {
            if (_backgroundImage == null) return;

            switch (_selectionState)
            {
                case NodeSelectionState.AssociationOrigin:
                    _backgroundImage.color = _associationOriginColor;
                    break;
                case NodeSelectionState.Selected:
                    _backgroundImage.color = _selectedColor;
                    break;
                default:
                    _backgroundImage.color = (Node != null && Node.IsLocked) ? _lockedColor : _normalColor;
                    break;
            }

            if (_lockIndicator != null)
            {
                _lockIndicator.gameObject.SetActive(Node != null && Node.IsLocked);
            }
        }

        private void UpdateMethodsDisplay()
        {
            if (_methodsContainer == null || Node == null) return;

            for (int i = _methodsContainer.childCount - 1; i >= 0; i--)
            {
                var child = _methodsContainer.GetChild(i);
                DestroyImmediate(child.gameObject);
            }

            foreach (var method in Node.Methods)
            {
                var methodGo = new GameObject($"Method_{method.Name}", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(Button));
                methodGo.transform.SetParent(_methodsContainer, false);
                var text = methodGo.GetComponent<TextMeshProUGUI>();
                text.text = $"+ {method.Name}()";
                text.fontSize = 14;
                text.color = Node.IsLocked ? Color.gray : Color.black;

                var btn = methodGo.GetComponent<Button>();
                btn.interactable = !Node.IsLocked;
                var capturedMethod = method;
                btn.onClick.AddListener(() =>
                {
                    if (Node != null && !Node.IsLocked)
                    {
                        OnRemoveMethodRequested?.Invoke(this, capturedMethod);
                    }
                });
            }
        }

        public void BeginEditName()
        {
            if (!CanRename) return;

            if (_classNameInput != null)
            {
                _classNameInput.gameObject.SetActive(true);
                _classNameInput.text = Node.Name;

                if (_classNameText != null)
                {
                    _classNameText.gameObject.SetActive(false);
                }

                _classNameInput.Select();
                _classNameInput.ActivateInputField();
            }
        }

        public void EndEditName()
        {
            if (_classNameInput != null && _classNameText != null)
            {
                _classNameInput.gameObject.SetActive(false);
                _classNameText.gameObject.SetActive(true);
            }
            UpdateDisplay();
        }

        private void HandleInputEndEdit(string newName)
        {
            if (Node != null && !string.IsNullOrWhiteSpace(newName))
            {
                string trimmed = newName.Trim();
                if (!string.Equals(trimmed, Node.Name, StringComparison.Ordinal))
                {
                    OnRenameRequested?.Invoke(this, trimmed);
                }
            }

            EndEditName();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.clickCount == 2 && CanRename && _selectionState != NodeSelectionState.AssociationOrigin)
            {
                BeginEditName();
                return;
            }

            OnNodeClicked?.Invoke(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            float scaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;
            RectTransform.anchoredPosition += eventData.delta / scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData == null || Node == null) return;
            var card = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<MethodCardUI>() : null;
            if (card != null && card.Method != null)
            {
                OnMethodDropped?.Invoke(this, card.Method);
            }
        }
    }
}
