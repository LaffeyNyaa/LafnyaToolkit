namespace GDScriptFormatter
{
    /// <summary>
    /// Non-blank line entry: records whether there was a blank line above in the original and the indentation level.
    /// </summary>
    internal struct NonBlankEntry
    {
        /// <summary>Whether a blank line existed above this line in the original input.</summary>
        public bool HadBlankAbove;

        /// <summary>The line text.</summary>
        public string Line;

        /// <summary>The indentation level.</summary>
        public int Indent;

        /// <summary>Whether this line is a continuation of the previous line.</summary>
        public bool IsContinuation;

        public NonBlankEntry(bool hadBlankAbove, string line, int indent,
            bool isContinuation)
        {
            HadBlankAbove = hadBlankAbove;
            Line = line;
            Indent = indent;
            IsContinuation = isContinuation;
        }
    }
}
