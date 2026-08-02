namespace JavaFormatter
{
    /// <summary>
    /// Shared predicate helpers used by the per-rule blank-line partial
    /// classes under <c>BlankLineRules/</c>.
    /// </summary>
    internal static class BlankLineHelpers
    {
        /// <summary>
        /// Determines whether the first non-whitespace character of the
        /// line is in a code region.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The line's start offset in the source
        /// text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>True if the first non-whitespace character is code;
        /// otherwise false.</returns>
        public static bool FirstNonWsInCode(
            string line,
            int lineStart,
            bool[] isCode
        )
        {
            int i = 0;

            while (i < line.Length && char.IsWhiteSpace(line[i]))
            {
                i++;
            }

            if (i >= line.Length)
            {
                return false;
            }

            int p = lineStart + i;
            return p >= 0 && p < isCode.Length && isCode[p];
        }

        /// <summary>
        /// Determines whether the last non-whitespace character of the
        /// line is in a code region.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The line's start offset in the source
        /// text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>True if the last non-whitespace character is code;
        /// otherwise false.</returns>
        public static bool LastNonWsInCode(
            string line,
            int lineStart,
            bool[] isCode
        )
        {
            int i = line.Length - 1;

            while (i >= 0 && char.IsWhiteSpace(line[i]))
            {
                i--;
            }

            if (i < 0)
            {
                return false;
            }

            int p = lineStart + i;
            return p >= 0 && p < isCode.Length && isCode[p];
        }

        /// <summary>
        /// Determines whether the trimmed line is a comment line: a
        /// line comment, a block comment start, or a block comment
        /// continuation.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a comment line; otherwise
        /// false.</returns>
        public static bool IsCommentLine(string trimmed)
        {
            return trimmed.StartsWith("//") || trimmed.StartsWith("/*") ||
                trimmed.StartsWith("*");
        }

        /// <summary>
        /// Determines whether the line is a plain single-line
        /// statement: code that ends with a semicolon and is not a
        /// block boundary, do-while tail, or comment.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <param name="startsInCode">Whether the line's first
        /// non-whitespace character is in a code region.</param>
        /// <returns>True if the line is a plain single-line statement;
        /// otherwise false.</returns>
        public static bool IsPlainSingleLineStatement(string trimmed,
            bool startsInCode)
        {
            if (!startsInCode)
            {
                return false;
            }

            if (trimmed.Length == 0 || !trimmed.EndsWith(";"))
            {
                return false;
            }

            if (LineClassifier.Instance.IsBlockEndLine(trimmed))
            {
                return false;
            }

            if (LineClassifier.Instance.IsBlockStartLine(trimmed))
            {
                return false;
            }

            if (LineClassifier.Instance.IsDoWhileTail(trimmed))
            {
                return false;
            }

            if (IsCommentLine(trimmed))
            {
                return false;
            }

            return true;
        }
    }
}
