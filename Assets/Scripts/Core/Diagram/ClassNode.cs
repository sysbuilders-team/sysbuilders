using System.Collections.Generic;

namespace Core.Diagram
{
    public class ClassNode
    {
        public string Name { get; private set; }
        private readonly List<MethodData> _methods = new List<MethodData>();
        public IReadOnlyList<MethodData> Methods => _methods;
        public bool IsLocked { get; private set; }

        public ClassNode(string name)
        {
            Name = name;
            IsLocked = false;
        }

        public bool Rename(string newName)
        {
            if (IsLocked || string.IsNullOrWhiteSpace(newName))
            {
                return false;
            }

            Name = newName.Trim();
            return true;
        }

        public void AddMethod(MethodData method)
        {
            if (!IsLocked && method != null)
            {
                _methods.Add(method);
            }
        }

        public bool RemoveMethod(MethodData method)
        {
            if (!IsLocked && method != null)
            {
                return _methods.Remove(method);
            }
            return false;
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