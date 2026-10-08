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
        public event Action<string> OnActionFailed;
        public ClassNode SelectedClass { get; private set; }
        public bool IsAssociationMode { get; private set; }
        public event Action<ClassNode> OnClassSelected;
        public event Action<bool> OnAssociationModeChanged;

        public MatchSession(
            string playerOneId,
            string playerTwoId,
            IReadOnlyList<Requirement> activeRequirements,
            PlayerSlot localSlot,
            IEvaluationService evaluator = null,
            bool isNetworkMatch = false)
        {
            if (activeRequirements == null || activeRequirements.Count == 0)
                throw new ArgumentException("A lista de requisitos não pode ser nula ou vazia.", nameof(activeRequirements));

            // A duracao da partida e o tamanho da trilha (TurnManager.MaxTurns usa Trail.TurnCount):
            // um numero diferente de requisitos deixaria turnos sem requisito ou requisitos ignorados.
            if (activeRequirements.Count != Trail.TurnCount)
                throw new ArgumentException(
                    $"A partida exige {Trail.TurnCount} requisitos (um por turno); recebidos {activeRequirements.Count}.",
                    nameof(activeRequirements));

            PlayerOne = new PlayerState(playerOneId);
            PlayerTwo = new PlayerState(playerTwoId);
            TurnManager = new TurnManager(isNetworkMatch ? 2 : 1);
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
                    ClearSelection();
                    SetAssociationMode(false);
                    PlayerOne.LockAllClasses();
                    PlayerTwo.LockAllClasses();
                    EvaluateLocalPlayer();
                    break;
                case TurnPhase.Show:
                    ClearSelection();
                    SetAssociationMode(false);
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
        // O histórico do rival precisa de exatamente uma entrada por turno, em ordem: um RPC
        // duplicado ou atrasado inflaria a soma das penalidades e corromperia o placar.
        public bool ReceiveOpponentResult(int turnNumber, EvaluationResult result)
        {
            if (result == null || turnNumber <= 0) return false;
            if (turnNumber != OpponentPlayer.EvaluationHistory.Count + 1) return false;

            OpponentPlayer.AddEvaluationResult(result);
            OnScoreUpdated?.Invoke();
            return true;
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

        public bool AddClassToPlayer(PlayerSlot slot, ClassNode classNode)
        {
            if (!CanModifyDiagram() || classNode == null) return false;

            return GetPlayerBySlot(slot).AddClass(classNode);
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

        public void SelectClass(ClassNode node)
        {
            if (!CanModifyDiagram()) return;
            SelectedClass = node;
            OnClassSelected?.Invoke(SelectedClass);
        }

        public void ClearSelection()
        {
            if (SelectedClass == null) return;
            SelectedClass = null;
            OnClassSelected?.Invoke(null);
        }

        public void SetAssociationMode(bool active)
        {
            if (!CanModifyDiagram() && active) return;
            if (IsAssociationMode == active) return;
            IsAssociationMode = active;
            OnAssociationModeChanged?.Invoke(IsAssociationMode);
        }

        public bool TryCreateClass(string className, out ClassNode createdNode)
        {
            createdNode = null;
            if (!CanModifyDiagram() || string.IsNullOrWhiteSpace(className)) return false;

            var newClass = new ClassNode(className);

            // Quem decide se o nome ja existe e o proprio diagrama (indice por nome, O(1)):
            // reportar sucesso sem verificar deixaria a UI contar uma classe que nao entrou.
            if (!LocalPlayer.AddClass(newClass)) return false;

            createdNode = newClass;
            OnDiagramChanged?.Invoke();
            return true;
        }

        public bool RemoveClass(ClassNode node)
        {
            if (!CanModifyDiagram() || node == null || node.IsLocked) return false;
            if (SelectedClass == node)
            {
                ClearSelection();
            }
            LocalPlayer.RemoveClass(node);
            if (!LocalPlayer.RemoveClass(node)) return false;

            OnDiagramChanged?.Invoke();
            return true;
        }

        public bool AddMethodToClass(ClassNode node, MethodData method)
        {
            if (!CanModifyDiagram())
            {
                OnActionFailed?.Invoke("Só é possível adicionar métodos na fase de construção.");
                return false;
            }

            if (node == null || method == null) return false;

            if (node.IsLocked)
            {
                OnActionFailed?.Invoke($"A classe '{node.Name}' está congelada. Refatore-a para alterar métodos.");
                return false;
            }

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
            if (!CanModifyDiagram())
            {
                OnActionFailed?.Invoke("Só é possível criar associações na fase de construção.");
                return false;
            }

            if (origin == null || destination == null) return false;

            if (origin == destination)
            {
                OnActionFailed?.Invoke("Não é possível associar uma classe a si mesma.");
                return false;
            }

            var relation = new Relation(origin, destination, type);
            bool added = LocalPlayer.AddRelation(relation);
            if (added)
            {
                if (IsAssociationMode)
                {
                    SetAssociationMode(false);
                }
                OnDiagramChanged?.Invoke();
            }
            else
            {
                OnActionFailed?.Invoke("As classes selecionadas já possuem uma associação.");
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