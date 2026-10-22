using System;
using System.Collections.Generic;

namespace Core.Diagram
{
    public class ClassDiagram : IReadOnlyClassDiagram
    {
        private readonly List<ClassNode> _classes = new List<ClassNode>();
        // Indice por nome: AddClass e FindClassByName deixam de varrer a lista (O(V) -> O(1)).
        private readonly Dictionary<string, ClassNode> _classesByName = new Dictionary<string, ClassNode>(StringComparer.Ordinal);
        private readonly List<Relation> _relations = new List<Relation>();

        public IReadOnlyList<ClassNode> Classes => _classes;
        public IReadOnlyList<Relation> Relations => _relations;
        
        public bool AddClass(ClassNode classNode)
        {
            if (classNode == null || string.IsNullOrWhiteSpace(classNode.Name)) return false;
            if (_classesByName.ContainsKey(classNode.Name)) return false;

            _classesByName.Add(classNode.Name, classNode);
            _classes.Add(classNode);
            return true;
        }

        public bool RenameClass(ClassNode classNode, string newName)
        {
            if (classNode == null || string.IsNullOrWhiteSpace(newName)) return false;
            if (!_classes.Contains(classNode)) return false;

            string trimmed = newName.Trim();
            if (string.Equals(classNode.Name, trimmed, StringComparison.Ordinal))
            {
                return true;
            }

            if (_classesByName.ContainsKey(trimmed)) return false;

            if (classNode.Name != null)
            {
                _classesByName.Remove(classNode.Name);
            }

            if (!classNode.Rename(trimmed))
            {
                if (classNode.Name != null) _classesByName[classNode.Name] = classNode;
                return false;
            }

            _classesByName.Add(trimmed, classNode);
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

        public ClassNode FindClassByName(string name)
        {
            if (name == null) return null;
            return _classesByName.TryGetValue(name, out var classNode) ? classNode : null;
        }

        public IReadOnlyList<IReadOnlyList<ClassNode>> GetConnectedComponents()
        {
            // A adjacencia e montada numa unica passada pelas relacoes: o BFS fica O(V+E).
            // Antes, cada no desenfileirado varria a lista completa de relacoes (O(V*E)).
            var adjacency = new Dictionary<ClassNode, List<ClassNode>>(_classes.Count);
            foreach (var classNode in _classes)
            {
                adjacency[classNode] = new List<ClassNode>();
            }

            foreach (var relation in _relations)
            {
                if (relation.Origin == null || relation.Destination == null || relation.Origin == relation.Destination)
                {
                    continue;
                }

                // Relacoes apontando para nos que nao pertencem ao diagrama sao ignoradas,
                // como no comportamento anterior.
                if (adjacency.TryGetValue(relation.Origin, out var originNeighbors) &&
                    adjacency.TryGetValue(relation.Destination, out var destinationNeighbors))
                {
                    originNeighbors.Add(relation.Destination);
                    destinationNeighbors.Add(relation.Origin);
                }
            }

            var visited = new HashSet<ClassNode>();
            var components = new List<IReadOnlyList<ClassNode>>();
            var queue = new Queue<ClassNode>();

            foreach (var start in _classes)
            {
                if (!visited.Add(start)) continue;

                var component = new List<ClassNode>();
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    component.Add(current);

                    foreach (var neighbor in adjacency[current])
                    {
                        if (visited.Add(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                components.Add(component);
            }

            return components;
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
