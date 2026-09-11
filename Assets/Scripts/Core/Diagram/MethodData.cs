namespace Core.Diagram
{
    public class MethodData
    {
        public string Name { get; }
        public string Category { get; }

        public MethodData(string name, string category)
        {
            Name = name ?? string.Empty;
            Category = category ?? string.Empty;
        }
    }
}