using System.Collections.Generic;
using Core.Session;

namespace Core.Diagram
{
    public class ClassDiagram : IReadOnlyClassDiagram
    {
        private readonly List<ClassNode> _classes = new List<ClassNode>();
        private readonly List<Relation> _relations = new List<Relation>();

        public IReadOnlyList<ClassNode> Classes => _classes;
        public IReadOnlyList<Relation> Relations => _relations;
        
        public bool AddClass(ClassNode classNode)
        {
            if (classNode == null || string.IsNullOrWhiteSpace(classNode.Name)) return false;
            if (_classes.Exists(x => x.Name == classNode.Name)) return false;
            
            _classes.Add(classNode);
            return true;
        }

        // Unnecessary edge case is to see if the Origin and Destination classes exists 
        public bool AddRelation(Relation relation)
        {
            if (relation == null) return false;
            
            _relations.Add(relation);
            return true;
        }

        public bool RemoveClass(ClassNode classNode)
        {
            if (classNode == null) return false;
            
            bool removed = _classes.Remove(classNode);
            if (removed)
            {
                _relations.RemoveAll(x => x.Origin == classNode || x.Destination == classNode);
            }
            
            return removed;
        }

        public ClassNode FindClassByName(string name)
        {
            return _classes.Find(c => c.Name == name);
        }
        
        
    }
}