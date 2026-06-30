using System.Collections.Generic;

namespace GDScriptFormatter
{
    /// <summary>
    /// Encapsulates line-continuation detection logic. A line is considered a continuation
    /// when the bracket depth (parentheses, square brackets, braces) from the previous line
    /// is greater than zero, or when the previous line ends with a backslash that is located
    /// in a Code region (i.e., not inside a string literal or comment).
    /// </summary>
    internal static class LineContinuationAnalyzer
    {
        /// <summary>
        /// Determines whether the line at <paramref name="lineIndex"/> is a continuation
        /// of the previous line, based on bracket depth and/or a trailing backslash.
        /// </summary>
        /// <param name="lineIndex">The index of the line to check.</param>
        /// <param name="parenBracketDepth">The current bracket depth before processing this line.</param>
        /// <param name="lineContinuesNext">Per-line flags indicating whether each line ends with a continuation indicator.</param>
        /// <param name="text">The full text.</param>
        /// <param name="isCode">The code mask of the text.</param>
        /// <param name="lineStarts">The starting offsets of each line in text.</param>
        /// <param name="lines">The list of lines.</param>
        /// <returns>True if the line is a continuation of the previous line.</returns>
        internal static bool IsContinuation(int lineIndex,
            int parenBracketDepth,
            bool[] lineContinuesNext, string text, bool[] isCode,
            int[] lineStarts, List<string> lines)
        {
            if (parenBracketDepth > 0)
            {
                return true;
            }

            if (lineIndex > 0 && EndsWithBackslash(text, isCode,
                lineStarts[lineIndex - 1], lines[lineIndex - 1].Length))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the line occupying [lineStart, lineStart+lineLength) in text ends with
        /// a continuation backslash that is located in a Code region. Backslashes inside comments or
        /// string literals do not trigger continuation. A doubled backslash (\\) in Code is treated
        /// as a non-continuation to preserve prior behavior.
        /// </summary>
        /// <param name="text">The full text.</param>
        /// <param name="isCode">The code mask of text.</param>
        /// <param name="lineStart">The starting offset of the line in text.</param>
        /// <param name="lineLength">The length of the line (excluding the line terminator).</param>
        /// <returns>True if the line ends with a Code-region continuation backslash.</returns>
        internal static bool EndsWithBackslash(string text, bool[] isCode,
            int lineStart, int lineLength)
        {
            int lastIdx = -1;

            for (int i = lineStart + lineLength - 1; i >= lineStart; i--)
            {
                if (i >= text.Length)
                {
                    continue;
                }

                char c = text[i];

                if (c != ' ' && c != '\t')
                {
                    lastIdx = i;
                    break;
                }
            }

            if (lastIdx < 0)
            {
                return false;
            }

            if (lastIdx >= isCode.Length || !isCode[lastIdx])
            {
                return false;
            }

            if (text[lastIdx] != '\\')
            {
                return false;
            }

            if (lastIdx > lineStart && text[lastIdx - 1] == '\\' &&
                lastIdx - 1 < isCode.Length && isCode[lastIdx - 1])
            {
                return false;
            }

            return true;
        }
    }
}
