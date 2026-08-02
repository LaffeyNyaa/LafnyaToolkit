namespace LafnyaToolkit.Core.Text
{
    /// <summary>
    /// A single non-blank line entry passed to blank-line rule processors.
    /// Carries the line text together with its position in the source and a
    /// flag indicating whether the entry is the first non-blank line of a
    /// logical block (function, class, control-flow block, etc.).
    /// </summary>
    public struct NonBlankEntry
    {
        /// <summary>The line text without trailing whitespace.</summary>
        public string Line;

        /// <summary>The index of the line in the original line list.</summary>
        public int Index;

        /// <summary>True if this entry is the first non-blank line of a logical block.</summary>
        public bool IsBlockStart;

        /// <summary>
        /// Creates a new non-blank entry.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="index">The line index.</param>
        /// <param name="isBlockStart">Whether the entry starts a logical block.</param>
        public NonBlankEntry(
            string line,
            int index,
            bool isBlockStart
        )
        {
            Line = line;
            Index = index;
            IsBlockStart = isBlockStart;
        }
    }
}
