using System.Collections.Generic;

namespace Core.Diagram
{
    public class ClassNode
    {
        public string Name { get; }
        private readonly List<MethodData> _methods = new List<MethodData>();
        public IReadOnlyList<MethodData> Methods => _methods;
        public bool IsLocked { get; private set; }
        

        public ClassNode(string name)
        {
            Name = name;
            IsLocked = false;
        }

        public void AddMethod(MethodData method)
        {
            if (!IsLocked)
            {
                _methods.Add(method);
            }
        }

        public void Lock()
        {
            IsLocked = true;
        }

        public void Unlock()
        {
            IsLocked = false;
        }
    }
}