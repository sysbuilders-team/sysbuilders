using System;
using Core.Diagram;

namespace Core.Session
{
    public interface IRefactorContext
    {
        int RefactorPoints { get; }
        int RefactorCost { get; }
        bool CanRefactor(ClassNode classNode);
        bool TryRefactorClass(ClassNode classNode);
        event Action<int> OnRefactorPointsChanged;
    }
}