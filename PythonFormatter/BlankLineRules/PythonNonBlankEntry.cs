namespace PythonFormatter
{
    /// <summary>
    /// A single non-blank line entry passed to blank-line rule
    /// processors. Carries the line text together with the original
    /// line index, the original indent, the indent of the previous
    /// non-blank line, the indent of the enclosing <c>def</c> (or 0
    /// if not inside a <c>def</c>), and a flag indicating whether a
    /// blank line preceded the entry in the source.
    /// </summary>
    internal struct PythonNonBlankEntry
    {
        /// <summary>Whether a blank line existed above this line in the input.</summary>
        public bool HadBlankAbove;

        /// <summary>The line text.</summary>
        public string Line;

        /// <summary>The original index of the line in the input list.</summary>
        public int OriginalIndex;

        /// <summary>The leading whitespace of this line in spaces (after tab expansion).</summary>
        public int Indent;

        /// <summary>
        /// The leading whitespace of the previous non-blank line in
        /// spaces, or -1 if there is no previous non-blank line.
        /// </summary>
        public int PrevIndent;

        /// <summary>
        /// The leading whitespace of the enclosing <c>def</c> body
        /// (i.e. the indent of the most recent preceding <c>def</c>
        /// keyword line), or 0 if there is no enclosing <c>def</c>.
        /// Used by the method-blank and block-blank rules to compare
        /// two <c>def</c> statements at the same level even when a
        /// body line is interleaved between them.
        /// </summary>
        public int DefIndent;

        /// <summary>
        /// Creates a new entry.
        /// </summary>
        /// <param name="hadBlankAbove">Whether a blank line preceded
        /// it.</param>
        /// <param name="line">The line text.</param>
        /// <param name="originalIndex">The original line index.</param>
        /// <param name="indent">The leading whitespace of this line in
        /// spaces.</param>
        /// <param name="prevIndent">The indent of the previous non-blank
        /// line, or -1.</param>
        /// <param name="defIndent">The indent of the enclosing
        /// <c>def</c>, or 0.</param>
        public PythonNonBlankEntry(bool hadBlankAbove, string line,
            int originalIndex, int indent, int prevIndent, int defIndent)
        {
            HadBlankAbove = hadBlankAbove;
            Line = line;
            OriginalIndex = originalIndex;
            Indent = indent;
            PrevIndent = prevIndent;
            DefIndent = defIndent;
        }
    }
}
