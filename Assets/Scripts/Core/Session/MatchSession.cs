using System;
using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using Core.Evaluation;
using Core.Requirements;

namespace Core.Session
{
    public class MatchSession : IDisposable, IRequirementContext
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
        private readonly IEvaluationService _evaluator;
        private readonly IReadOnlyList<Requirement> _activeRequirements;

        // Sub-contexts for clean ISP injection
        public DiagramEditor Editor => _editor;
        public MatchScoreTracker ScoreTracker => _scoreTracker;
        public RefactorHandler RefactorHandler => _refactorHandler;
        public IDiagramActions DiagramActions => _editor;
        public IScoreContext ScoreContext => _scoreTracker;
        public ITurnSummaryContext TurnSummaryContext => _scoreTracker;
        public IMatchEndContext MatchEndContext => _scoreTracker;
        public IRequirementContext RequirementContext => this;
        public ITurnContext TurnContext => TurnManager;
        public IRefactorContext RefactorContext => _refactorHandler;

        // Events
        public event Action<EvaluationResult> OnEvaluationCompleted;
        public event Action<Requirement> OnRequirementChanged;

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

        public bool HasPendingMethods
        {
            get
            {
                var current = CurrentRequirement;
                if (current?.RequirementMethods == null || current.RequirementMethods.Count == 0)
                    return false;

                var assigned = new HashSet<(string Name, string Category)>(
                    LocalPlayer.Diagram.Classes
                        .SelectMany(c => c.Methods)
                        .Select(m => (m.Name, m.Category ?? string.Empty)));

                return current.RequirementMethods.Any(
                    m => !assigned.Contains((m.Name, m.Category ?? string.Empty)));
            }
        }
        #endregion

        public MatchSession(
            string playerOneId,
            string playerTwoId,
            IReadOnlyList<Requirement> activeRequirements,
            PlayerSlot localSlot,
            IEvaluationService evaluator = null,
            int requiredPlayers  = 1,
            IRefactorPointPolicy refactorPointPolicy = null,
            int maxClassesPerMatch = Trail.DefaultMaxClasses)
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
            TurnManager = new TurnManager(requiredPlayers);

            _evaluator = evaluator ?? new EvaluationEngine();
            _activeRequirements = activeRequirements;

            _editor = new DiagramEditor(
                localPlayerProvider: () => LocalPlayer,
                canModifyProvider: CanModifyDiagram,
                maxClassesPerMatch: maxClassesPerMatch);
            _scoreTracker = new MatchScoreTracker(PlayerOne, PlayerTwo, LocalSlot);
            _refactorHandler = new RefactorHandler(
                localPlayerProvider: () => LocalPlayer,
                canModifyProvider: CanModifyDiagram,
                selectedClassProvider: () => _editor.SelectedClass,
                onClassRefactored: _editor.NotifyDiagramChanged,
                pointPolicy: refactorPointPolicy
            );

            _refactorHandler.GrantInitialPoints(PlayerOne, PlayerTwo);

            TurnManager.OnPhaseChange += HandlePhaseChange;
        }

        private void HandlePhaseChange(TurnPhase newPhase)
        {
            switch (newPhase)
            {
                case TurnPhase.Evaluate:
                    _editor.ResetInteractionState();
                    PlayerOne.LockAllClasses();
                    PlayerTwo.LockAllClasses();
                    _editor.NotifyDiagramChanged();
                    EvaluateLocalPlayer();
                    break;
                case TurnPhase.Show:
                    _editor.ResetInteractionState();
                    _editor.ResetTurnQuota();
                    OnRequirementChanged?.Invoke(CurrentRequirement);
                    break;
                case TurnPhase.Build:
                    _editor.ResetTurnQuota();
                    _refactorHandler.GrantTurnPoints(TurnManager.CurrentTurn, PlayerOne, PlayerTwo);
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
            return TurnManager.CurrentPhase == TurnPhase.Build && !TurnManager.IsPlayerReady(LocalSlot);
        }

        public PlayerState GetPlayerBySlot(PlayerSlot slot)
        {
            return slot == PlayerSlot.PlayerOne ? PlayerOne : PlayerTwo;
        }

        public SetPlayerReadyResult SetPlayerReady(PlayerSlot slot)
        {
            return TurnManager.SetPlayerReady(slot);
        }

        public void Dispose()
        {
            TurnManager.OnPhaseChange -= HandlePhaseChange;
        }
    }
}
