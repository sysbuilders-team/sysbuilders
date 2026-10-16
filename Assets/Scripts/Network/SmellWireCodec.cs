using System;
using System.Collections.Generic;
using Core.Evaluation;

namespace Network
{
    /// <summary>
    /// Wire codec for SmellOccurrence arrays over Mirror commands.
    ///
    /// Mirror can only marshal primitives, so a SmellOccurrence travels as
    /// three parallel arrays: type ids, severities, and affected class names
    /// joined by a separator character. This class is the only place that
    /// knows the wire shape.
    ///
    /// Extracted from MatchSyncBridge so the format can be tested without
    /// reaching into a NetworkBehaviour through reflection.
    /// </summary>
    public static class SmellWireCodec
    {
        /// <summary>Upper bound on smells transmitted per evaluation result.</summary>
        public const int MaxSmellsPerResult = 50;

        /// <summary>
        /// ASCII unit separator. Chosen because it cannot appear in a class
        /// name (SanitizeShortText keeps only letters, digits, space, '-' and
        /// '_'), so it can never collide with a real value.
        /// </summary>
        public const char ClassSeparator = '\u001f';

        public static void Encode(
            IReadOnlyList<SmellOccurrence> smells,
            out int[] types,
            out int[] severities,
            out string[] classes)
        {
            if (smells == null)
            {
                types = Array.Empty<int>();
                severities = Array.Empty<int>();
                classes = Array.Empty<string>();
                return;
            }

            types = new int[smells.Count];
            severities = new int[smells.Count];
            classes = new string[smells.Count];

            for (int i = 0; i < smells.Count; i++)
            {
                types[i] = (int)smells[i].Type;
                severities[i] = smells[i].Severity;
                var affected = smells[i].AffectedClassNames;
                classes[i] = affected != null && affected.Count > 0
                    ? string.Join(ClassSeparator.ToString(), affected)
                    : string.Empty;
            }
        }

        public static List<SmellOccurrence> Decode(int[] types, int[] severities, string[] classes)
        {
            var smells = new List<SmellOccurrence>();
            if (types == null || severities == null || classes == null) return smells;

            int count = Math.Min(
                Math.Min(types.Length, Math.Min(severities.Length, classes.Length)),
                MaxSmellsPerResult);

            for (int i = 0; i < count; i++)
            {
                try
                {
                    var names = string.IsNullOrEmpty(classes[i])
                        ? Array.Empty<string>()
                        : classes[i].Split(ClassSeparator);

                    smells.Add(new SmellOccurrence((CodeSmellType)types[i], names, Math.Max(1, severities[i])));
                }
                catch (ArgumentException)
                {
                    // Invalid data from the network: skip this occurrence rather
                    // than aborting the whole decode.
                }
            }

            return smells;
        }
    }
}
