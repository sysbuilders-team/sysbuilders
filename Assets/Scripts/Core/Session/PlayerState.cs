using Core.Diagram;

namespace Core.Session
{
    public class PlayerState
    {
        public string Name { get; }
        private readonly ClassDiagram _classDiagram;

        public ClassDiagram Diagram => _classDiagram;
        private int _refactorPoints;

        public int RefactorCost { get; }
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
    }
}