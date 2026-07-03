namespace GDScriptFormatter
{
    /// <summary>
    /// Per-line analysis information: whether the line ends with colon/brace, whether it is a continuation, and its original indentation depth.
    /// </summary>
    internal struct LineAnalysis
    {
        /// <summary>Whether the line ends with a colon (not inside brackets).</summary>
        public bool ColonTerminated;

        /// <summary>Whether the line ends with { (not inside a string/comment) and the brace is not closed on the same line.</summary>
        public bool BraceTerminated;

        /// <summary>Whether the line starts with } (close-brace line).</summary>
        public bool IsCloseBrace;

        /// <summary>Whether the line is a continuation (bracket depth &gt; 0 or the previous line ended with \).</summary>
        public bool IsContinuation;

        /// <summary>The line's original indentation level (leading spaces / IndentSize).</summary>
        public int OriginalDepth;

        /// <summary>The bracket depth at the start of this line, before processing any brackets on this line.
        /// Used to distinguish outermost continuation closing brackets (depth 1 → drop to parent indent)
        /// from nested continuation closing brackets (depth &gt; 1 → keep continuation indent).</summary>
        public int StartBracketDepth;

        /// <summary>The bracket depth at the end of this line, after processing all brackets on this line.
        /// Used to determine whether a continuation line starting with a closing bracket should keep
        /// continuation indent (EndBracketDepth > 0 means still inside nested brackets). This is
        /// stable across formatting passes even when synthetic parentheses are introduced by line
        /// splitting, unlike StartBracketDepth which shifts when wrapping parentheses are added.</summary>
        public int EndBracketDepth;
    }
}
