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

        private bool _isAssociationModeActive;
        private readonly List<MethodCardUI> _activeCards = new List<MethodCardUI>();

        public int ClassesCreatedThisTurn => _diagramActions?.ClassesCreatedThisTurn ?? 0;
        public int MaxClassesPerTurn => _diagramActions?.MaxClassesPerTurn ?? DiagramEditor.DefaultMaxClassesPerTurn;
        public int TotalClassesCreated => _diagramActions?.TotalClassesCreated ?? 0;
        public int MaxClassesPerMatch => _diagramActions?.MaxClassesPerMatch ?? DiagramEditor.DefaultMaxClassesPerMatch;
        public bool IsAssociationModeActive => _isAssociationModeActive;
        public IReadOnlyList<MethodCardUI> ActiveCards => _activeCards;

        public event Action OnCreateClassClicked;
        public event Action<bool> OnAssociationModeToggled;
        public event Action<MethodData> OnMethodCardSelected;
        public event Action<bool> OnPendingMethodsChanged;

        private IReadOnlyClassDiagram _diagram;
        public bool HasPendingMethods => _activeCards.Count > 0;

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
            PopulateMethodsList(_requirementContext?.CurrentRequirement);
        }

        private void HandleRequirementChanged(Requirement newRequirement)
        {
            PopulateMethodsList(newRequirement);
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
            OnAssociationModeToggled?.Invoke(_isAssociationModeActive);
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

            OnCreateClassClicked?.Invoke();
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
            OnAssociationModeToggled?.Invoke(_isAssociationModeActive);
        }

        private void PopulateMethodsList(Requirement requirement)
        {
            ClearMethodsList();

            if (requirement == null || requirement.RequirementMethods == null || _methodsContainer == null)
            {
                OnPendingMethodsChanged?.Invoke(false);
                return;
            }

            var assignedMethodNames = _diagram != null
                ? new HashSet<string>(_diagram.Classes.SelectMany(c => c.Methods).Select(m => m.Name), StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            foreach (var method in requirement.RequirementMethods)
            {
                if (assignedMethodNames.Contains(method.Name))
                {
                    continue;
                }

                GameObject cardGo;
                if (_methodCardPrefab != null)
                {
                    cardGo = Instantiate(_methodCardPrefab, _methodsContainer);
                }
                else
                {
                    cardGo = CreateDefaultMethodCard(method);
                }

                var cardUI = cardGo.GetComponent<MethodCardUI>() ?? cardGo.AddComponent<MethodCardUI>();
                cardUI.Setup(method);
                cardUI.OnCardClicked += HandleCardClicked;
                _activeCards.Add(cardUI);
            }

            OnPendingMethodsChanged?.Invoke(HasPendingMethods);
        }

        // metodo fallback(ou usado sempre, xd) pra criar o cartao de metodo
        // pode ser melhor explorado, pra ficar mais agradavel como UI
        private GameObject CreateDefaultMethodCard(MethodData method)
        {
            var cardGo = new GameObject($"MethodCard_{method.Name}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            cardGo.transform.SetParent(_methodsContainer, false);

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
            tmp.text = $"+ {method.Name}()";
            tmp.fontSize = 14;
            tmp.color = new Color(0.12f, 0.12f, 0.18f, 1f);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            return cardGo;
        }

        private void HandleCardClicked(MethodData method)
        {
            if (_diagramActions != null && _diagramActions.SelectedClass != null)
            {
                _diagramActions.AddMethodToClass(_diagramActions.SelectedClass, method);
            }

            OnMethodCardSelected?.Invoke(method);
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
            PopulateMethodsList(_requirementContext?.CurrentRequirement);
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
