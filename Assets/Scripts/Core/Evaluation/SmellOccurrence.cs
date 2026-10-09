using System;
using System.Collections.Generic;

namespace Core.Evaluation
{
    public class SmellOccurrence
    {
        public CodeSmellType Type { get; }
        public IReadOnlyList<string> AffectedClassNames { get; }
        public int Severity { get; }

        public SmellOccurrence(CodeSmellType type, IReadOnlyList<string> affectedClassNames, int severity = 1)
        {
            if (affectedClassNames == null || affectedClassNames.Count == 0)
            {
                throw new ArgumentException("A smell occurrence must affect at least one class.", nameof(affectedClassNames));
            }

            if (type == CodeSmellType.DuplicateCode && affectedClassNames.Count < 2)
            {
                throw new ArgumentException("DuplicateCode requires at least two affected classes.", nameof(affectedClassNames));
            }

            if (severity < 1)
            {
                throw new ArgumentException("Severity must be at least 1.", nameof(severity));
            }

            Type = type;
            // Copia defensiva: a colecao exposta como IReadOnlyList nao pode continuar mutavel por quem a passou.
            AffectedClassNames = new List<string>(affectedClassNames);
            Severity = severity;
        }
    }
}