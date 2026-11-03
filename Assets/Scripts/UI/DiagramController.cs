using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace UI
{
    public class DiagramController : MonoBehaviour
    {
        [Header("Containers & Prefabs")]
        [SerializeField] private RectTransform _diagramContent;
        [SerializeField] private GameObject _classNodePrefab;
        [SerializeField] private GameObject _associationLinePrefab;

        [Header("Feedback Toast (Optional)")]
        [SerializeField] private TMP_Text _feedbackText;
        [SerializeField] private GameObject _feedbackContainer;
        private Coroutine _feedbackCoroutine;

        private IReadOnlyClassDiagram _diagram;
        private IDiagramActions _diagramActions;

        private readonly Dictionary<ClassNode, ClassNodeUI> _activeNodeUIs = new Dictionary<ClassNode, ClassNodeUI>();
        private readonly List<AssociationLineUI> _activeLineUIs = new List<AssociationLineUI>();

        private ClassNodeUI _selectedNodeUI;
        private DiagramBackgroundHandler _backgroundClickHandler;
        private bool _isAssociationMode;
        private Vector2 _nextSpawnPosition = new Vector2(1920f, -1080f);

        public IReadOnlyDictionary<string, ClassNodeUI> ActiveNodes => 
            _activeNodeUIs.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
        public IReadOnlyDictionary<ClassNode, ClassNodeUI> ActiveNodesByNode => _activeNodeUIs;
        public ClassNodeUI SelectedNode => _selectedNodeUI;
        public bool IsAssociationMode => _isAssociationMode;

        public event Action<ClassNode> OnNodeSelected;
        public event Action<string> OnFeedbackMessage;
        public event Action<string> OnAssociationModeHint;

        public void Initialize(IReadOnlyClassDiagram diagram, IDiagramActions diagramActions)
        {
            UnsubscribeFromEvents();

            _diagram = diagram ?? throw new ArgumentNullException(nameof(diagram));
            _diagramActions = diagramActions ?? throw new ArgumentNullException(nameof(diagramActions));

            if (_diagramContent != null)
            {
                _diagramContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 3840f);
                _diagramContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 2160f);

                if (_diagramContent.parent is RectTransform parentRect)
                {
                    float parentWidth = parentRect.rect.width;
                    float parentHeight = parentRect.rect.height;

                    if (parentWidth <= 0f) parentWidth = 1920f;
                    if (parentHeight <= 0f) parentHeight = 1080f;

                    float centerX = (parentWidth - 3840f) / 2f;
                    float centerY = (2160f - parentHeight) / 2f;
                    _diagramContent.anchoredPosition = new Vector2(centerX, centerY);
                }

                _nextSpawnPosition = new Vector2(1920f, -1080f);
            }

            SetupBackgroundClick();

            _diagramActions.OnDiagramChanged += HandleDiagramChanged;
            _diagramActions.OnClassSelected += HandleClassSelected;
            _diagramActions.OnAssociationModeChanged += HandleAssociationModeChanged;
            _diagramActions.OnActionFailed += HandleActionFailed;

            _isAssociationMode = _diagramActions.IsAssociationMode;
            _selectedNodeUI = null;

            RenderDiagram();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            ClearDiagramUI();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;

            if (_isAssociationMode)
            {
                bool escapePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;

                if (escapePressed)
                {
                    SetAssociationMode(false);
                    _diagramActions?.ClearSelection();
                    return;
                }
            }

            if (_diagramActions == null || !_diagramActions.CanModifyDiagram) return;
            if (IsTypingInInputField()) return;

            if (_diagramActions.SelectedClass != null)
            {
                bool deletePressed = keyboard != null &&
                    (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame);

                if (deletePressed)
                {
                    var selected = _diagramActions.SelectedClass;
                    if (!selected.IsLocked)
                    {
                        _diagramActions.RemoveClass(selected);
                    }
                }
            }
        }

        private static bool IsTypingInInputField()
        {
            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected == null) return false;

            return selected.GetComponent<TMP_InputField>() != null
                || selected.GetComponent<UnityEngine.UI.InputField>() != null;
        }

        private void UnsubscribeFromEvents()
        {
            if (_diagramActions != null)
            {
                _diagramActions.OnDiagramChanged -= HandleDiagramChanged;
                _diagramActions.OnClassSelected -= HandleClassSelected;
                _diagramActions.OnAssociationModeChanged -= HandleAssociationModeChanged;
                _diagramActions.OnActionFailed -= HandleActionFailed;
            }

            if (_backgroundClickHandler != null)
            {
                _backgroundClickHandler.OnBackgroundClicked -= HandleBackgroundClicked;
            }
        }

        private void HandleDiagramChanged()
        {
            RenderDiagram();
        }

        private void HandleClassSelected(ClassNode selectedNode)
        {
            if (_selectedNodeUI != null)
            {
                _selectedNodeUI.SetSelectionState(NodeSelectionState.None);
                _selectedNodeUI = null;
            }

            if (selectedNode != null && _activeNodeUIs.TryGetValue(selectedNode, out var nodeUI))
            {
                _selectedNodeUI = nodeUI;
                _selectedNodeUI.SetSelectionState(NodeSelectionState.Selected);
            }

            OnNodeSelected?.Invoke(selectedNode);
        }

        private void HandleAssociationModeChanged(bool active)
        {
            _isAssociationMode = active;
            // On entering association mode, clear any normal selection so the player
            // explicitly picks an origin (Bug 3a).
            if (active && _selectedNodeUI != null && _selectedNodeUI.SelectionState != NodeSelectionState.AssociationOrigin)
            {
                ClearSelectionState();
            }
            else if (!active && _selectedNodeUI != null)
            {
                ClearSelectionUI();
            }
            OnAssociationModeHint?.Invoke(active ? "Selecione a classe de origem" : null);
        }

        private void SetupBackgroundClick()
        {
            if (_diagramContent == null) return;

            var graphic = _diagramContent.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic == null)
            {
                var image = _diagramContent.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.color = new Color(0.96f, 0.90f, 0.60f, 1f);
                image.raycastTarget = true;
            }
            else
            {
                graphic.raycastTarget = true;
            }

            if (_backgroundClickHandler == null)
            {
                _backgroundClickHandler = _diagramContent.GetComponent<DiagramBackgroundHandler>()
                    ?? _diagramContent.gameObject.AddComponent<DiagramBackgroundHandler>();
            }

            _backgroundClickHandler.OnBackgroundClicked -= HandleBackgroundClicked;
            _backgroundClickHandler.OnBackgroundClicked += HandleBackgroundClicked;
        }

        public void MoveDiagramUp() => _backgroundClickHandler?.PanBy(new Vector2(0f, -200f));
        public void MoveDiagramDown() => _backgroundClickHandler?.PanBy(new Vector2(0f, 200f));
        public void MoveDiagramLeft() => _backgroundClickHandler?.PanBy(new Vector2(200f, 0f));
        public void MoveDiagramRight() => _backgroundClickHandler?.PanBy(new Vector2(-200f, 0f));

        /// <summary>Clicking the empty diagram area cancels an in-progress association and clears selection.</summary>
        private void HandleBackgroundClicked()
        {
            if (_isAssociationMode)
            {
                SetAssociationMode(false);
            }

            ClearSelectionState();
        }

        private void ClearSelectionUI()
        {
            if (_selectedNodeUI != null)
            {
                _selectedNodeUI.SetSelectionState(NodeSelectionState.None);
                _selectedNodeUI = null;
            }
        }

        private void ClearSelectionState()
        {
            ClearSelectionUI();

            if (_diagramActions != null && _diagramActions.SelectedClass != null)
            {
                _diagramActions.ClearSelection();
            }
        }

        public void ShowFeedback(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            HandleActionFailed(message);
        }

        public void SetAssociationMode(bool active)
        {
            bool wasActive = _isAssociationMode;
            _isAssociationMode = active;

            if (_diagramActions != null && _diagramActions.IsAssociationMode != active)
            {
                _diagramActions.SetAssociationMode(active);
            }

            if (active)
            {
                if (!wasActive) ClearSelectionState();
            }
            else
            {
                ClearSelectionUI();
            }
        }

        public bool TryCreateClass(string className)
        {
            if (_diagramActions == null) return false;

            return _diagramActions.TryCreateClass(className, out _);
        }

        public bool TryRenameClass(ClassNode node, string newName)
        {
            if (_diagramActions == null || node == null) return false;
            return _diagramActions.RenameClass(node, newName);
        }

        public bool TryAddMethodToClass(ClassNode node, MethodData method)
        {
            if (_diagramActions == null || node == null || method == null) return false;
            return _diagramActions.AddMethodToClass(node, method);
        }

        public void RenderDiagram()
        {
            if (_diagram == null || _diagramContent == null) return;
            
            var currentClasses = new HashSet<ClassNode>(_diagram.Classes);
            
            var toRemove = _activeNodeUIs.Keys.Where(node => !currentClasses.Contains(node)).ToList();
            foreach (var node in toRemove)
            {
                var nodeUI = _activeNodeUIs[node];
                UnsubscribeNodeUI(nodeUI);
                DestroyNodeGameObject(nodeUI.gameObject);
                _activeNodeUIs.Remove(node);
            }
            
            foreach (var classNode in _diagram.Classes)
            {
                if (!_activeNodeUIs.TryGetValue(classNode, out var nodeUI))
                {
                    nodeUI = InstantiateNodeUI(classNode);
                    _activeNodeUIs[classNode] = nodeUI;
                }
                nodeUI.Bind(classNode);
                bool isSelected = _diagramActions != null && _diagramActions.SelectedClass == classNode;
                nodeUI.SetSelectionState(isSelected ? NodeSelectionState.Selected : NodeSelectionState.None);
                if (isSelected)
                {
                    _selectedNodeUI = nodeUI;
                }
            }
            
            RenderRelations();
        }

        private ClassNodeUI InstantiateNodeUI(ClassNode classNode)
        {
            GameObject nodeGo;
            if (_classNodePrefab != null)
            {
                nodeGo = Instantiate(_classNodePrefab, _diagramContent);
            }
            else
            {
                nodeGo = new GameObject($"ClassNode_{classNode.Name}", typeof(RectTransform));
                nodeGo.transform.SetParent(_diagramContent, false);
            }

            var nodeUI = nodeGo.GetComponent<ClassNodeUI>() ?? nodeGo.AddComponent<ClassNodeUI>();

            nodeUI.RectTransform.anchorMin = new Vector2(0f, 1f);
            nodeUI.RectTransform.anchorMax = new Vector2(0f, 1f);
            nodeUI.RectTransform.pivot = new Vector2(0.5f, 0.5f);

            _nextSpawnPosition.x = Mathf.Clamp(_nextSpawnPosition.x, 300f, 3500f);
            _nextSpawnPosition.y = Mathf.Clamp(_nextSpawnPosition.y, -1900f, -300f);

            nodeUI.RectTransform.anchoredPosition = _nextSpawnPosition;
            _nextSpawnPosition += new Vector2(200f, -100f);

            if (_nextSpawnPosition.x > 3300f)
            {
                _nextSpawnPosition = new Vector2(800f, _nextSpawnPosition.y - 200f);
            }

            if (_nextSpawnPosition.y < -1900f)
            {
                _nextSpawnPosition = new Vector2(1920f, -600f);
            }

            nodeUI.OnNodeClicked += HandleNodeClicked;
            nodeUI.OnRenameRequested += HandleRenameRequested;
            nodeUI.OnMethodDropped += HandleMethodDropped;
            nodeUI.OnRemoveMethodRequested += HandleRemoveMethodRequested;
            nodeUI.OnDeleteRequested += HandleDeleteRequested;
            nodeUI.SetCanModifyProvider(() => _diagramActions != null && _diagramActions.CanModifyDiagram);

            return nodeUI;
        }

        private void RenderRelations()
        {
            foreach (var line in _activeLineUIs)
            {
                if (line != null)
                {
                    line.OnLineClicked -= HandleLineClicked;
                    DestroyNodeGameObject(line.gameObject);
                }
            }
            _activeLineUIs.Clear();

            if (_diagram.Relations == null) return;

            foreach (var relation in _diagram.Relations)
            {
                if (relation.Origin == null || relation.Destination == null) continue;

                if (_activeNodeUIs.TryGetValue(relation.Origin, out var originUI) &&
                    _activeNodeUIs.TryGetValue(relation.Destination, out var destUI))
                {
                    var lineUI = CreateLineUI();
                    lineUI.Connect(originUI.RectTransform, destUI.RectTransform, relation.Origin, relation.Destination);
                    lineUI.OnLineClicked += HandleLineClicked;
                    _activeLineUIs.Add(lineUI);
                }
            }
        }

        private AssociationLineUI CreateLineUI()
        {
            GameObject lineGo;
            if (_associationLinePrefab != null)
            {
                lineGo = Instantiate(_associationLinePrefab, _diagramContent);
            }
            else
            {
                lineGo = new GameObject("AssociationLine", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                lineGo.transform.SetParent(_diagramContent, false);
                lineGo.transform.SetAsFirstSibling();
            }

            return lineGo.GetComponent<AssociationLineUI>() ?? lineGo.AddComponent<AssociationLineUI>();
        }

        private void HandleMethodDropped(ClassNodeUI nodeUI, MethodData method)
        {
            if (_diagramActions == null || nodeUI == null || nodeUI.Node == null || method == null) return;
            _diagramActions.AddMethodToClass(nodeUI.Node, method);
        }

        private void HandleRemoveMethodRequested(ClassNodeUI nodeUI, MethodData method)
        {
            if (_diagramActions == null || nodeUI == null || nodeUI.Node == null || method == null) return;
            _diagramActions.RemoveMethodFromClass(nodeUI.Node, method);
        }

        private void HandleDeleteRequested(ClassNodeUI nodeUI)
        {
            if (_diagramActions == null || nodeUI == null || nodeUI.Node == null) return;
            if (nodeUI.Node.IsLocked) return;

            _diagramActions.RemoveClass(nodeUI.Node);
        }

        private void HandleRenameRequested(ClassNodeUI nodeUI, string newName)
        {
            if (_diagramActions == null || nodeUI == null || nodeUI.Node == null) return;

            bool renamed = _diagramActions.RenameClass(nodeUI.Node, newName);
            if (!renamed)
            {
                nodeUI.UpdateDisplay();
            }
        }

        private void HandleLineClicked(AssociationLineUI lineUI)
        {
            if (_isAssociationMode) return;
            if (lineUI == null || lineUI.OriginNode == null || lineUI.DestinationNode == null) return;
            _diagramActions?.RemoveRelation(lineUI.OriginNode, lineUI.DestinationNode);
        }

        private void HandleNodeClicked(ClassNodeUI clickedNodeUI)
        {
            if (clickedNodeUI == null) return;

            if (_isAssociationMode)
            {
                if (_selectedNodeUI == null)
                {
                    _selectedNodeUI = clickedNodeUI;
                    _selectedNodeUI.SetSelectionState(NodeSelectionState.AssociationOrigin);
                    OnAssociationModeHint?.Invoke("Selecione a classe de destino");
                }
                else if (_selectedNodeUI == clickedNodeUI)
                {
                    _selectedNodeUI.SetSelectionState(NodeSelectionState.None);
                    _selectedNodeUI = null;
                    OnAssociationModeHint?.Invoke("Selecione a classe de origem");
                }
                else
                {
                    var origin = _selectedNodeUI.Node;
                    var destination = clickedNodeUI.Node;

                    _diagramActions.AddRelation(origin, destination, RelationType.Association);
                    SetAssociationMode(false);
                }
            }
            else
            {
                if (_selectedNodeUI == clickedNodeUI)
                {
                    _diagramActions.ClearSelection();
                }
                else
                {
                    _diagramActions.SelectClass(clickedNodeUI.Node);
                }
            }
        }

        private void ClearDiagramUI()
        {
            foreach (var nodeUI in _activeNodeUIs.Values)
            {
                if (nodeUI != null)
                {
                    UnsubscribeNodeUI(nodeUI);
                    DestroyNodeGameObject(nodeUI.gameObject);
                }
            }
            _activeNodeUIs.Clear();

            foreach (var line in _activeLineUIs)
            {
                if (line != null)
                {
                    line.OnLineClicked -= HandleLineClicked;
                    DestroyNodeGameObject(line.gameObject);
                }
            }
            _activeLineUIs.Clear();
        }

        private void UnsubscribeNodeUI(ClassNodeUI nodeUI)
        {
            if (nodeUI == null) return;

            nodeUI.OnNodeClicked -= HandleNodeClicked;
            nodeUI.OnRenameRequested -= HandleRenameRequested;
            nodeUI.OnMethodDropped -= HandleMethodDropped;
            nodeUI.OnRemoveMethodRequested -= HandleRemoveMethodRequested;
            nodeUI.OnDeleteRequested -= HandleDeleteRequested;
        }

        private void DestroyNodeGameObject(GameObject go)
        {
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        private void HandleActionFailed(string message)
        {
            Debug.LogWarning($"[DiagramController] {message}");
            OnFeedbackMessage?.Invoke(message);

            if (_feedbackText != null)
            {
                if (_feedbackCoroutine != null)
                {
                    StopCoroutine(_feedbackCoroutine);
                }
                _feedbackCoroutine = StartCoroutine(ShowFeedbackRoutine(message));
            }
        }

        private IEnumerator ShowFeedbackRoutine(string message)
        {
            _feedbackText.text = message;
            if (_feedbackContainer != null) _feedbackContainer.SetActive(true);
            else _feedbackText.gameObject.SetActive(true);

            yield return new WaitForSeconds(2.5f);

            if (_feedbackContainer != null) _feedbackContainer.SetActive(false);
            else _feedbackText.gameObject.SetActive(false);
            _feedbackCoroutine = null;
        }
    }
}
