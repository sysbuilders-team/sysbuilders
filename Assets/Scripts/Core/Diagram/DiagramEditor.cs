using System;
using System.Collections.Generic;
using System.Linq;
using Core.Requirements;
using Core.Session;

namespace Core.Diagram
{
    public class DiagramEditor : IDiagramActions
    {
        public const int DefaultMaxClassesPerTurn = 2;
        public const int DefaultMaxClassesPerMatch = Trail.DefaultMaxClasses;

        private readonly Func<PlayerState> _localPlayerProvider;
        private readonly Func<bool> _canModifyProvider;
        private readonly HashSet<ClassNode> _classesCreatedThisTurn = new HashSet<ClassNode>();

        public int ClassesCreatedThisTurn => _classesCreatedThisTurn.Count;
        public int MaxClassesPerTurn { get; }
        public int TotalClassesCreated => LocalPlayer?.Diagram.Classes.Count ?? 0;
        public int MaxClassesPerMatch { get; }
        public bool CanCreateClass =>
            CanModifyDiagram &&
            ClassesCreatedThisTurn < MaxClassesPerTurn &&
            TotalClassesCreated < MaxClassesPerMatch;

        public ClassNode SelectedClass { get; private set; }
        public bool IsAssociationMode { get; private set; }

        public event Action<ClassNode> OnClassSelected;
        public event Action<bool> OnAssociationModeChanged;
        public event Action OnDiagramChanged;
        public event Action<string> OnActionFailed;

        private PlayerState LocalPlayer => _localPlayerProvider();
        public bool CanModifyDiagram => _canModifyProvider();

        public DiagramEditor(
            Func<PlayerState> localPlayerProvider,
            Func<bool> canModifyProvider,
            int maxClassesPerTurn = DefaultMaxClassesPerTurn,
            int maxClassesPerMatch = DefaultMaxClassesPerMatch)
        {
            _localPlayerProvider = localPlayerProvider ?? throw new ArgumentNullException(nameof(localPlayerProvider));
            _canModifyProvider = canModifyProvider ?? throw new ArgumentNullException(nameof(canModifyProvider));

            if (maxClassesPerTurn < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxClassesPerTurn),
                    "O limite de classes por turno deve ser pelo menos 1.");
            }

