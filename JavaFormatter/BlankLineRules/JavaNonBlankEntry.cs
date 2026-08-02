namespace JavaFormatter
{
    /// <summary>
    /// A single non-blank line entry passed to blank-line rule
    /// processors. Carries the line text together with the original
    /// line index and a flag indicating whether a blank line preceded
    /// the entry in the source.
    /// </summary>
    internal struct JavaNonBlankEntry
    {
        /// <summary>Whether a blank line existed above this line in the input.</summary>
        public bool HadBlankAbove;

        /// <summary>The line text.</summary>
        public string Line;

        /// <summary>The original index of the line in the input list.</summary>
        public int OriginalIndex;

        /// <summary>
        /// Creates a new entry.
        /// </summary>
        /// <param name="hadBlankAbove">Whether a blank line preceded
        /// it.</param>
        /// <param name="line">The line text.</param>
        /// <param name="originalIndex">The original line index.</param>
        public JavaNonBlankEntry(
            bool hadBlankAbove,
            string line,
            int originalIndex
        )
        {
            HadBlankAbove = hadBlankAbove;
            Line = line;
            OriginalIndex = originalIndex;
        }
    }
}
