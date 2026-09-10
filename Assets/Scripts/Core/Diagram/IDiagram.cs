using System.Collections.Generic;

namespace Core.Diagram
{
    public interface IReadOnlyClassDiagram
    {
        IReadOnlyList<ClassNode> Classes { get; }
        IReadOnlyList<Relation> Relations { get; }
        ClassNode FindClassByName(string name);
        IReadOnlyList<IReadOnlyList<ClassNode>> GetConnectedComponents();
    }
}