            if (maxClassesPerMatch < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxClassesPerMatch),
                    "O limite de classes por partida deve ser pelo menos 1.");
            }

            MaxClassesPerTurn = maxClassesPerTurn;
            MaxClassesPerMatch = maxClassesPerMatch;
        }

        public void ResetTurnQuota()
        {
            _classesCreatedThisTurn.Clear();
        }

        public void SelectClass(ClassNode node)
        {
            if (!CanModifyDiagram) return;
            if (node != null && !LocalPlayer.Diagram.Classes.Contains(node)) return;

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
            if (!CanModifyDiagram && active) return;
            if (IsAssociationMode == active) return;

            IsAssociationMode = active;
            OnAssociationModeChanged?.Invoke(IsAssociationMode);
        }

        public void ResetInteractionState()
        {
            ClearSelection();
            SetAssociationMode(false);
        }

        public void NotifyDiagramChanged()
        {
            OnDiagramChanged?.Invoke();
        }

        public bool TryCreateClass(string className, out ClassNode createdNode)
        {
            createdNode = null;
            if (!CanModifyDiagram) return false;

            if (TotalClassesCreated >= MaxClassesPerMatch)
            {
                OnActionFailed?.Invoke($"Limite máximo de {MaxClassesPerMatch} classes na partida atingido.");
                return false;
            }

            if (ClassesCreatedThisTurn >= MaxClassesPerTurn)
            {
                OnActionFailed?.Invoke($"Limite de {MaxClassesPerTurn} classes por turno atingido.");
                return false;
            }

            string finalName = ClassNameRules.Sanitize(className) ?? GenerateDefaultClassName();
            var newClass = new ClassNode(finalName);

            if (!LocalPlayer.AddClass(newClass))
            {
                OnActionFailed?.Invoke($"Já existe uma classe chamada '{finalName}' no diagrama.");
                return false;
            }

            _classesCreatedThisTurn.Add(newClass);
            createdNode = newClass;
            SelectClass(newClass);
            OnDiagramChanged?.Invoke();
            return true;
        }

        private string GenerateDefaultClassName()
        {
            var existing = LocalPlayer.Diagram.Classes;
            int n = 1;
            string candidate;
            do
            {
                candidate = $"Classe{n++}";
            } while (existing.Any(c => c.Name == candidate));

            return candidate;
        }

        public bool RenameClass(ClassNode node, string newName)
        {
            if (node == null) return false;

            if (!CanModifyDiagram)
            {
                OnActionFailed?.Invoke("Só é possível renomear classes na fase de construção.");
                return false;
            }

            if (!LocalPlayer.Diagram.Classes.Contains(node))
            {
                OnActionFailed?.Invoke("A classe selecionada não pertence ao seu diagrama.");
                return false;
            }

            if (node.IsLocked)
            {
                OnActionFailed?.Invoke($"A classe '{node.Name}' está congelada. Refatore-a para renomeá-la.");
                return false;
            }

            string sanitized = ClassNameRules.Sanitize(newName);
            if (sanitized == null)
            {
                OnActionFailed?.Invoke("O nome da classe não pode ser vazio.");
                return false;
            }

            if (string.Equals(node.Name, sanitized, StringComparison.Ordinal))
            {
                return true;
            }

            if (!LocalPlayer.RenameClass(node, sanitized))
            {
                OnActionFailed?.Invoke($"Já existe uma classe chamada '{sanitized}' no diagrama.");
                return false;
            }

            OnDiagramChanged?.Invoke();
            return true;
        }

        public bool RemoveClass(ClassNode node)
        {
            if (node == null) return false;

            if (!CanModifyDiagram)
            {
                OnActionFailed?.Invoke("Só é possível remover classes na fase de construção.");
                return false;
            }

            if (node.IsLocked)
            {
                OnActionFailed?.Invoke($"A classe '{node.Name}' está congelada e não pode ser removida sem refatoração.");
                return false;
            }

            bool removed = LocalPlayer.RemoveClass(node);
            if (!removed) return false;

            _classesCreatedThisTurn.Remove(node);

            if (SelectedClass == node)
            {
                ClearSelection();
            }

            OnDiagramChanged?.Invoke();
            return true;
        }

        public bool AddMethodToClass(ClassNode node, MethodData method)
        {
            if (!CanModifyDiagram)
            {
                OnActionFailed?.Invoke("Só é possível adicionar métodos na fase de construção.");
                return false;
            }

            if (node == null || method == null) return false;

            if (!LocalPlayer.Diagram.Classes.Contains(node))
            {
                OnActionFailed?.Invoke("A classe selecionada não pertence ao seu diagrama.");
                return false;
            }

            if (node.IsLocked)
            {
                OnActionFailed?.Invoke($"A classe '{node.Name}' está congelada. Refatore-a para alterar métodos.");
                return false;
            }

            if (node.Methods.Contains(method))
            {
                OnActionFailed?.Invoke($"O método '{method.Name}' já está presente na classe '{node.Name}'.");
                return false;
            }

            node.AddMethod(method);
            OnDiagramChanged?.Invoke();
            return true;
        }

        public bool RemoveMethodFromClass(ClassNode node, MethodData method)
        {
            if (!CanModifyDiagram || node == null || method == null || node.IsLocked) return false;
            if (!LocalPlayer.Diagram.Classes.Contains(node)) return false;

            bool removed = node.RemoveMethod(method);
            if (removed)
            {
                OnDiagramChanged?.Invoke();
            }
            return removed;
        }

        public bool AddRelation(ClassNode origin, ClassNode destination, RelationType type = RelationType.Association)
        {
            if (!CanModifyDiagram)
            {
                OnActionFailed?.Invoke("Só é possível conectar classes na fase de construção.");
                return false;
            }

            if (origin == null || destination == null) return false;
            if (origin.IsLocked && destination.IsLocked)
            {
                OnActionFailed?.Invoke("Não é possível associar classes bloqueadas. Refatore pelo menos uma delas primeiro.");
                return false;
            }

            if (!LocalPlayer.Diagram.Classes.Contains(origin) || !LocalPlayer.Diagram.Classes.Contains(destination))
            {
                OnActionFailed?.Invoke("As classes associadas devem pertencer ao seu diagrama.");
                return false;
            }

            if (origin == destination)
            {
                OnActionFailed?.Invoke("Uma classe não pode se relacionar consigo mesma.");
                return false;
            }

            bool added = LocalPlayer.AddRelation(new Relation(origin, destination, type));
            if (!added)
            {
                OnActionFailed?.Invoke("Essa associação já existe entre as classes selecionadas.");
                return false;
            }

            SetAssociationMode(false);
            OnDiagramChanged?.Invoke();
            return true;
        }

        public bool RemoveRelation(ClassNode origin, ClassNode destination)
        {   
            if (!CanModifyDiagram || origin == null || destination == null) return false;
            if (origin.IsLocked && destination.IsLocked)
            {
                OnActionFailed?.Invoke("Não é possível remover associação entre classes bloqueadas. Refatore pelo menos uma delas primeiro.");
                return false;
            }

            bool removed = LocalPlayer.RemoveRelation(origin, destination);
            if (removed)
            {
                OnDiagramChanged?.Invoke();
            }
            return removed;
        }
    }
}
