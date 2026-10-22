using System;

namespace Core.Diagram
{
    public interface IDiagramActions
    {
        int ClassesCreatedThisTurn { get; }
        int MaxClassesPerTurn { get; }
        int TotalClassesCreated { get; }
        int MaxClassesPerMatch { get; }
        bool CanCreateClass { get; }

        bool TryCreateClass(string className, out ClassNode createdNode);
        bool RenameClass(ClassNode node, string newName);

        bool AddMethodToClass(ClassNode node, MethodData method);
        bool RemoveMethodFromClass(ClassNode node, MethodData method);

        bool AddRelation(ClassNode origin, ClassNode destination, RelationType type = RelationType.Association);
        bool RemoveRelation(ClassNode origin, ClassNode destination);

        ClassNode SelectedClass { get; }
        void SelectClass(ClassNode node);
        void ClearSelection();
        event Action<ClassNode> OnClassSelected;

        bool IsAssociationMode { get; }
        void SetAssociationMode(bool active);
        event Action<bool> OnAssociationModeChanged;

        event Action OnDiagramChanged;
        event Action<string> OnActionFailed;
    }
}
