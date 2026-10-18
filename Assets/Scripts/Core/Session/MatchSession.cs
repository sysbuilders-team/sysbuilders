using System;
using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using Core.Evaluation;
using Core.Requirements;

namespace Core.Session
{
    public class MatchSession : IDisposable, IRequirementContext, IScoreContext, IRefactorContext, ITurnSummaryContext, IMatchEndContext, IDiagramActions
    {
        public PlayerState PlayerOne { get; }
        public PlayerState PlayerTwo { get; }
        public PlayerSlot LocalSlot { get; }
        public PlayerState LocalPlayer => GetPlayerBySlot(LocalSlot);
        public PlayerState OpponentPlayer => LocalSlot == PlayerSlot.PlayerOne ? PlayerTwo : PlayerOne;

        public TurnManager TurnManager { get; }

        private readonly DiagramEditor _editor;
        private readonly MatchScoreTracker _scoreTracker;
        private readonly RefactorHandler _refactorHandler;
        private readonly IRefactorPointPolicy _refactorPointPolicy;
        private readonly IEvaluationService _evaluator;
        private readonly IReadOnlyList<Requirement> _activeRequirements;

        // Sub-contexts for clean ISP injection
        public DiagramEditor Editor => _editor;
        public MatchScoreTracker ScoreTracker => _scoreTracker;
        public IDiagramActions DiagramActions => _editor;
        public IScoreContext ScoreContext => _scoreTracker;
        public ITurnSummaryContext TurnSummaryContext => _scoreTracker;
        public IMatchEndContext MatchEndContext => _scoreTracker;
        public IRequirementContext RequirementContext => this;
        public ITurnContext TurnContext => TurnManager;
        public IRefactorContext RefactorContext => this;

        // Events
        public event Action<EvaluationResult> OnEvaluationCompleted;
        public event Action<Requirement> OnRequirementChanged;
        public event Action OnScoreUpdated;
        public event Action<int> OnRefactorPointsChanged;

        // IDiagramActions Events (forwarded from DiagramEditor)
        public event Action OnDiagramChanged;
        public event Action<string> OnActionFailed;
        public event Action<ClassNode> OnClassSelected;
        public event Action<bool> OnAssociationModeChanged;

        #region IRequirementContext
        public Requirement CurrentRequirement
        {
            get
            {
                var index = TurnManager.CurrentTurn - 1;
                if (index >= 0 && index < _activeRequirements.Count)
                    return _activeRequirements[index];
                return null;
            }
        }

        public IReadOnlyList<Requirement> RequirementsHistory => GetRequirementsUpToCurrentTurn();
        #endregion

        #region IScoreContext (forwarded to MatchScoreTracker)
        public string PlayerOneName => _scoreTracker.PlayerOneName;
        public string PlayerTwoName => _scoreTracker.PlayerTwoName;
        public int PlayerOneScore => _scoreTracker.PlayerOneScore;
        public int PlayerTwoScore => _scoreTracker.PlayerTwoScore;
        #endregion

        #region ITurnSummaryContext (forwarded to MatchScoreTracker)
        public int RoundPointsEarned => _scoreTracker.RoundPointsEarned;
        public int RoundPenaltyPoints => _scoreTracker.RoundPenaltyPoints;
        public int RoundOpponentPointsEarned => _scoreTracker.RoundOpponentPointsEarned;
        public int RoundOpponentPenaltyPoints => _scoreTracker.RoundOpponentPenaltyPoints;
        #endregion

        #region IMatchEndContext (forwarded to MatchScoreTracker)
        public IReadOnlyList<EvaluationResult> PlayerOneHistory => _scoreTracker.PlayerOneHistory;
        public IReadOnlyList<EvaluationResult> PlayerTwoHistory => _scoreTracker.PlayerTwoHistory;
        public bool IsDraw => _scoreTracker.IsDraw;
        public string WinnerName => _scoreTracker.WinnerName;
        #endregion

        #region IRefactorContext
        public int RefactorPoints => LocalPlayer.RefactorPoints;
        public int RefactorCost => LocalPlayer.RefactorCost;
        #endregion

        #region IDiagramActions (forwarded to DiagramEditor)
        public ClassNode SelectedClass => _editor.SelectedClass;
        public bool IsAssociationMode => _editor.IsAssociationMode;
        #endregion

        public MatchSession(
            string playerOneId,
            string playerTwoId,
            IReadOnlyList<Requirement> activeRequirements,
            PlayerSlot localSlot,
            IEvaluationService evaluator = null,
            bool isNetworkMatch = false,
            IRefactorPointPolicy refactorPointPolicy = null)
        {
            if (activeRequirements == null || activeRequirements.Count == 0)
                throw new ArgumentException("A lista de requisitos não pode ser nula ou vazia.", nameof(activeRequirements));

            if (activeRequirements.Count != Trail.TurnCount)
                throw new ArgumentException(
                    $"A partida exige {Trail.TurnCount} requisitos (um por turno); recebidos {activeRequirements.Count}.",
                    nameof(activeRequirements));

            PlayerOne = new PlayerState(playerOneId);
            PlayerTwo = new PlayerState(playerTwoId);
            LocalSlot = localSlot;
            TurnManager = new TurnManager(isNetworkMatch ? 2 : 1);

            _refactorPointPolicy = refactorPointPolicy ?? new DefaultRefactorPointPolicy();
            _refactorHandler = new RefactorHandler();
            _evaluator = evaluator ?? new EvaluationEngine();
            _activeRequirements = activeRequirements;

            _editor = new DiagramEditor(() => LocalPlayer, CanModifyDiagram);
            _scoreTracker = new MatchScoreTracker(PlayerOne, PlayerTwo, LocalSlot);

            // Wire forwarded events
            _editor.OnDiagramChanged += ForwardDiagramChanged;
            _editor.OnClassSelected += ForwardClassSelected;
            _editor.OnAssociationModeChanged += ForwardAssociationModeChanged;
            _editor.OnActionFailed += ForwardActionFailed;
            _scoreTracker.OnScoreUpdated += ForwardScoreUpdated;

            // Grant initial refactor points
            PlayerOne.AddRefactorPoints(_refactorPointPolicy.InitialPoints);
            PlayerTwo.AddRefactorPoints(_refactorPointPolicy.InitialPoints);

            TurnManager.OnPhaseChange += HandlePhaseChange;
        }

