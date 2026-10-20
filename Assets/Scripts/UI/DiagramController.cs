using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using TMPro;
using UnityEngine;

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
        private bool _isAssociationMode;
        private Vector2 _nextSpawnPosition = new Vector2(100f, -100f);

        public IReadOnlyDictionary<string, ClassNodeUI> ActiveNodes => 
            _activeNodeUIs.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
        public IReadOnlyDictionary<ClassNode, ClassNodeUI> ActiveNodesByNode => _activeNodeUIs;
        public ClassNodeUI SelectedNode => _selectedNodeUI;
        public bool IsAssociationMode => _isAssociationMode;

        public event Action<ClassNode> OnNodeSelected;
        public event Action<Relation> OnAssociationCreated;
        public event Action<string> OnFeedbackMessage;

        public void Initialize(IReadOnlyClassDiagram diagram, IDiagramActions diagramActions)
        {
            UnsubscribeFromEvents();

            _diagram = diagram ?? throw new ArgumentNullException(nameof(diagram));
            _diagramActions = diagramActions ?? throw new ArgumentNullException(nameof(diagramActions));

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

        private void UnsubscribeFromEvents()
        {
            if (_diagramActions != null)
            {
                _diagramActions.OnDiagramChanged -= HandleDiagramChanged;
                _diagramActions.OnClassSelected -= HandleClassSelected;
                _diagramActions.OnAssociationModeChanged -= HandleAssociationModeChanged;
                _diagramActions.OnActionFailed -= HandleActionFailed;
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
                _selectedNodeUI.SetSelected(false);
                _selectedNodeUI = null;
            }

            if (selectedNode != null && _activeNodeUIs.TryGetValue(selectedNode, out var nodeUI))
            {
                _selectedNodeUI = nodeUI;
                _selectedNodeUI.SetSelected(true);
            }

            OnNodeSelected?.Invoke(selectedNode);
        }

        private void HandleAssociationModeChanged(bool active)
        {
            _isAssociationMode = active;
            if (!active && _selectedNodeUI != null)
            {
                _selectedNodeUI.SetSelected(false);
                _selectedNodeUI = null;
            }
        }

        public void SetAssociationMode(bool active)
        {
            _isAssociationMode = active;
            if (_diagramActions != null && _diagramActions.IsAssociationMode != active)
            {
                _diagramActions.SetAssociationMode(active);
            }

            if (!active && _selectedNodeUI != null)
            {
                _selectedNodeUI.SetSelected(false);
                _selectedNodeUI = null;
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

        public bool TryRemoveClass(ClassNode node)
        {
            if (_diagramActions == null || node == null) return false;
            return _diagramActions.RemoveClass(node);
        }

        public void RenderDiagram()
        {
            if (_diagram == null || _diagramContent == null) return;
            
            var currentClasses = new HashSet<ClassNode>(_diagram.Classes);
            
            var toRemove = _activeNodeUIs.Keys.Where(node => !currentClasses.Contains(node)).ToList();
            foreach (var node in toRemove)
            {
                var nodeUI = _activeNodeUIs[node];
                nodeUI.OnNodeClicked -= HandleNodeClicked;
                nodeUI.OnRenameRequested -= HandleRenameRequested;
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
                nodeUI.SetSelected(isSelected);
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
            nodeUI.RectTransform.anchoredPosition = _nextSpawnPosition;
            _nextSpawnPosition += new Vector2(160f, -40f);

            if (_nextSpawnPosition.x > 600f)
            {
                _nextSpawnPosition = new Vector2(100f, _nextSpawnPosition.y - 120f);
            }

            nodeUI.OnNodeClicked += HandleNodeClicked;
            nodeUI.OnRenameRequested += HandleRenameRequested;
            return nodeUI;
        }

        private void RenderRelations()
        {
            foreach (var line in _activeLineUIs)
            {
                if (line != null)
                {
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
                    lineUI.Connect(originUI.RectTransform, destUI.RectTransform);
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

        private void HandleRenameRequested(ClassNodeUI nodeUI, string newName)
        {
            if (_diagramActions == null || nodeUI == null || nodeUI.Node == null) return;

            bool renamed = _diagramActions.RenameClass(nodeUI.Node, newName);
            if (!renamed)
            {
                nodeUI.UpdateDisplay();
            }
        }

        private void HandleNodeClicked(ClassNodeUI clickedNodeUI)
        {
            if (clickedNodeUI == null) return;

            if (_isAssociationMode)
            {
                if (_selectedNodeUI == null)
                {
                    _selectedNodeUI = clickedNodeUI;
                    _selectedNodeUI.SetSelected(true);
                }
                else if (_selectedNodeUI == clickedNodeUI)
                {
                    _selectedNodeUI.SetSelected(false);
                    _selectedNodeUI = null;
                }
                else
                {
                    var origin = _selectedNodeUI.Node;
                    var destination = clickedNodeUI.Node;

                    if (_diagramActions.AddRelation(origin, destination, RelationType.Association))
                    {
                        OnAssociationCreated?.Invoke(new Relation(origin, destination, RelationType.Association));
                    }

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
                    nodeUI.OnNodeClicked -= HandleNodeClicked;
                    nodeUI.OnRenameRequested -= HandleRenameRequested;
                    DestroyNodeGameObject(nodeUI.gameObject);
                }
            }
            _activeNodeUIs.Clear();

            foreach (var line in _activeLineUIs)
            {
                if (line != null)
                {
                    DestroyNodeGameObject(line.gameObject);
                }
            }
            _activeLineUIs.Clear();
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
