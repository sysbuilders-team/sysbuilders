using System.Collections.Generic;

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

        public bool AddRelation(Relation relation)
        {
            if (relation == null || relation.Origin == null || relation.Destination == null) return false;

            bool alreadyExists = _relations.Exists(r => 
                (r.Origin == relation.Origin && r.Destination == relation.Destination) ||
                (r.Origin == relation.Destination && r.Destination == relation.Origin));

            if (alreadyExists) return false;

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

        public IReadOnlyList<IReadOnlyList<ClassNode>> GetConnectedComponents()
        {
            var visited = new HashSet<ClassNode>();
            var components = new List<IReadOnlyList<ClassNode>>();

            foreach (var classNode in _classes)
            {
                if (visited.Contains(classNode)) continue;

                var component = new List<ClassNode>();
                var queue = new Queue<ClassNode>();
                queue.Enqueue(classNode);
                visited.Add(classNode);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    component.Add(current);

                    foreach (var neighbor in GetNeighbors(current))
                    {
                        if (visited.Contains(neighbor)) continue;
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }

                components.Add(component);
            }

            return components;
        }

        private IEnumerable<ClassNode> GetNeighbors(ClassNode node)
        {
            foreach (var relation in _relations)
            {
                if (relation.Origin == node) yield return relation.Destination;
                else if (relation.Destination == node) yield return relation.Origin;
            }
        }
        
        public bool RemoveRelation(ClassNode origin, ClassNode destination)
        {
            if (origin == null || destination == null) return false;

            var relation = _relations.Find(r => 
                (r.Origin == origin && r.Destination == destination) ||
                (r.Origin == destination && r.Destination == origin));

            if (relation != null)
            {
                return _relations.Remove(relation);
            }
            return false;
        }
    }
}