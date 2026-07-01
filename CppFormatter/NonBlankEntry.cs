namespace CppFormatter
{
    /// <summary>
    /// Non-blank line entry: records whether there was a blank line above
    /// in the original and whether the line is inside a multi-line token.
    /// </summary>
    internal readonly struct NonBlankEntry
    {
        /// <summary>Whether a blank line existed above this line in the original input.</summary>
        public bool HadBlankAbove
        {
            get;
        }

        /// <summary>The line text.</summary>
        public string Line
        {
            get;
        }

        /// <summary>Whether the line is inside a multi-line string or comment token.</summary>
        public bool IsProtected
        {
            get;
        }

        /// <summary>
        /// Creates a new NonBlankEntry.
        /// </summary>
        /// <param name="hadBlankAbove">Whether a blank line existed above this line in the original input.</param>
        /// <param name="line">The line text.</param>
        /// <param name="isProtected">Whether the line is inside a multi-line string or comment token.</param>
        public NonBlankEntry(bool hadBlankAbove, string line,
            bool isProtected)
        {
            HadBlankAbove = hadBlankAbove;
            Line = line;
            IsProtected = isProtected;
        }
    }
}
