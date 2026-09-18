using System;
using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using Core.Evaluation;
using Core.Requirements;

namespace Core.Session
{
    public class MatchSession : IRequirementContext, IScoreContext, IRefactorContext
    {
        public PlayerState PlayerOne { get; }
        public PlayerState PlayerTwo { get; }
        
        public PlayerSlot PlayerSlot { get; }
        public PlayerState LocalPlayer => GetPlayerBySlot(PlayerSlot);
        public TurnManager TurnManager { get; }
        private readonly RefactorHandler _refactorHandler;
        private readonly IEvaluationService _evaluator;
        private readonly IReadOnlyList<Requirement> _activeRequirements;

        public event Action<EvaluationResult, EvaluationResult> OnEvaluationCompleted;
        public event Action<Requirement> OnRequirementChanged;
        public event Action OnScoreUpdated;

        public event Action<int> OnRefactorPointsChanged;

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

        public string PlayerOneName => PlayerOne.Name;
        public string PlayerTwoName => PlayerTwo.Name;
        public int PlayerOneScore => PlayerOne.EvaluationHistory.Sum(e => e.TotalScore);
        public int PlayerTwoScore => PlayerTwo.EvaluationHistory.Sum(e => e.TotalScore);

        public int RefactorPoints => LocalPlayer.RefactorPoints;

        public int RefactorCost => LocalPlayer.RefactorCost;

        public MatchSession(
            string playerOneId,
            string playerTwoId,
            IReadOnlyList<Requirement> activeRequirements,
            PlayerSlot localSlot,
            IEvaluationService evaluator = null)
        {
            if (activeRequirements == null || activeRequirements.Count == 0)
                throw new ArgumentException("A lista de requisitos não pode ser nula ou vazia.", nameof(activeRequirements));

            PlayerOne = new PlayerState(playerOneId);
            PlayerTwo = new PlayerState(playerTwoId);
            TurnManager = new TurnManager();
            _refactorHandler = new RefactorHandler();
            _evaluator = evaluator ?? new EvaluationEngine();
            _activeRequirements = activeRequirements;
            PlayerSlot = localSlot;
            TurnManager.OnPhaseChange += HandlePhaseChange;
        }

        private void HandlePhaseChange(TurnPhase newPhase)
        {
            switch (newPhase)
            {
                case TurnPhase.Evaluate:
                    PlayerOne.LockAllClasses();
                    PlayerTwo.LockAllClasses();
                    EvaluateBothPlayer();
                    break;
                case TurnPhase.Show:
                    OnRequirementChanged?.Invoke(CurrentRequirement);
                    break;
            }
        }
        
        

        private void EvaluateBothPlayer()
        {
            var requirementsSoFar = GetRequirementsUpToCurrentTurn();
            var resultOne = _evaluator.Evaluate(PlayerOne.Diagram, requirementsSoFar);
            PlayerOne.AddEvaluationResult(resultOne);

            var resultTwo = _evaluator.Evaluate(PlayerTwo.Diagram, requirementsSoFar);
            PlayerTwo.AddEvaluationResult(resultTwo);

            OnScoreUpdated?.Invoke();
            OnEvaluationCompleted?.Invoke(resultOne, resultTwo);
        }

        private IReadOnlyList<Requirement> GetRequirementsUpToCurrentTurn()
        {
            return _activeRequirements.Take(TurnManager.CurrentTurn).ToList();
        }
        
        public bool TryRefactorClass(ClassNode classNode)
        {
            if (!CanModifyDiagram()) return false;

            bool result = _refactorHandler.TryRefactor(classNode, LocalPlayer);
            if (result)
            {
                OnRefactorPointsChanged?.Invoke(LocalPlayer.RefactorPoints);
            }
            return result;
        }

        private PlayerState GetPlayerBySlot(PlayerSlot slot)
        {
            return slot == PlayerSlot.PlayerOne ? PlayerOne : PlayerTwo;
        }

        public void AddClassToPlayer(PlayerSlot slot, ClassNode classNode)
        {
            if (!CanModifyDiagram()) return;

            GetPlayerBySlot(slot).AddClass(classNode);
        }

        public SetPlayerReadyResult SetPlayerReady(PlayerSlot slot)
        {
            return TurnManager.SetPlayerReady(slot);
        }

        private bool CanModifyDiagram()
        {
            return TurnManager.CurrentPhase == TurnPhase.Build;
        }

        public bool CanRefactor(ClassNode classNode)
        {
            if (!CanModifyDiagram() || !classNode.IsLocked || LocalPlayer.RefactorPoints < LocalPlayer.RefactorCost)
                return false;
            
            return true;
        }

        public void Dispose()
        {
            TurnManager.OnPhaseChange -= HandlePhaseChange;
        }
    }
}
