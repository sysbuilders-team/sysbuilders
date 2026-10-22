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
    /// <summary>
    /// EditMode tests for the ui-workflow bug sprint (bugs 1a/1b + bug D).
    /// </summary>
    public class UIBugSprintTests
    {
        // ─── Shared fakes ────────────────────────────────────────────────────

        private class FakeRequirementContext : IRequirementContext
        {
            private readonly List<Requirement> _history = new List<Requirement>();

            public Requirement CurrentRequirement => _history.Count > 0 ? _history[_history.Count - 1] : null;
            public IReadOnlyList<Requirement> RequirementsHistory => _history;
            public event Action<Requirement> OnRequirementChanged;

            public void AddRequirement(Requirement req)
            {
                _history.Add(req);
                OnRequirementChanged?.Invoke(req);
            }
        }

        private class FakeDiagramActions : IDiagramActions
        {
            public bool TryCreateClass(string className, out ClassNode createdNode)
            {
                createdNode = new ClassNode(className);
                return true;
            }
            public int ClassesCreatedThisTurn { get; set; }
            public int MaxClassesPerTurn => DiagramEditor.DefaultMaxClassesPerTurn;
            public int TotalClassesCreated { get; set; }
            public int MaxClassesPerMatch => DiagramEditor.DefaultMaxClassesPerMatch;
            public bool CanCreateClass =>
                ClassesCreatedThisTurn < MaxClassesPerTurn &&
                TotalClassesCreated < MaxClassesPerMatch;

            public bool RenameClass(ClassNode node, string newName) => true;

            public bool AddMethodToClass(ClassNode node, MethodData method) { node.AddMethod(method); OnDiagramChanged?.Invoke(); return true; }
            public bool RemoveMethodFromClass(ClassNode node, MethodData method) => true;
            public bool AddRelation(ClassNode origin, ClassNode destination, RelationType type = RelationType.Association) => true;
            public bool RemoveRelation(ClassNode origin, ClassNode destination) => true;
            public ClassNode SelectedClass { get; private set; }
            public void SelectClass(ClassNode node) { SelectedClass = node; OnClassSelected?.Invoke(node); }
            public void ClearSelection() { SelectedClass = null; OnClassSelected?.Invoke(null); }
            public event Action<ClassNode> OnClassSelected;
            public bool IsAssociationMode { get; private set; }
            public void SetAssociationMode(bool active) { IsAssociationMode = active; OnAssociationModeChanged?.Invoke(active); }
            public event Action<bool> OnAssociationModeChanged;
            public event Action OnDiagramChanged;
            public event Action<string> OnActionFailed;
        }

        private class FakeRefactorContext : IRefactorContext
        {
            public int RefactorPoints { get; set; } = 2;
            public int RefactorCost { get; set; } = 1;
            private ClassNode _selectedClass;

            public void SetSelectedClass(ClassNode node) => _selectedClass = node;

            public bool CanRefactor(ClassNode classNode) => RefactorPoints >= RefactorCost;
            public bool TryRefactorClass(ClassNode classNode) => true;
            public bool CanRefactorSelected() => _selectedClass != null && _selectedClass.IsLocked && RefactorPoints >= RefactorCost;
            public bool TryRefactorSelectedClass() => true;
            public event Action<int> OnRefactorPointsChanged;
            public void TriggerRefactorChanged(int pts) { RefactorPoints = pts; OnRefactorPointsChanged?.Invoke(pts); }
        }

        private class FakeScoreContext : IScoreContext
        {
            public string PlayerOneName { get; set; } = "A";
            public string PlayerTwoName { get; set; } = "B";
            public int PlayerOneScore { get; set; } = 0;
            public int PlayerTwoScore { get; set; } = 0;
            public event Action OnScoreUpdated;
        }

        // ─── Setup helpers ────────────────────────────────────────────────────

        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                UnityEngine.Object.DestroyImmediate(_root);
        }

        private ToolboxController CreateToolbox(out Transform methodsContainer)
        {
            var go = new GameObject("Toolbox");
            go.transform.SetParent(_root.transform);
            var ctrl = go.AddComponent<ToolboxController>();

            var containerGo = new GameObject("MethodsContainer", typeof(RectTransform));
            containerGo.transform.SetParent(go.transform);
            methodsContainer = containerGo.transform;
            SetField(ctrl, "_methodsContainer", methodsContainer);
            SetField(ctrl, "_maxClassesPerTurn", 2);

            var assocBtnGo = new GameObject("AssocBtn");
            assocBtnGo.transform.SetParent(go.transform);
            var assocBtn = assocBtnGo.AddComponent<Button>();
            SetField(ctrl, "_associationButton", assocBtn);

            return ctrl;
        }

        private static void SetField(object target, string name, object value)
        {
            var f = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            f?.SetValue(target, value);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Bug 1a — identity is (Name, Category), not just Name
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Bug1a_DifferentCategorySameName_BothCardsAppear()
        {
            // Arrange
            var ctx = new FakeRequirementContext();
            var actions = new FakeDiagramActions();
            var turnMgr = new TurnManager();
            turnMgr.SetPlayerReady(PlayerSlot.PlayerOne);
            turnMgr.SetPlayerReady(PlayerSlot.PlayerTwo); // Build phase

            var ctrl = CreateToolbox(out _);
            ctrl.Initialize(ctx, actions, turnMgr);

            // Reveal turn 1: Matricular/Usuario
            ctx.AddRequirement(new Requirement("r1", "Desc", new List<MethodData>
            {
                new MethodData("Matricular", "Usuario")
            }));

            // Reveal turn 2: Matricular/Financeiro — same name, different category
            ctx.AddRequirement(new Requirement("r2", "Desc", new List<MethodData>
            {
                new MethodData("Matricular", "Financeiro")
            }));

            // Both cards must be present (one from each turn, different categories)
            Assert.AreEqual(2, ctrl.ActiveCards.Count,
                "Both Matricular cards must appear — they have different categories.");
        }

        [Test]
        public void Bug1a_SameNameAndCategory_FiltersOutDuplicate()
        {
            // Arrange: two turns with the exact same (Name, Category) method
            var ctx = new FakeRequirementContext();
            var actions = new FakeDiagramActions();
            var turnMgr = new TurnManager();
            turnMgr.SetPlayerReady(PlayerSlot.PlayerOne);
            turnMgr.SetPlayerReady(PlayerSlot.PlayerTwo);

            var ctrl = CreateToolbox(out _);
            ctrl.Initialize(ctx, actions, turnMgr);

            ctx.AddRequirement(new Requirement("r1", "Desc", new List<MethodData>
            {
                new MethodData("Buscar", "Livro")
            }));

            ctx.AddRequirement(new Requirement("r2", "Desc", new List<MethodData>
            {
                new MethodData("Buscar", "Livro") // exact duplicate
            }));

            // Only one card should appear (duplicate suppressed)
            Assert.AreEqual(1, ctrl.ActiveCards.Count,
                "Exact duplicate (Name+Category) across turns must appear only once.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Bug 1b — RequirementsHistory instead of CurrentRequirement only
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Bug1b_PastTurnMethods_RemainsInToolboxAfterNewTurnReveal()
        {
            // Arrange
            var ctx = new FakeRequirementContext();
            var actions = new FakeDiagramActions();
            var turnMgr = new TurnManager();
            turnMgr.SetPlayerReady(PlayerSlot.PlayerOne);
            turnMgr.SetPlayerReady(PlayerSlot.PlayerTwo);

            var ctrl = CreateToolbox(out _);
            ctrl.Initialize(ctx, actions, turnMgr);

            // Turn 1 requirement
            ctx.AddRequirement(new Requirement("r1", "Desc", new List<MethodData>
            {
                new MethodData("Cadastrar", "Usuario")
            }));
            Assert.AreEqual(1, ctrl.ActiveCards.Count, "1 card after turn 1");

            // Turn 2 reveals a new requirement — turn 1 card must still be present
            ctx.AddRequirement(new Requirement("r2", "Desc", new List<MethodData>
            {
                new MethodData("Buscar", "Livro")
            }));

            Assert.AreEqual(2, ctrl.ActiveCards.Count,
                "Past-turn card (Cadastrar/Usuario) must still appear after turn 2 is revealed.");
        }

        [Test]
        public void Bug1b_HasPendingMethods_OnlyCountsCurrentTurnCards()
        {
            // Arrange: turn 1 card assigned to diagram; turn 2 card not assigned.
            // HasPendingMethods must reflect only current-turn cards.
            var ctx = new FakeRequirementContext();
            var actions = new FakeDiagramActions();
            var turnMgr = new TurnManager();
            turnMgr.SetPlayerReady(PlayerSlot.PlayerOne);
            turnMgr.SetPlayerReady(PlayerSlot.PlayerTwo);

            // Pre-populate diagram with turn-1 method already assigned
            var diagram = new ClassDiagram();
            var node = new ClassNode("Usuario");
            node.AddMethod(new MethodData("Cadastrar", "Usuario"));
            diagram.AddClass(node);

            var ctrl = CreateToolbox(out _);
            ctrl.Initialize(ctx, actions, turnMgr, diagram);

            // Turn 1 — method already in diagram, so no card
            ctx.AddRequirement(new Requirement("r1", "Desc", new List<MethodData>
            {
                new MethodData("Cadastrar", "Usuario")
            }));
            Assert.AreEqual(0, ctrl.ActiveCards.Count);

            // Turn 2 — new method, should appear and HasPendingMethods = true
            ctx.AddRequirement(new Requirement("r2", "Desc", new List<MethodData>
            {
                new MethodData("Buscar", "Livro")
            }));

            Assert.AreEqual(1, ctrl.ActiveCards.Count);
            Assert.IsTrue(ctrl.HasPendingMethods, "HasPendingMethods must be true for current-turn card.");
        }

        [Test]
        public void Bug1b_PastTurnCard_DoesNotBlockEndTurn()
        {
            // Arrange: only a past-turn card remains (current-turn all assigned).
            // HasPendingMethods must be false so end-turn is not blocked.
            var ctx = new FakeRequirementContext();
            var actions = new FakeDiagramActions();
            var turnMgr = new TurnManager();
            turnMgr.SetPlayerReady(PlayerSlot.PlayerOne);
            turnMgr.SetPlayerReady(PlayerSlot.PlayerTwo);

            // Diagram already has turn-2 method assigned
            var diagram = new ClassDiagram();
            var node = new ClassNode("Livro");
            node.AddMethod(new MethodData("Buscar", "Livro"));
            diagram.AddClass(node);

            var ctrl = CreateToolbox(out _);
            ctrl.Initialize(ctx, actions, turnMgr, diagram);

            // Turn 1 method NOT in diagram — past-turn card will appear
            ctx.AddRequirement(new Requirement("r1", "Desc", new List<MethodData>
            {
                new MethodData("Cadastrar", "Usuario")
            }));

            // Turn 2 method IS in diagram — no current-turn card
            ctx.AddRequirement(new Requirement("r2", "Desc", new List<MethodData>
            {
                new MethodData("Buscar", "Livro")
            }));

            // One past-turn card visible, but HasPendingMethods must be false
            Assert.AreEqual(1, ctrl.ActiveCards.Count, "Past-turn card is still visible (muted).");
            Assert.IsFalse(ctrl.HasPendingMethods,
                "HasPendingMethods must be false — past-turn card must not block end-turn.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Bug D — Refactor button uses CanRefactorSelected
        // ═══════════════════════════════════════════════════════════════════════

        private RightBarController CreateRightBar(out Button refactorBtn)
        {
            var go = new GameObject("RightBar");
            go.transform.SetParent(_root.transform);
            var ctrl = go.AddComponent<RightBarController>();

            var btnGo = new GameObject("RefactorBtn");
            btnGo.transform.SetParent(go.transform);
            refactorBtn = btnGo.AddComponent<Button>();
            SetField(ctrl, "_refactorClassButton", refactorBtn);

            var endBtnGo = new GameObject("EndTurnBtn");
            endBtnGo.transform.SetParent(go.transform);
            var endBtn = endBtnGo.AddComponent<Button>();
            SetField(ctrl, "_endTurnButton", endBtn);

            return ctrl;
        }

        [Test]
        public void BugD_RefactorButton_DisabledWhenNoClassSelected()
        {
            var refactorCtx = new FakeRefactorContext { RefactorPoints = 5, RefactorCost = 1 };
            // No class selected => CanRefactorSelected() returns false
            var scoreCtx = new FakeScoreContext();
            var turnMgr = new TurnManager();
            var actions = new FakeDiagramActions();

            var ctrl = CreateRightBar(out var refactorBtn);
            ctrl.Initialize(scoreCtx, refactorCtx, turnMgr, PlayerSlot.PlayerOne,
                turnContext: turnMgr, diagramActions: actions);

            // Points are sufficient, but no class is selected → button must be disabled
            Assert.IsFalse(refactorBtn.interactable,
                "Refactor button must be disabled when no class is selected, even if points >= cost.");
        }

        [Test]
        public void BugD_RefactorButton_EnabledOnlyWhenLockedClassSelected()
        {
            var refactorCtx = new FakeRefactorContext { RefactorPoints = 5, RefactorCost = 1 };
            var scoreCtx = new FakeScoreContext();
            var turnMgr = new TurnManager();
            var actions = new FakeDiagramActions();

            var ctrl = CreateRightBar(out var refactorBtn);
            ctrl.Initialize(scoreCtx, refactorCtx, turnMgr, PlayerSlot.PlayerOne,
                turnContext: turnMgr, diagramActions: actions);

            // Select a locked class
            var lockedNode = new ClassNode("LockedClass");
            lockedNode.Lock();
            refactorCtx.SetSelectedClass(lockedNode);

            // Simulate selection change
            actions.SelectClass(lockedNode);

            Assert.IsTrue(refactorBtn.interactable,
                "Refactor button must be enabled when a locked class is selected and points >= cost.");
        }

        [Test]
        public void BugD_RefactorButton_DisabledWhenInsufficientPoints()
        {
            var refactorCtx = new FakeRefactorContext { RefactorPoints = 0, RefactorCost = 1 };
            var scoreCtx = new FakeScoreContext();
            var turnMgr = new TurnManager();
            var actions = new FakeDiagramActions();

            var ctrl = CreateRightBar(out var refactorBtn);
            ctrl.Initialize(scoreCtx, refactorCtx, turnMgr, PlayerSlot.PlayerOne,
                turnContext: turnMgr, diagramActions: actions);

            var lockedNode = new ClassNode("Locked");
            lockedNode.Lock();
            refactorCtx.SetSelectedClass(lockedNode);
            actions.SelectClass(lockedNode);

            Assert.IsFalse(refactorBtn.interactable,
                "Refactor button must be disabled when points < cost, even with a locked class selected.");
        }
    }
}
