namespace CppFormatter
{
    /// <summary>
    /// Shared constants, data structures, and utility methods used across
    /// all C++ formatting modules.
    /// </summary>
    internal static partial class TextUtils
    {
        /// <summary>
        /// Represents a text insertion point used by brace enforcement.
        /// </summary>
        internal struct Insertion
        {
            /// <summary>The character position at which to insert.</summary>
            public int Position;

            /// <summary>The text to insert.</summary>
            public string Text;

            /// <summary>
            /// Creates a new insertion record.
            /// </summary>
            /// <param name="position">The character position.</param>
            /// <param name="text">The text to insert.</param>
            public Insertion(int position, string text)
            {
                Position = position;
                Text = text;
            }
        }

        /// <summary>
        /// Represents a text replacement range used by enum formatting.
        /// </summary>
        internal struct Replacement
        {
            /// <summary>The start position (inclusive).</summary>
            public int Start;

            /// <summary>The end position (exclusive).</summary>
            public int End;

            /// <summary>The replacement text.</summary>
            public string NewText;

            /// <summary>
            /// Creates a new replacement record.
            /// </summary>
            /// <param name="start">The start position.</param>
            /// <param name="end">The end position.</param>
            /// <param name="newText">The replacement text.</param>
            public Replacement(int start, int end, string newText)
            {
                Start = start;
                End = end;
                NewText = newText;
            }
        }
    }
}
