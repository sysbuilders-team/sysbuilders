using Core.Diagram;

namespace Core.Session
{
    public interface IPlayerSessionActions
    {
        int RefactorPoints { get; }
        int RefactorCost { get; }

        bool TryAddClass(string className);
        bool TryRemoveClass(string className);
        bool TryAddMethodToClass(string className, MethodData method);
        bool TryAddRelation(string originClassName, string targetClassName, RelationType type);
        bool TryRefactorClass(string className);
    }
}