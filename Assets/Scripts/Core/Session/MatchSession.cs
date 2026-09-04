using System;
using System.Collections.Generic;
using Core.Diagram;
using Core.Evaluation;
using Core.Requirements;
using TextMateSharp.Themes;

namespace Core.Session
{
    public class MatchSession
    {
        public PlayerState PlayerOne { get; }
        public PlayerState PlayerTwo { get; }
        public TurnManager TurnManager { get; }
        private readonly RefactorHandler _refactorHandler;
        private readonly EvaluationEngine _evaluator;
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
                    //TODO: UI reage trocando de tela e lendo o estado atualizado dos jogadores
                    break;
            }
        }

        private void EvaluateBothPlayer()
        {
            var resultOne = _evaluator.Evaluate(PlayerOne.Diagram, _activeRequirements);
            PlayerOne.AddEvaluationResult(resultOne);

            // TODO: DEVE PASSAR A LISTA DE REQUIREMENTS ATIVOS DO TURNO, TAREFA DO REQUIREMENTPROVIDER
            var resultTwo = _evaluator.Evaluate(PlayerTwo.Diagram, _activeRequirements);
            PlayerTwo.AddEvaluationResult(resultTwo);

            OnEvaluationCompleted?.Invoke(resultOne, resultTwo);
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