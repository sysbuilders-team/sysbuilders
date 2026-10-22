using Core.Diagram;
using Core.Evaluation;
using System;
using System.Collections.Generic;

namespace Core.Session
{
    public class PlayerState
    {
        public const int DefaultMaxRefactorPoints = 2;

        public string Name { get; }
        public int MaxRefactorPoints { get; }
        private readonly ClassDiagram _classDiagram;

        public IReadOnlyClassDiagram Diagram => _classDiagram;
        private int _refactorPoints;
        public int RefactorPoints => _refactorPoints;
        public int RefactorCost { get; }
        private readonly List<EvaluationResult> _evaluationHistory = new List<EvaluationResult>();
        public IReadOnlyList<EvaluationResult> EvaluationHistory => _evaluationHistory;
        
        public bool AddRelation(Relation relation) => _classDiagram.AddRelation(relation);
        public bool RemoveRelation(ClassNode origin, ClassNode destination) => _classDiagram.RemoveRelation(origin, destination);

        public PlayerState(string name, int maxRefactorPoints = DefaultMaxRefactorPoints)
        {
            Name = name;
            MaxRefactorPoints = maxRefactorPoints > 0 ? maxRefactorPoints : DefaultMaxRefactorPoints;
            _classDiagram = new ClassDiagram();
            _refactorPoints = 0;
            RefactorCost = 1;
        }

        public bool AddClass(ClassNode classNode)
        {
            if (classNode == null) return false;
            return _classDiagram.AddClass(classNode);
        }

        public bool RenameClass(ClassNode classNode, string newName)
        {
            if (classNode == null) return false;
            return _classDiagram.RenameClass(classNode, newName);
        }

        public void AddRefactorPoints(int points)
        {
            if (points > 0)
            {
                _refactorPoints = Math.Min(MaxRefactorPoints, _refactorPoints + points);
            }
        }

        public bool TryDeductPoints(int value)
        {
            if (value <= 0) return true;
            if (_refactorPoints < value) return false;

            _refactorPoints -= value;

            return true;
        }

        public void LockAllClasses()
        {
            foreach (var classNode in _classDiagram.Classes)
            {
                classNode.Lock();
            }
        }

        public void AddEvaluationResult(EvaluationResult result)
        {
            _evaluationHistory.Add(result);
        }
    }
}
