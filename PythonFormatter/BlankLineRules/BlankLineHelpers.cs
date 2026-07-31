namespace PythonFormatter
{
    /// <summary>
    /// Shared predicate helpers used by the per-rule blank-line partial
    /// classes under <c>BlankLineRules/</c>.
    /// </summary>
    internal static class BlankLineHelpers
    {
        /// <summary>
        /// Computes the tab-expanded visible width of the leading
        /// whitespace run of a line, where each tab counts as 4
        /// spaces.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <returns>The visible width of the leading whitespace.</returns>
        public static int ComputeIndentWidth(string line)
        {
            int width = 0;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == ' ')
                {
                    width++;
                }
                else if (c == '\t')
                {
                    width += 4;
                }
                else
                {
                    break;
                }
            }

            return width;
        }

        /// <summary>
        /// Determines whether the trimmed line is a comment line
        /// (starts with <c>#</c>).
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a comment; otherwise false.</returns>
        public static bool IsCommentLine(string trimmed)
        {
            return trimmed.Length > 0 && trimmed[0] == '#';
        }

        /// <summary>
        /// Determines whether the current non-blank entry is at the
        /// top of its indentation level. A line is at the top if its
        /// previous non-blank line is at a strictly smaller indent
        /// (a dedent) or if there is no previous non-blank line.
        /// </summary>
        /// <param name="entry">The current non-blank entry.</param>
        /// <returns>True if the entry is at the top of its indent
        /// level; otherwise false.</returns>
        public static bool IsAtTopOfIndentLevel(PythonNonBlankEntry entry)
        {
            return entry.PrevIndent < 0 || entry.PrevIndent < entry.Indent;
        }

        /// <summary>
        /// Determines whether the current non-blank entry is at the
        /// bottom of its indentation level. A line is at the bottom if
        /// the next non-blank line is at a strictly smaller or equal
        /// indent, or if there is no next non-blank line.
        /// </summary>
        /// <param name="entry">The current non-blank entry.</param>
        /// <param name="nextEntry">The next non-blank entry, or null
        /// if there is no next non-blank line.</param>
        /// <returns>True if the entry is at the bottom of its indent
        /// level; otherwise false.</returns>
        public static bool IsAtBottomOfIndentLevel(PythonNonBlankEntry entry,
            PythonNonBlankEntry? nextEntry)
        {
            if (nextEntry == null)
            {
                return true;
            }

            return nextEntry.Value.Indent <= entry.Indent;
        }
    }
}
