using System;
using System.Linq;
using Core.Session;

namespace Core.Diagram
{
    public class DiagramEditor : IDiagramActions
    {
        private readonly Func<PlayerState> _localPlayerProvider;
        private readonly Func<bool> _canModifyProvider;

        public ClassNode SelectedClass { get; private set; }
        public bool IsAssociationMode { get; private set; }

        public event Action<ClassNode> OnClassSelected;
        public event Action<bool> OnAssociationModeChanged;
        public event Action OnDiagramChanged;
        public event Action<string> OnActionFailed;

        private PlayerState LocalPlayer => _localPlayerProvider();
        public bool CanModifyDiagram => _canModifyProvider();

        public DiagramEditor(Func<PlayerState> localPlayerProvider, Func<bool> canModifyProvider)
        {
            _localPlayerProvider = localPlayerProvider ?? throw new ArgumentNullException(nameof(localPlayerProvider));
            _canModifyProvider = canModifyProvider ?? throw new ArgumentNullException(nameof(canModifyProvider));
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

            string finalName = string.IsNullOrWhiteSpace(className) ? GenerateDefaultClassName() : className.Trim();
            var newClass = new ClassNode(finalName);

            if (!LocalPlayer.AddClass(newClass))
            {
                OnActionFailed?.Invoke($"Já existe uma classe chamada '{finalName}' no diagrama.");
                return false;
            }

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

            if (string.IsNullOrWhiteSpace(newName))
            {
                OnActionFailed?.Invoke("O nome da classe não pode ser vazio.");
                return false;
            }

            string trimmed = newName.Trim();
            if (string.Equals(node.Name, trimmed, StringComparison.Ordinal))
            {
                return true;
            }

            if (!LocalPlayer.RenameClass(node, trimmed))
            {
                OnActionFailed?.Invoke($"Já existe uma classe chamada '{trimmed}' no diagrama.");
                return false;
            }

            OnDiagramChanged?.Invoke();
            return true;
        }

        public bool RemoveClass(ClassNode node)
        {
            if (!CanModifyDiagram || node == null || node.IsLocked) return false;
            if (!LocalPlayer.Diagram.Classes.Contains(node)) return false;

            if (SelectedClass == node)
            {
                ClearSelection();
            }

            if (!LocalPlayer.RemoveClass(node)) return false;

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

            bool removed = LocalPlayer.RemoveRelation(origin, destination);
            if (removed)
            {
                OnDiagramChanged?.Invoke();
            }
            return removed;
        }
    }
}
