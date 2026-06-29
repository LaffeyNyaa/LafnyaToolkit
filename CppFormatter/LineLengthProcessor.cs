using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Splits lines exceeding the maximum length at safe token boundaries.
    /// </summary>
    internal static class LineLengthProcessor
    {
        /// <summary>Two-char operators whose break point sits right after
        /// the operator. Excludes &lt;&lt;/&gt;&gt; (stream ops need
        /// IsStreamOpContext) and single-char ops.</summary>
        private static readonly string[] TwoCharBreakOps =
            { "==", "!=", "<=", ">=", "=>", "+=", "-=", "&&", "||" };

        /// <summary>
        /// Splits lines exceeding 80 characters at safe token boundaries;
        /// continuation lines are indented one extra level. Lines entirely
        /// inside a multi-line string or comment token are preserved verbatim
        /// and never split.
        /// <paramref name="lineContinuesNext"/> flags whether each line ends
        /// with a continuation indicator; when a line is itself a continuation
        /// of the previous line, its split segments reuse the line's current
        /// indent (no extra level) so that splitting a continuation line does
        /// not cascade into deeper indents on a second pass.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full source text corresponding to
        /// <paramref name="lines"/>.</param>
        /// <param name="lineContinuesNext">Per-line flags indicating whether
        /// the line ends with a continuation indicator; entry i corresponds
        /// to line i. May be null when continuation detection is not
        /// available.</param>
        /// <returns>The processed line list.</returns>
        internal static List<string> ApplyLineLengthLimit(List<string> lines,
            string text, bool[] lineContinuesNext)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] protectedLines = Tokenizer.ComputeProtectedLines(text,
                tokens, lines.Count);
            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                if (i < protectedLines.Length && protectedLines[i])
                {
                    result.Add(lines[i]);
                    continue;
                }

                string line = lines[i];

                if (line.Length <= TextUtils.MaxLineLength)
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
        /// Recursively splits a single line so that each segment does not
        /// exceed 80 characters. <paramref name="fixedContIndent"/> is the
        /// fixed continuation indent reused across all continuation segments
        /// so that 3+ segment splits do not cascade; pass null on the first
        /// call to trigger computation from the original line's indent.
        /// </summary>
        private static List<string> SplitLongLine(string line,
            string fixedContIndent)
        {
            if (line.Length <= TextUtils.MaxLineLength)
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
            // matching Reindent's behaviour).
            if (fixedContIndent == null)
            {
                fixedContIndent = indent + new string(' ',
                    TextUtils.IndentSize);
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
        /// Finds a safe break point within Code tokens. Additionally supports &lt;&lt; and &gt;&gt; beyond the C# break point set.
        /// </summary>
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

                if (i + 1 < line.Length)
                {
                    string pair = line.Substring(i, 2);
                    foreach (var op in TwoCharBreakOps)
                    {
                        if (pair == op)
                        {
                            bp = i + 2;
                            i++;
                            break;
                        }
                    }
                }

                if (bp < 0 && c == '<' && i + 1 < line.Length &&
                    line[i + 1] == '<' &&
                    IsStreamOpContext(line, i, startIdx))
                {
                    bp = i + 2;
                    i++;
                }

                else if (bp < 0 && c == '>' && i + 1 < line.Length &&
                    line[i + 1] == '>' &&
                    IsStreamOpContext(line, i, startIdx))
                {
                    bp = i + 2;
                    i++;
                }

                else if (bp < 0 && c == ',')
                {
                    bp = i + 1;
                }

                else if (bp < 0 && c == ';')
                {
                    if (i + 1 < line.Length)
                    {
                        bp = i + 1;
                    }
                }

                else if (bp < 0 && i > startIdx &&
                    IsBinaryOpContext(line, i, startIdx) &&
                    (c == '+' || c == '-' || c == '*' || c == '/' ||
                    c == '%' || c == '<' || c == '>'))
                {
                    bp = i + 1;
                }

                else if (bp < 0 && c == '=' && i > startIdx &&
                    IsBinaryOpContext(line, i, startIdx) &&
                    (i + 1 >= line.Length || (line[i + 1] != '=' &&
                    line[i + 1] != '>')))
                {
                    bp = i + 1;
                }

                if (bp > 0)
                {
                    if (bp <= TextUtils.MaxLineLength)
                    {
                        bestInRange = bp;
                    }

                    else if (firstOutOfRange < 0)
                    {
                        firstOutOfRange = bp;
                    }
                }

                i++;
            }

            if (bestInRange > 0)
            {
                return bestInRange;
            }

            return firstOutOfRange;
        }

        /// <summary>
        /// Determines whether a position with &lt;&lt; or &gt;&gt; is in a stream operator context:
        /// i.e., the preceding non-whitespace character is ), ], an identifier character, _, " or '.
        /// This avoids breaking inside template parameter lists (e.g., vector&lt;vector&lt;int&gt;&gt;).
        /// </summary>
        private static bool IsStreamOpContext(string line, int i,
            int startIdx)
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
                pc == '_' || pc == '"' || pc == '\'';
        }

        /// <summary>
        /// Determines whether the position at line[i] is in a binary operator context.
        /// </summary>
        private static bool IsBinaryOpContext(string line, int i,
            int startIdx)
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
    }
}
