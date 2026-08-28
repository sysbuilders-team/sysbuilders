using Core.Diagram;
using Core.Evaluation;
using System.Collections.Generic;

namespace Core.Session
{
    public interface IReadOnlyClassDiagram
    {
        IReadOnlyList<ClassNode> Classes {get;}
        IReadOnlyList<Relation> Relations {get;}
        ClassNode FindClassByName(string name);
    }
    public class PlayerState
    {
        public string Name { get; }
        private readonly ClassDiagram _classDiagram;

        public IReadOnlyClassDiagram Diagram => _classDiagram;
        private int _refactorPoints;

        public int RefactorCost { get; }
        private readonly List<EvaluationResult> _evaluationHistory = new List<EvaluationResult>();
        public IReadOnlyList<EvaluationResult> EvaluationHistory => _evaluationHistory;

        // TODO: Later, Need to add a list of EvaluationResult, or smthing equivalent, to player keep track of their Evaluation Points
        public PlayerState(string name)
        {
            Name = name;
            _classDiagram = new ClassDiagram();
            _refactorPoints = 0;
            RefactorCost = 1;
        }

        public void AddClass(ClassNode classNode)
        {
            _classDiagram.AddClass(classNode);
        }

        public void RemoveClass(ClassNode classNode)
        {
            _classDiagram.RemoveClass(classNode);
        }

        // TODO: Need to test edge cases for this one
        public bool TryDeductPoints(int value)
        {
            if (_refactorPoints < value) return false;

            _refactorPoints -= value;

            return true;
        }

        public void AddEvaluationResult(EvaluationResult result)
        {
            _evaluationHistory.Add(result);
        }
    }
}