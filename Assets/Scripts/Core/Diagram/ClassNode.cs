using System.Collections.Generic;

namespace Core.Diagram
{
    public class ClassNode
    {
        public string Name { get; }
        public List<MethodData> Methods { get; }
        // o set sendo private significa que so pode ser setado dentro dessa classe
        public bool IsLocked { get; private set; }
        

        public ClassNode(string name)
        {
            Name = name;
            Methods = new List<MethodData>();
            IsLocked = false;
        }

        public void AddMethod(MethodData method)
        {
            if (!IsLocked)
            {
                Methods.Add(method);
            }
        }

        public void Lock()
        {
            IsLocked = true;
        }
    }
}