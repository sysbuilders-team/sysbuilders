using System.Collections.Generic;

namespace Core.Diagram
{
    public class ClassDiagram
    {
        private readonly List<ClassNode> _classes = new List<ClassNode>();
        private readonly List<Relation> _relations = new List<Relation>();

        private readonly List<MethodData> _methods = new List<MethodData>();
        public IReadOnlyList<ClassNode> Classes => _classes;
        public IReadOnlyList<Relation> Relations => _relations;

        public IReadOnlyList<MethodData> Methods => _methods;

        public void AddClass(ClassNode classNode)
        {
            _classes.Add(classNode);
        }

        public void AddRelation(Relation relation)
        {
            _relations.Add(relation);
        }

        public void AddMethod(MethodData method)
        {
            _methods.Add(method);
        }

        public ClassNode FindClassByName(string name)
        {
            return _classes.Find(c => c.Name == name);
        }
    }
}