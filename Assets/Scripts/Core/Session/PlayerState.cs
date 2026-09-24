using Core.Diagram;
using Core.Evaluation;
using System.Collections.Generic;

namespace Core.Session
{
    public class PlayerState
    {
        public string Name { get; }
        private readonly ClassDiagram _classDiagram;

        public IReadOnlyClassDiagram Diagram => _classDiagram;
        private int _refactorPoints;
        public int RefactorPoints => _refactorPoints;
        // TODO: Counter of classcreations this turns to limit how many allowed, generally 2 or 3 per turn.
        public int RefactorCost { get; }
        private readonly List<EvaluationResult> _evaluationHistory = new List<EvaluationResult>();
        public IReadOnlyList<EvaluationResult> EvaluationHistory => _evaluationHistory;
        
        public bool AddRelation(Relation relation) => _classDiagram.AddRelation(relation);
        public bool RemoveRelation(ClassNode origin, ClassNode destination) => _classDiagram.RemoveRelation(origin, destination);

        public PlayerState(string name)
        {
            Name = name;
            _classDiagram = new ClassDiagram();
            _refactorPoints = 0;
            RefactorCost = 1;
        }

        public void AddClass(ClassNode classNode)
        {
            if (classNode == null) return;
            _classDiagram.AddClass(classNode);
        }

        public void RemoveClass(ClassNode classNode)
        {
            if (classNode == null) return;
            _classDiagram.RemoveClass(classNode);
        }

        public void AddRefactorPoints(int points)
        {
            if (points > 0)
            {
                _refactorPoints += points;
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