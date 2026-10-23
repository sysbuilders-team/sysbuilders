using System.Text;

namespace Core.Diagram
{
    /// <summary>
    /// Sanitizer for class names. The rules here match what the wire can carry:
    ///   - at most 32 characters
    ///   - letters, digits, space, hyphen, underscore
    ///   - consecutive spaces collapsed
    ///   - leading/trailing whitespace trimmed
    ///   - null/whitespace or all-invalid input becomes null
    ///
    /// Used by DiagramEditor so client-side names can never be richer than what
    /// the wire accepts, and by SmellWireCodec.Decode so names arriving from a
    /// modified client are bounded before they reach TMP text fields.
    /// </summary>
    public static class ClassNameRules
    {
        public const int MaxLength = 32;

        /// <summary>Returns the sanitized name, or null when nothing usable survives.</summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var builder = new StringBuilder(MaxLength);
            bool lastWasSpace = false;

            foreach (char c in raw)
            {
                if (builder.Length >= MaxLength) break;

                bool allowed = char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_';
                if (!allowed) continue;

                if (c == ' ')
                {
                    if (lastWasSpace) continue;
                    lastWasSpace = true;
                }
                else
                {
                    lastWasSpace = false;
                }

                builder.Append(c);
            }

            string trimmed = builder.ToString().Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
