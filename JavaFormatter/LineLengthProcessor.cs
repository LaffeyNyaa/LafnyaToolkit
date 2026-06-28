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
        /// continuation lines are indented one level deeper than the statement base indent.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <returns>The lines with long lines split.</returns>
        public static List<string> ApplyLineLengthLimit(List<string> lines)
        {
            var result = new List<string>(lines.Count);

            foreach (var line in lines)
            {
                if (line.Length <= Formatter.MaxLineLength)
                {
                    result.Add(line);
                    continue;
                }

                var split = SplitLongLine(line);
                result.AddRange(split);
            }

            return result;
        }

        /// <summary>
        /// Recursively splits a single line so each segment does not exceed the
        /// maximum length; only breaks at Code token boundaries. Never breaks
        /// inside String/TextBlock/Char/Comment tokens. If no safe break point
        /// is found, the original line is preserved.
        /// </summary>
        /// <param name="line">The line to split.</param>
        /// <returns>The list of split segments.</returns>
        private static List<string> SplitLongLine(string line)
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
            string contIndent = indent + new string(' ', Formatter.IndentSize);
            var tokens = Tokenizer.Tokenize(line);
            bool[] isCode = Tokenizer.BuildCodeMask(line, tokens);
            int breakAt = FindSafeBreakPoint(line, isCode, indentLen);

            if (breakAt < 0 || breakAt >= line.Length)
            {
                return new List<string> { line };
            }

            string first = line.Substring(0, breakAt).TrimEnd();
            string rest = contIndent + line.Substring(breakAt).TrimStart();

            if (first.Length == 0 || first.Length >= line.Length)
            {
                return new List<string> { line };
            }

            var result = new List<string> { first };
            result.AddRange(SplitLongLine(rest));
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
