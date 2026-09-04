using System;
using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using Core.Evaluation;
using Core.Requirements;

namespace Core.Session
{
    public class MatchSession
    {
        public PlayerState PlayerOne { get; }
        public PlayerState PlayerTwo { get; }
        public TurnManager TurnManager { get; }
        private readonly RefactorHandler _refactorHandler;
        private readonly IEvaluationService _evaluator;
        private readonly IReadOnlyList<Requirement> _activeRequirements;
        public event Action<EvaluationResult, EvaluationResult> OnEvaluationCompleted;

        public MatchSession(string playerOneId, string playerTwoId, string theme, IRequirementProvider requirementProvider)
        {
            PlayerOne = new PlayerState(playerOneId);
            PlayerTwo = new PlayerState(playerTwoId);
            TurnManager = new TurnManager();    
            _refactorHandler = new RefactorHandler();
            _evaluator = new EvaluationEngine();

            _activeRequirements = requirementProvider.SelectTrailForMatch(theme, playerOneId, playerTwoId);
            TurnManager.OnPhaseChange += HandlePhaseChange;
        }

        private void HandlePhaseChange(TurnPhase newPhase)
        {
            switch (newPhase)
            {
                case TurnPhase.Evaluate:
                    EvaluateBothPlayer();
                    break;
                case TurnPhase.Show:
                    // TODO: UI reage trocando de tela e lendo o estado atualizado dos jogadores
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

            OnEvaluationCompleted?.Invoke(resultOne, resultTwo);
        }

        private IReadOnlyList<Requirement> GetRequirementsUpToCurrentTurn()
        {
            return _activeRequirements.Take(TurnManager.CurrentTurn).ToList();
        }

        public bool TryRefactorClass(PlayerSlot slot, ClassNode classNode)
        {
            if (!CanModifyDiagram()) return false;

            var player = GetPlayerBySlot(slot);
            return _refactorHandler.TryRefactor(classNode, player);
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

        public void Dispose()
        {
            TurnManager.OnPhaseChange -= HandlePhaseChange;
        }
    }
}
