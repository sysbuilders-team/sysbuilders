namespace Core.Diagram
{
    public enum RelationType
    {
        Association
    }

    public class Relation
    {
        public ClassNode Origin { get; }
        public ClassNode Destination { get; }
        public RelationType Type { get; }

        public Relation(ClassNode origin, ClassNode destination, RelationType type)
        {
            Origin = origin;
            Destination = destination;
            Type = type;
        }
    }
}