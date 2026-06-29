using System.Collections.Generic;

namespace JavaFormatter
{
    /// <summary>
    /// Splits lines that exceed the configured maximum length at safe break points.
    /// </summary>
    internal static class LineLengthProcessor
    {
        /// <summary>
        /// Splits lines exceeding the maximum length at safe break points;
        /// continuation lines are indented one level deeper than the statement
        /// base indent.
        /// <paramref name="lineContinuesNext"/> flags whether each line ends
        /// with a continuation indicator; when a line is itself a continuation
        /// of the previous line, its split segments reuse the line's current
        /// indent (no extra level) so that splitting a continuation line does
        /// not cascade into deeper indents on a second pass.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <param name="lineContinuesNext">Per-line flags indicating whether
        /// the line ends with a continuation indicator; entry i corresponds
        /// to line i. May be null when continuation detection is not
        /// available.</param>
        /// <returns>The lines with long lines split.</returns>
        public static List<string> ApplyLineLengthLimit(List<string> lines,
            bool[] lineContinuesNext)
        {
            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                if (line.Length <= Formatter.MaxLineLength)
                {
                    result.Add(line);
                    continue;
                }

                // If this line is itself a continuation of the previous line
                // (previous line ends with a continuation indicator), the
                // continuation indent equals this line's current indent — do
                // NOT add another indent level. Otherwise, continuation
                // segments are indented one level deeper than the statement
                // base indent (handled by passing null to SplitLongLine).
                bool isContinuation = lineContinuesNext != null &&
                    i > 0 && i - 1 < lineContinuesNext.Length &&
                    lineContinuesNext[i - 1];

                string fixedContIndent;

                if (isContinuation)
                {
                    int indentLen = 0;

                    while (indentLen < line.Length &&
                        line[indentLen] == ' ')
                    {
                        indentLen++;
                    }

                    fixedContIndent = line.Substring(0, indentLen);
                }

                else
                {
                    fixedContIndent = null;
                }

                var split = SplitLongLine(line, fixedContIndent);
                result.AddRange(split);
            }

            return result;
        }

        /// <summary>
        /// Recursively splits a single line so each segment does not exceed the
        /// maximum length; only breaks at Code token boundaries. Never breaks
        /// inside String/TextBlock/Char/Comment tokens. If no safe break point
        /// is found, the original line is preserved.
        /// <paramref name="fixedContIndent"/> is the fixed continuation indent
        /// reused across all continuation segments so that 3+ segment splits do
        /// not cascade; pass null on the first call to trigger computation
        /// from the original line's indent.
        /// </summary>
        /// <param name="line">The line to split.</param>
        /// <param name="fixedContIndent">The fixed continuation indent, or null
        /// to compute from the line's indent on the first split.</param>
        /// <returns>The list of split segments.</returns>
        private static List<string> SplitLongLine(string line,
            string fixedContIndent)
        {
            if (line.Length <= Formatter.MaxLineLength)
            {
                return new List<string> { line };
            }

            int indentLen = 0;

            while (indentLen < line.Length && line[indentLen] == ' ')
            {
                indentLen++;
            }

            if (indentLen >= line.Length)
            {
                return new List<string> { line };
            }

            string indent = line.Substring(0, indentLen);
            // On the first call (fixedContIndent == null), compute the fixed
            // continuation indent from the original line's indent. This indent
            // is reused for ALL continuation segments so that 3+ segment
            // splits do not cascade (parent+4 for every continuation line,
            // matching IndentationProcessor's behaviour).

            if (fixedContIndent == null)
            {
                fixedContIndent = indent +
                    new string(' ', Formatter.IndentSize);
            }

            var tokens = Tokenizer.Tokenize(line);
            bool[] isCode = Tokenizer.BuildCodeMask(line, tokens);
            int breakAt = FindSafeBreakPoint(line, isCode, indentLen);

            if (breakAt < 0 || breakAt >= line.Length)
            {
                return new List<string> { line };
            }

            string first = line.Substring(0, breakAt).TrimEnd();
            string rest = fixedContIndent + line.Substring(breakAt).TrimStart();

            if (first.Length == 0 || first.Length >= line.Length)
            {
                return new List<string> { line };
            }

            var result = new List<string> { first };
            result.AddRange(SplitLongLine(rest, fixedContIndent));
            return result;
        }