        private void ForwardDiagramChanged() => OnDiagramChanged?.Invoke();
        private void ForwardClassSelected(ClassNode node) => OnClassSelected?.Invoke(node);
        private void ForwardAssociationModeChanged(bool mode) => OnAssociationModeChanged?.Invoke(mode);
        private void ForwardActionFailed(string msg) => OnActionFailed?.Invoke(msg);
        private void ForwardScoreUpdated() => OnScoreUpdated?.Invoke();

        private void HandlePhaseChange(TurnPhase newPhase)
        {
            switch (newPhase)
            {
                case TurnPhase.Evaluate:
                    _editor.ResetInteractionState();
                    PlayerOne.LockAllClasses();
                    PlayerTwo.LockAllClasses();
                    EvaluateLocalPlayer();
                    break;
                case TurnPhase.Show:
                    _editor.ResetInteractionState();
                    OnRequirementChanged?.Invoke(CurrentRequirement);
                    break;
                case TurnPhase.Build:
                    if (TurnManager.CurrentTurn > 1)
                    {
                        int points = _refactorPointPolicy.GetPointsForTurn(TurnManager.CurrentTurn);
                        if (points > 0)
                        {
                            PlayerOne.AddRefactorPoints(points);
                            PlayerTwo.AddRefactorPoints(points);
                            OnRefactorPointsChanged?.Invoke(LocalPlayer.RefactorPoints);
                        }
                    }
                    break;
            }
        }

        private void EvaluateLocalPlayer()
        {
            var requirementsSoFar = GetRequirementsUpToCurrentTurn();
            var localResult = _evaluator.Evaluate(LocalPlayer.Diagram, requirementsSoFar);
            _scoreTracker.RecordEvaluation(LocalPlayer, localResult);

            OnEvaluationCompleted?.Invoke(localResult);
        }

        public bool ReceiveOpponentResult(int turnNumber, EvaluationResult result)
        {
            return _scoreTracker.ReceiveOpponentResult(turnNumber, result);
        }

        private IReadOnlyList<Requirement> GetRequirementsUpToCurrentTurn()
        {
            return _activeRequirements.Take(TurnManager.CurrentTurn).ToList();
        }

        public bool CanModifyDiagram()
        {
            return TurnManager.CurrentPhase == TurnPhase.Build;
        }

        public PlayerState GetPlayerBySlot(PlayerSlot slot)
        {
            return slot == PlayerSlot.PlayerOne ? PlayerOne : PlayerTwo;
        }

        public SetPlayerReadyResult SetPlayerReady(PlayerSlot slot)
        {
            return TurnManager.SetPlayerReady(slot);
        }

        #region Refactoring
        public bool CanRefactor(ClassNode classNode)
        {
            if (!CanModifyDiagram() || classNode == null || !classNode.IsLocked || LocalPlayer.RefactorPoints < LocalPlayer.RefactorCost)
                return false;

            return true;
        }

        public bool CanRefactorSelected()
        {
            return SelectedClass != null && CanRefactor(SelectedClass);
        }

        public bool TryRefactorSelectedClass()
        {
            if (SelectedClass == null) return false;
            return TryRefactorClass(SelectedClass);
        }

        public bool TryRefactorClass(ClassNode classNode)
        {
            if (!CanModifyDiagram()) return false;

            bool result = _refactorHandler.TryRefactor(classNode, LocalPlayer);
            if (result)
            {
                OnRefactorPointsChanged?.Invoke(LocalPlayer.RefactorPoints);
                _editor.NotifyDiagramChanged();
            }
            return result;
        }
        #endregion

        #region IDiagramActions Delegation to DiagramEditor
        public void SelectClass(ClassNode node) => _editor.SelectClass(node);
        public void ClearSelection() => _editor.ClearSelection();
        public void SetAssociationMode(bool active) => _editor.SetAssociationMode(active);
        public bool TryCreateClass(string className, out ClassNode createdNode) => _editor.TryCreateClass(className, out createdNode);
        public bool RenameClass(ClassNode node, string newName) => _editor.RenameClass(node, newName);
        public bool RemoveClass(ClassNode node) => _editor.RemoveClass(node);
        public bool AddMethodToClass(ClassNode node, MethodData method) => _editor.AddMethodToClass(node, method);
        public bool RemoveMethodFromClass(ClassNode node, MethodData method) => _editor.RemoveMethodFromClass(node, method);
        public bool AddRelation(ClassNode origin, ClassNode destination, RelationType type = RelationType.Association) => _editor.AddRelation(origin, destination, type);
        public bool RemoveRelation(ClassNode origin, ClassNode destination) => _editor.RemoveRelation(origin, destination);
        #endregion

        public void Dispose()
        {
            TurnManager.OnPhaseChange -= HandlePhaseChange;
            _editor.OnDiagramChanged -= ForwardDiagramChanged;
            _editor.OnClassSelected -= ForwardClassSelected;
            _editor.OnAssociationModeChanged -= ForwardAssociationModeChanged;
            _editor.OnActionFailed -= ForwardActionFailed;
            _scoreTracker.OnScoreUpdated -= ForwardScoreUpdated;
        }
    }
}