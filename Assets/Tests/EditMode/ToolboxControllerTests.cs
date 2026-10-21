using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Diagram;
using Core.Requirements;
using Core.Session;
using NUnit.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.EditMode
{
    public class ToolboxControllerTests
    {
        private class FakeRequirementContext : IRequirementContext
        {
            public Requirement CurrentRequirement { get; set; }
            public IReadOnlyList<Requirement> RequirementsHistory => new List<Requirement>();
            public event Action<Requirement> OnRequirementChanged;

            public void TriggerRequirementChanged(Requirement req)
            {
                CurrentRequirement = req;
                OnRequirementChanged?.Invoke(req);
            }
        }

        private class FakeDiagramActions : IDiagramActions
        {
            public int ClassesCreatedThisTurn { get; set; }
            public int MaxClassesPerTurn { get; set; } = DiagramEditor.DefaultMaxClassesPerTurn;
            public int TotalClassesCreated { get; set; }
            public int MaxClassesPerMatch { get; set; } = DiagramEditor.DefaultMaxClassesPerMatch;
            public bool CanCreateClass =>
                ClassesCreatedThisTurn < MaxClassesPerTurn &&
                TotalClassesCreated < MaxClassesPerMatch;

            public bool TryCreateClass(string className, out ClassNode createdNode)
            {
                if (!CanCreateClass)
                {
                    createdNode = null;
                    return false;
                }

                ClassesCreatedThisTurn++;
                TotalClassesCreated++;
                createdNode = new ClassNode(className ?? $"Classe{TotalClassesCreated}");
                OnDiagramChanged?.Invoke();
                return true;
            }
            public bool RenameClass(ClassNode node, string newName) => true;
            public bool AddMethodToClass(ClassNode node, MethodData method)
            {
                node?.AddMethod(method);
                return true;
            }
            public bool RemoveMethodFromClass(ClassNode node, MethodData method) => true;
            public bool AddRelation(ClassNode origin, ClassNode destination, RelationType type = RelationType.Association) => true;
            public bool RemoveRelation(ClassNode origin, ClassNode destination) => true;
            public ClassNode SelectedClass { get; private set; }
            public void SelectClass(ClassNode node)
            {
                SelectedClass = node;
                OnClassSelected?.Invoke(node);
            }
            public void ClearSelection()
            {
                SelectedClass = null;
                OnClassSelected?.Invoke(null);
            }
            public event Action<ClassNode> OnClassSelected;

            public bool IsAssociationMode { get; private set; }
            public void SetAssociationMode(bool active)
            {
                IsAssociationMode = active;
                OnAssociationModeChanged?.Invoke(active);
            }
            public event Action<bool> OnAssociationModeChanged;

            public event Action OnDiagramChanged;
            public event Action<string> OnActionFailed { add { } remove { } }
        }

        private GameObject _go;
        private ToolboxController _controller;
        private Button _createClassBtn;
        private TextMeshProUGUI _createClassBtnText;
        private Transform _methodsContainer;
        private Button _assocBtn;
        private TextMeshProUGUI _assocBtnText;

        private FakeRequirementContext _requirementContext;
        private FakeDiagramActions _diagramActions;
        private TurnManager _turnManager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ToolboxTest");
            _controller = _go.AddComponent<ToolboxController>();

            var createBtnGo = new GameObject("CreateClassBtn");
            createBtnGo.transform.SetParent(_go.transform);
            _createClassBtn = createBtnGo.AddComponent<Button>();

            var createBtnTextGo = new GameObject("CreateClassBtnText");
            createBtnTextGo.transform.SetParent(createBtnGo.transform);
            _createClassBtnText = createBtnTextGo.AddComponent<TextMeshProUGUI>();

            var containerGo = new GameObject("MethodsContainer", typeof(RectTransform));
            containerGo.transform.SetParent(_go.transform);
            _methodsContainer = containerGo.transform;

            var assocBtnGo = new GameObject("AssocBtn");
            assocBtnGo.transform.SetParent(_go.transform);
            _assocBtn = assocBtnGo.AddComponent<Button>();

            var assocBtnTextGo = new GameObject("AssocBtnText");
            assocBtnTextGo.transform.SetParent(assocBtnGo.transform);
            _assocBtnText = assocBtnTextGo.AddComponent<TextMeshProUGUI>();

            SetPrivateField(_controller, "_createClassButton", _createClassBtn);
            SetPrivateField(_controller, "_createClassButtonText", _createClassBtnText);
            SetPrivateField(_controller, "_methodsContainer", _methodsContainer);
            SetPrivateField(_controller, "_associationButton", _assocBtn);
            SetPrivateField(_controller, "_associationButtonText", _assocBtnText);

            _requirementContext = new FakeRequirementContext();
            _diagramActions = new FakeDiagramActions();
            _turnManager = new TurnManager();

            // Advance TurnManager to Build phase for testing
            _turnManager.SetPlayerReady(PlayerSlot.PlayerOne);
            _turnManager.SetPlayerReady(PlayerSlot.PlayerTwo);
            Assert.AreEqual(TurnPhase.Build, _turnManager.CurrentPhase);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void Initialize_DisplaysTurnAndMatchClassCreationLimits()
        {
            _controller.Initialize(_requirementContext, _diagramActions, _turnManager);

            StringAssert.Contains("0/2", _createClassBtnText.text);
            StringAssert.Contains("0/7", _createClassBtnText.text);
            Assert.IsTrue(_createClassBtn.interactable);
        }

        [Test]
        public void CreateClassButton_UpdatesCountersAndDisablesWhenTurnOrMatchLimitReached()
        {
            _diagramActions.MaxClassesPerTurn = 2;
            _diagramActions.MaxClassesPerMatch = 3;
            _controller.Initialize(_requirementContext, _diagramActions, _turnManager);

            _createClassBtn.onClick.Invoke();
            StringAssert.Contains("1/2", _createClassBtnText.text);
            StringAssert.Contains("1/3", _createClassBtnText.text);
            Assert.IsTrue(_createClassBtn.interactable);

            _createClassBtn.onClick.Invoke();
            StringAssert.Contains("2/2", _createClassBtnText.text);
            StringAssert.Contains("2/3", _createClassBtnText.text);
            Assert.IsFalse(_createClassBtn.interactable);
            Assert.IsFalse(_controller.CanCreateClass());
        }

        [Test]
        public void OnRequirementChanged_PopulatesMethodsCards()
        {
            _controller.Initialize(_requirementContext, _diagramActions, _turnManager);

            var req = new Requirement("req_1", "Desc", new List<MethodData>
            {
                new MethodData("Cadastrar", "Livro"),
                new MethodData("Emprestar", "Emprestimo")
            });

            _requirementContext.TriggerRequirementChanged(req);

            Assert.AreEqual(2, _controller.ActiveCards.Count);
            Assert.AreEqual("Cadastrar", _controller.ActiveCards[0].Method.Name);
            Assert.AreEqual("Emprestar", _controller.ActiveCards[1].Method.Name);
        }

        [Test]
        public void SetAssociationMode_TogglesStateAndText()
        {
            _controller.Initialize(_requirementContext, _diagramActions, _turnManager);

            _controller.SetAssociationMode(true);
            Assert.IsTrue(_controller.IsAssociationModeActive);
            StringAssert.Contains("Cancelar Associação", _assocBtnText.text);

            _controller.SetAssociationMode(false);
            Assert.IsFalse(_controller.IsAssociationModeActive);
            StringAssert.Contains("Criar Associação", _assocBtnText.text);
        }

        [Test]
        public void CreateClassButton_WhenClicked_CallsTryCreateClassAndIncrementsCounter()
        {
            _controller.Initialize(_requirementContext, _diagramActions, _turnManager);

            _createClassBtn.onClick.Invoke();

            Assert.AreEqual(1, _controller.ClassesCreatedThisTurn);
            Assert.AreEqual(1, _controller.TotalClassesCreated);
            StringAssert.Contains("1/2", _createClassBtnText.text);
            StringAssert.Contains("1/7", _createClassBtnText.text);
        }

        [Test]
        public void MethodCardClicked_WhenClassSelected_CallsAddMethodToClass()
        {
            _controller.Initialize(_requirementContext, _diagramActions, _turnManager);
            var selectedNode = new ClassNode("Usuario");
            _diagramActions.SelectClass(selectedNode);

            var req = new Requirement("req_1", "Desc", new List<MethodData>
            {
                new MethodData("Cadastrar", "Usuario")
            });
            _requirementContext.TriggerRequirementChanged(req);

            Assert.AreEqual(1, _controller.ActiveCards.Count);
            var cardButton = _controller.ActiveCards[0].GetComponent<Button>();
            cardButton.onClick.Invoke();

            Assert.AreEqual(1, selectedNode.Methods.Count);
            Assert.AreEqual("Cadastrar", selectedNode.Methods[0].Name);
        }

        [Test]
        public void PopulateMethods_FiltersOutMethodsAlreadyAssignedToDiagramByIdentity()
        {
            var diagram = new ClassDiagram();
            var node = new ClassNode("Usuario");
            node.AddMethod(new MethodData("Cadastrar", "Usuario"));
            diagram.AddClass(node);

            _controller.Initialize(_requirementContext, _diagramActions, _turnManager, diagram);

            var req = new Requirement("req_1", "Desc", new List<MethodData>
            {
                new MethodData("Cadastrar", "Usuario"),
                new MethodData("Cadastrar", "Livro"), // Mesmo nome, mas outra categoria: deve continuar pendente
                new MethodData("Buscar", "Usuario")
            });
            _requirementContext.TriggerRequirementChanged(req);

            Assert.AreEqual(2, _controller.ActiveCards.Count);
            Assert.AreEqual("Cadastrar", _controller.ActiveCards[0].Method.Name);
            Assert.AreEqual("Livro", _controller.ActiveCards[0].Method.Category);
            Assert.AreEqual("Buscar", _controller.ActiveCards[1].Method.Name);
            Assert.IsTrue(_controller.HasPendingMethods);
        }

        [Test]
        public void HasPendingMethods_ReturnsFalse_WhenAllMethodsAssigned()
        {
            var diagram = new ClassDiagram();
            var node = new ClassNode("Usuario");
            node.AddMethod(new MethodData("Cadastrar", "Usuario"));
            diagram.AddClass(node);

            _controller.Initialize(_requirementContext, _diagramActions, _turnManager, diagram);

            var req = new Requirement("req_1", "Desc", new List<MethodData>
            {
                new MethodData("Cadastrar", "Usuario")
            });
            _requirementContext.TriggerRequirementChanged(req);

            Assert.AreEqual(0, _controller.ActiveCards.Count);
            Assert.IsFalse(_controller.HasPendingMethods);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
