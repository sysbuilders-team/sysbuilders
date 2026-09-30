using System;
using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using Core.Evaluation;
using Core.Requirements;

namespace Core.Session
{
    public class MatchSession : IRequirementContext, IScoreContext, IRefactorContext, ITurnSummaryContext, IMatchEndContext, IDiagramActions
    {
        public PlayerState PlayerOne { get; }
        public PlayerState PlayerTwo { get; }
            
        public PlayerSlot LocalSlot { get; }
        public PlayerState LocalPlayer => GetPlayerBySlot(LocalSlot);
        
        public PlayerState OpponentPlayer => LocalSlot == PlayerSlot.PlayerOne ? PlayerTwo : PlayerOne;
        public TurnManager TurnManager { get; }
        private readonly RefactorHandler _refactorHandler;
        private readonly IEvaluationService _evaluator;
        private readonly IReadOnlyList<Requirement> _activeRequirements;

        public event Action<EvaluationResult> OnEvaluationCompleted;
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
        // Regra B: o RawScore de cada avaliação já é acumulado (re-verifica todos os requisitos
        // revelados) e a penalidade é recalculada por turno.
        // Placar = RawScore da última avaliação - soma das penalidades de cada turno.
        public int PlayerOneScore => CalculateScore(PlayerOne);
        public int PlayerTwoScore => CalculateScore(PlayerTwo);

        public int RefactorPoints => LocalPlayer.RefactorPoints;

        public int RefactorCost => LocalPlayer.RefactorCost;

        public int RoundPointsEarned => RoundRawDelta(LocalPlayer);
        public int RoundPenaltyPoints => LocalPlayer.EvaluationHistory.LastOrDefault()?.PenaltyScore ?? 0;

        public int RoundOpponentPointsEarned => RoundRawDelta(OpponentPlayer);

        public int RoundOpponentPenaltyPoints => OpponentPlayer.EvaluationHistory.LastOrDefault()?.PenaltyScore ?? 0;

        public IReadOnlyList<EvaluationResult> PlayerOneHistory => PlayerOne.EvaluationHistory;
        public IReadOnlyList<EvaluationResult> PlayerTwoHistory => PlayerTwo.EvaluationHistory;

        public bool IsDraw => PlayerOneScore == PlayerTwoScore;
        public string WinnerName => IsDraw ? null : (PlayerOneScore > PlayerTwoScore) ? PlayerOneName : PlayerTwoName;
        
        public event Action OnDiagramChanged;

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
            LocalSlot = localSlot;
            TurnManager.OnPhaseChange += HandlePhaseChange;
        }

        private void HandlePhaseChange(TurnPhase newPhase)
        {
            switch (newPhase)
            {
                case TurnPhase.Evaluate:
                    PlayerOne.LockAllClasses();
                    PlayerTwo.LockAllClasses();
                    EvaluateLocalPlayer();
                    break;
                case TurnPhase.Show:
                    OnRequirementChanged?.Invoke(CurrentRequirement);
                    break;
            }
        }
        
        private void EvaluateLocalPlayer()
        {
            var requirementsSoFar = GetRequirementsUpToCurrentTurn();
            var localResult = _evaluator.Evaluate(LocalPlayer.Diagram, requirementsSoFar);
            LocalPlayer.AddEvaluationResult(localResult);

            OnScoreUpdated?.Invoke();
            OnEvaluationCompleted?.Invoke(localResult);
        }

        // Chamado pela camada de rede com o resultado que o rival calculou na máquina dele.
        public void ReceiveOpponentResult(EvaluationResult result)
        {
            if (result == null) return;

            OpponentPlayer.AddEvaluationResult(result);
            OnScoreUpdated?.Invoke();
        }

        private static int RoundRawDelta(PlayerState player)
        {
            var history = player.EvaluationHistory;
            if (history.Count == 0) return 0;

            int previousRaw = history.Count > 1 ? history[history.Count - 2].RawScore : 0;
            return history[history.Count - 1].RawScore - previousRaw;
        }

        private static int CalculateScore(PlayerState player)
        {
            var history = player.EvaluationHistory;
            if (history.Count == 0) return 0;

            return history[history.Count - 1].RawScore - history.Sum(e => e.PenaltyScore);
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

        public PlayerState GetPlayerBySlot(PlayerSlot slot)
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
        public bool TryCreateClass(string className, out ClassNode createdNode)
        {
            createdNode = null;
            if (!CanModifyDiagram() || string.IsNullOrWhiteSpace(className)) return false;
            createdNode = new ClassNode(className);
            bool success = LocalPlayer.Diagram.Classes.All(c => c.Name != className);
            if (success)
            {
                LocalPlayer.AddClass(createdNode);
                OnDiagramChanged?.Invoke();
                return true;
            }
            createdNode = null;
            return false;
        }
        public bool RemoveClass(ClassNode node)
        {
            if (!CanModifyDiagram() || node == null || node.IsLocked) return false;
            LocalPlayer.RemoveClass(node);
            OnDiagramChanged?.Invoke();
            return true;
        }
        public bool AddMethodToClass(ClassNode node, MethodData method)
        {
            if (!CanModifyDiagram() || node == null || method == null || node.IsLocked) return false;
            node.AddMethod(method);
            OnDiagramChanged?.Invoke();
            return true;
        }
        public bool RemoveMethodFromClass(ClassNode node, MethodData method)
        {
            if (!CanModifyDiagram() || node == null || method == null || node.IsLocked) return false;
            bool removed = node.RemoveMethod(method);
            if (removed)
            {
                OnDiagramChanged?.Invoke();
            }
            return removed;
        }
        public bool AddRelation(ClassNode origin, ClassNode destination, RelationType type = RelationType.Association)
        {
            if (!CanModifyDiagram() || origin == null || destination == null || origin == destination) return false;
            var relation = new Relation(origin, destination, type);
            bool added = LocalPlayer.AddRelation(relation);
            if (added)
            {
                OnDiagramChanged?.Invoke();
            }
            return added;
        }
        public bool RemoveRelation(ClassNode origin, ClassNode destination)
        {
            if (!CanModifyDiagram() || origin == null || destination == null) return false;
            bool removed = LocalPlayer.RemoveRelation(origin, destination);
            if (removed)
            {
                OnDiagramChanged?.Invoke();
            }
            return removed;
        }

        public void Dispose()
        {
            TurnManager.OnPhaseChange -= HandlePhaseChange;
        }
    }
}