        /// <summary>
        /// Finds a safe break point within Code tokens: prefers the latest break
        /// point that does not exceed the maximum length; if none, returns the
        /// first break point beyond the maximum length. Breaks after operators:
        /// , ; + - * / % == != &lt; &gt; &lt;= &gt;= = += -= &amp;&amp; ||.
        /// Does NOT break at . (member access).
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="startIdx">The scan start position.</param>
        /// <returns>The break point index, or -1 if none found.</returns>
        private static int FindSafeBreakPoint(string line, bool[] isCode,
            int startIdx)
        {
            int bestInRange = -1;
            int firstOutOfRange = -1;
            int i = startIdx;

            while (i < line.Length)
            {
                if (!isCode[i])
                {
                    i++;
                    continue;
                }

                char c = line[i];
                int bp = -1;
                int extra = 0;

                if (i + 1 < line.Length && IsDoubleOpBreak(c, line[i + 1]))
                {
                    bp = i + 2;
                    extra = 1;
                }

                else if (c == ',')
                {
                    bp = i + 1;
                }

                else if (c == ';' && i + 1 < line.Length)
                {
                    bp = i + 1;
                }

                else if (i > startIdx && IsBinaryOpContext(line, i, startIdx) &&
                    (c == '+' || c == '-' || c == '*' || c == '/' ||
                    c == '%' || c == '<' || c == '>'))
                {
                    bp = i + 1;
                }

                else if (c == '=' && i > startIdx &&
                    IsBinaryOpContext(line, i, startIdx) &&
                    (i + 1 >= line.Length || line[i + 1] != '='))
                {
                    bp = i + 1;
                }

                if (bp > 0)
                {
                    if (bp <= Formatter.MaxLineLength)
                    {
                        bestInRange = bp;
                    }

                    else if (firstOutOfRange < 0)
                    {
                        firstOutOfRange = bp;
                    }
                }

                i += 1 + extra;
            }

            if (bestInRange > 0)
            {
                return bestInRange;
            }

            return firstOutOfRange;
        }

        /// <summary>
        /// Determines whether line[i] is in a binary operator context: the previous
        /// non-whitespace character is ), ], an identifier character, _, or ".
        /// Used to exclude unary operators and generic type parameters.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="i">The current operator position.</param>
        /// <param name="startIdx">The scan start position.</param>
        /// <returns>True if in a binary operator context; otherwise false.</returns>
        private static bool IsBinaryOpContext(string line, int i, int startIdx)
        {
            int prev = i - 1;

            while (prev >= startIdx && line[prev] == ' ')
            {
                prev--;
            }

            if (prev < startIdx)
            {
                return false;
            }

            char pc = line[prev];

            return pc == ')' || pc == ']' || char.IsLetterOrDigit(pc) ||
                pc == '_' || pc == '"';
        }

        /// <summary>
        /// Determines whether the character pair forms a two-character breakable
        /// operator (==, !=, &lt;=, &gt;=, +=, -=, &amp;&amp;, ||).
        /// </summary>
        /// <param name="c">The first character.</param>
        /// <param name="next">The second character.</param>
        /// <returns>True if the pair is a breakable two-character operator.</returns>
        private static bool IsDoubleOpBreak(char c, char next)
        {
            switch (c)
            {
                case '=':
                case '!':
                case '<':
                case '>':
                case '+':
                case '-':
                    return next == '=';
                case '&':
                    return next == '&';
                case '|':
                    return next == '|';
                default:
                    return false;
            }
        }
    }
}
