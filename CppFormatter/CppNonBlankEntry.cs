namespace CppFormatter
{
    /// <summary>
    /// Non-blank line entry used by the C++ blank-line rule pipeline.
    /// Captures the line text together with two flags describing its
    /// original context: whether a blank line preceded it in the source
    /// (used by rules that preserve author-inserted blank lines) and
    /// whether the line is fully inside a multi-line string or comment
    /// token (in which case formatting rules must not touch it).
    /// </summary>
    internal readonly struct CppNonBlankEntry
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
        /// Creates a new <see cref="CppNonBlankEntry"/>.
        /// </summary>
        /// <param name="hadBlankAbove">Whether a blank line existed above this line in the original input.</param>
        /// <param name="line">The line text.</param>
        /// <param name="isProtected">Whether the line is inside a multi-line string or comment token.</param>
        public CppNonBlankEntry(
            bool hadBlankAbove,
            string line,
            bool isProtected
        )
        {
            HadBlankAbove = hadBlankAbove;
            Line = line;
            IsProtected = isProtected;
        }
    }
}
