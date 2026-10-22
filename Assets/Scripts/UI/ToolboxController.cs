using System;
using System.Linq;
using System.Collections.Generic;
using Core.Diagram;
using Core.Requirements;
using Core.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class ToolboxController : MonoBehaviour
    {
        [Header("Criar Classe")]
        [SerializeField] private Button _createClassButton;
        [SerializeField] private TMP_Text _createClassButtonText;
        [SerializeField] private TMP_InputField _classNameInputField;

        [Header("Lista de Métodos")]
        [SerializeField] private Transform _methodsContainer;
        [SerializeField] private GameObject _methodCardPrefab;

        [Header("Associação")]
        [SerializeField] private Button _associationButton;
        [SerializeField] private TMP_Text _associationButtonText;

        private IRequirementContext _requirementContext;
        private IDiagramActions _diagramActions;
        private ITurnContext _turnContext;
        private IReadOnlyClassDiagram _diagram;

        private bool _isAssociationModeActive;
        private readonly List<MethodCardUI> _activeCards = new List<MethodCardUI>();

        public int ClassesCreatedThisTurn => _diagramActions?.ClassesCreatedThisTurn ?? 0;
        public int MaxClassesPerTurn => _diagramActions?.MaxClassesPerTurn ?? DiagramEditor.DefaultMaxClassesPerTurn;
        public int TotalClassesCreated => _diagramActions?.TotalClassesCreated ?? 0;
        public int MaxClassesPerMatch => _diagramActions?.MaxClassesPerMatch ?? DiagramEditor.DefaultMaxClassesPerMatch;
        public bool IsAssociationModeActive => _isAssociationModeActive;
        public IReadOnlyList<MethodCardUI> ActiveCards => _activeCards;
        public bool HasPendingMethods => _activeCards.Any(c => c != null && c.IsCurrentTurn);

        private void Awake()
        {
            ResolveMethodsContainer();
            SetupButtons();
        }

        private void ResolveMethodsContainer()
        {
            if (_methodsContainer != null)
            {
                var scrollRect = _methodsContainer.GetComponentInChildren<ScrollRect>();
                if (scrollRect != null && scrollRect.content != null)
                {
                    _methodsContainer = scrollRect.content;
                }
            }
        }

        public void Initialize(IRequirementContext requirementContext, IDiagramActions diagramActions, ITurnContext turnContext, IReadOnlyClassDiagram diagram = null)
        {
            UnsubscribeFromEvents();
            ResolveMethodsContainer();
            SetupButtons();

            _requirementContext = requirementContext ?? throw new ArgumentNullException(nameof(requirementContext));
            _diagramActions = diagramActions ?? throw new ArgumentNullException(nameof(diagramActions));
            _turnContext = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
            _diagram = diagram;

            _requirementContext.OnRequirementChanged += HandleRequirementChanged;
            _turnContext.OnPhaseChange += HandlePhaseChange;
            _diagramActions.OnAssociationModeChanged += HandleAssociationModeChanged;
            _diagramActions.OnDiagramChanged += HandleDiagramChanged;

            _isAssociationModeActive = _diagramActions.IsAssociationMode;

            UpdateAllDisplays();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();

            if (_createClassButton != null)
            {
                _createClassButton.onClick.RemoveListener(HandleCreateClassClicked);
            }

            if (_associationButton != null)
            {
                _associationButton.onClick.RemoveListener(HandleAssociationButtonClicked);
            }

            ClearMethodsList();
        }

        private void UnsubscribeFromEvents()
        {
            if (_requirementContext != null)
            {
                _requirementContext.OnRequirementChanged -= HandleRequirementChanged;
            }

            if (_turnContext != null)
            {
                _turnContext.OnPhaseChange -= HandlePhaseChange;
            }

            if (_diagramActions != null)
            {
                _diagramActions.OnAssociationModeChanged -= HandleAssociationModeChanged;
                _diagramActions.OnDiagramChanged -= HandleDiagramChanged;
            }
        }

        private void SetupButtons()
        {
            if (_createClassButton != null)
            {
                _createClassButton.onClick.RemoveListener(HandleCreateClassClicked);
                _createClassButton.onClick.AddListener(HandleCreateClassClicked);

                if (_createClassButtonText == null)
                {
                    _createClassButtonText = _createClassButton.GetComponentInChildren<TMP_Text>();
                }
            }

            if (_associationButton != null)
            {
                _associationButton.onClick.RemoveListener(HandleAssociationButtonClicked);
                _associationButton.onClick.AddListener(HandleAssociationButtonClicked);

                if (_associationButtonText == null)
                {
                    _associationButtonText = _associationButton.GetComponentInChildren<TMP_Text>();
                }
            }
        }

        private void HandleDiagramChanged()
        {
            UpdateCreateClassButtonDisplay();
            PopulateMethodsList();
        }

        private void HandleRequirementChanged(Requirement newRequirement)
        {
            PopulateMethodsList();
        }

        private void HandlePhaseChange(TurnPhase newPhase)
        {
            if (newPhase != TurnPhase.Build)
            {
                SetAssociationMode(false);
            }

            UpdateButtonsInteractable(newPhase);
        }

        private void HandleAssociationModeChanged(bool active)
        {
            _isAssociationModeActive = active;
            UpdateAssociationButtonDisplay();
        }

        private void HandleCreateClassClicked()
        {
            if (!CanCreateClass()) return;

            if (_diagramActions != null)
            {
                string className = (_classNameInputField != null && !string.IsNullOrWhiteSpace(_classNameInputField.text))
                    ? _classNameInputField.text.Trim()
                    : null;

                if (_diagramActions.TryCreateClass(className, out _))
                {
                    if (_classNameInputField != null) _classNameInputField.text = string.Empty;
                    UpdateCreateClassButtonDisplay();
                }
            }
        }

        public bool CanCreateClass()
        {
            if (_turnContext == null || _turnContext.CurrentPhase != TurnPhase.Build) return false;
            return _diagramActions != null && _diagramActions.CanCreateClass;
        }

        private void HandleAssociationButtonClicked()
        {
            if (_turnContext == null || _turnContext.CurrentPhase != TurnPhase.Build) return;

            SetAssociationMode(!_isAssociationModeActive);
        }

        public void SetAssociationMode(bool active)
        {
            _isAssociationModeActive = active;
            if (_diagramActions != null && _diagramActions.IsAssociationMode != active)
            {
                _diagramActions.SetAssociationMode(active);
            }

            UpdateAssociationButtonDisplay();
        }

        private void PopulateMethodsList()
        {
            ClearMethodsList();

            if (_requirementContext == null || _methodsContainer == null)
            {
                return;
            }

            var assignedOrSeenMethods = _diagram != null
                ? new HashSet<(string Name, string Category)>(
                    _diagram.Classes.SelectMany(c => c.Methods)
                           .Select(m => (m.Name, m.Category ?? string.Empty)))
                : new HashSet<(string Name, string Category)>();

            var history = _requirementContext.RequirementsHistory;
            if (history == null || history.Count == 0)
            {
                if (_requirementContext.CurrentRequirement != null)
                {
                    history = new[] { _requirementContext.CurrentRequirement };
                }
                else
                {
                    return;
                }
            }

            for (int turnIndex = 0; turnIndex < history.Count; turnIndex++)
            {
                var requirement = history[turnIndex];
                if (requirement?.RequirementMethods == null) continue;

                bool isCurrentTurn = turnIndex == history.Count - 1;
                foreach (var method in requirement.RequirementMethods)
                {
                    if (!assignedOrSeenMethods.Add((method.Name, method.Category ?? string.Empty)))
                    {
                        continue;
                    }

                    GameObject cardGo = _methodCardPrefab != null
                        ? Instantiate(_methodCardPrefab, _methodsContainer)
                        : MethodCardUI.CreateDefault(_methodsContainer, method);

                    var cardUI = cardGo.GetComponent<MethodCardUI>() ?? cardGo.AddComponent<MethodCardUI>();
                    cardUI.Setup(method, isCurrentTurn);
                    cardUI.OnCardClicked += HandleCardClicked;
                    _activeCards.Add(cardUI);
                }
            }
        }

        private void HandleCardClicked(MethodData method)
        {
            if (_diagramActions != null && _diagramActions.SelectedClass != null)
            {
                _diagramActions.AddMethodToClass(_diagramActions.SelectedClass, method);
            }
        }

        private void ClearMethodsList()
        {
            foreach (var card in _activeCards)
            {
                if (card != null)
                {
                    card.OnCardClicked -= HandleCardClicked;
                    if (Application.isPlaying)
                    {
                        Destroy(card.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(card.gameObject);
                    }
                }
            }
            _activeCards.Clear();
        }

        private void UpdateAllDisplays()
        {
            UpdateCreateClassButtonDisplay();
            UpdateAssociationButtonDisplay();
            UpdateButtonsInteractable(_turnContext != null ? _turnContext.CurrentPhase : TurnPhase.Build);
            PopulateMethodsList();
        }

        private void UpdateCreateClassButtonDisplay()
        {
            if (_createClassButtonText != null)
            {
                _createClassButtonText.text = $"Criar Classe (Turno: {ClassesCreatedThisTurn}/{MaxClassesPerTurn} | Total: {TotalClassesCreated}/{MaxClassesPerMatch})";
            }

            if (_createClassButton != null)
            {
                _createClassButton.interactable = CanCreateClass();
            }
        }

        private void UpdateAssociationButtonDisplay()
        {
            if (_associationButtonText != null)
            {
                _associationButtonText.text = _isAssociationModeActive ? "Cancelar Associação" : "Criar Associação";
            }
        }

        private void UpdateButtonsInteractable(TurnPhase phase)
        {
            bool isBuildPhase = phase == TurnPhase.Build;

            UpdateCreateClassButtonDisplay();

            if (_associationButton != null)
            {
                _associationButton.interactable = isBuildPhase;
            }
        }
    }
}
