using System.Collections.Generic;
using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace CSharpFormatter
{
    /// <summary>
    /// Splits lines exceeding the maximum length at safe token
    /// boundaries.
    /// </summary>
    internal sealed class LineLengthProcessor
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly LineLengthProcessor Instance = new LineLengthProcessor();

        private LineLengthProcessor()
        {
        }

        /// <summary>
        /// Splits lines exceeding 80 characters at safe token
        /// boundaries; continuation lines are indented one extra
        /// level. <paramref name="lineContinuesNext"/> flags whether
        /// each line ends with a continuation indicator; when a line
        /// is itself a continuation of the previous line, its split
        /// segments reuse the line's current indent (no extra level)
        /// so that splitting a continuation line does not cascade
        /// into deeper indents on a second pass.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="lineContinuesNext">Per-line flags indicating whether the line ends with a continuation indicator; entry i corresponds to line i. May be null when continuation detection is not available.</param>
        /// <returns>The processed line list.</returns>
        public List<string> ApplyLineLengthLimit(
            List<string> lines, bool[] lineContinuesNext)
        {
            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                if (line.Length <= TextUtils.MaxLineLength)
                {
                    result.Add(line);
                    continue;
                }

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
        /// Recursively splits a single line so that each segment does
        /// not exceed 80 characters; splits only at Code token
        /// boundaries. <paramref name="fixedContIndent"/> is the
        /// fixed continuation indent reused across all continuation
        /// segments so that 3+ segment splits do not cascade; pass
        /// null on the first call to trigger computation from the
        /// original line's indent.
        /// </summary>
        /// <param name="line">The line to split.</param>
        /// <param name="fixedContIndent">The fixed continuation indent (or null on the first call).</param>
        /// <returns>The list of split segments.</returns>
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

            if (fixedContIndent == null)
            {
                fixedContIndent = indent +
                    new string(' ', TextUtils.IndentSize);
            }

            var tokens = CSharpTokenizer.Instance.Tokenize(line);
            bool[] isCode = CSharpTokenizer.Instance.BuildCodeMask(line, tokens);
            int breakAt = FindSafeBreakPoint(line, isCode, indentLen);

            if (breakAt < 0 || breakAt >= line.Length)
            {
                return new List<string> { line };
            }

            string afterTrimmed = line.Substring(breakAt).TrimStart();

            if (afterTrimmed.StartsWith("//") ||
                afterTrimmed.StartsWith("/*") ||
                afterTrimmed.StartsWith("*"))
            {
                fixedContIndent = indent;
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
        /// Finds a safe break point within a Code token: prefers the
        /// largest break point not exceeding 80 characters; if no
        /// such point exists, returns the first break point beyond
        /// 80 characters.
        /// </summary>
        /// <param name="line">The line to scan.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="startIdx">The position to start scanning from.</param>
        /// <returns>The break position, or -1 if none exists.</returns>
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

                if ((c == '+' || c == '-') && i + 1 < line.Length &&
                    line[i + 1] == c)
                {
                    i += 2;
                    continue;
                }

                int bp = -1;
                bp = TryMatchTwoCharOperator(line, i, c);

                if (bp < 0 && c == ',')
                {
                    bp = i + 1;
                }

                if (bp < 0 && c == ';' && i + 1 < line.Length)
                {
                    bp = i + 1;
                }

                if (bp < 0 && i > startIdx)
                {
                    bp = TryMatchSingleCharOp(line, i, c, startIdx);
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
        /// Attempts to match a two-character operator at position
        /// <paramref name="i"/> and returns the break position after
        /// it.
        /// </summary>
        /// <param name="line">The line to scan.</param>
        /// <param name="i">The position of the first character of the operator.</param>
        /// <param name="c">The first character of the operator.</param>
        /// <returns>The break position after the operator, or -1 if not matched.</returns>
        private static int TryMatchTwoCharOperator(string line, int i,
            char c)
        {
            if (i + 1 >= line.Length)
            {
                return -1;
            }

            char next = line[i + 1];

            if (c == '=' && (next == '=' || next == '>'))
            {
                return i + 2;
            }

            if (c == '!' && next == '=')
            {
                return i + 2;
            }

            if (c == '<' && next == '=')
            {
                return i + 2;
            }

            if (c == '>' && next == '=')
            {
                return i + 2;
            }

            if (c == '+' && next == '=')
            {
                return i + 2;
            }

            if (c == '-' && next == '=')
            {
                return i + 2;
            }

            if (c == '&' && next == '&')
            {
                return i + 2;
            }

            if (c == '|' && next == '|')
            {
                return i + 2;
            }

            return -1;
        }

        /// <summary>
        /// Attempts to match a single-character binary operator at
        /// position <paramref name="i"/>.
        /// </summary>
        /// <param name="line">The line to scan.</param>
        /// <param name="i">The position of the operator.</param>
        /// <param name="c">The operator character.</param>
        /// <param name="startIdx">The position to start scanning from.</param>
        /// <returns>The break position after the operator, or -1 if not matched.</returns>
        private static int TryMatchSingleCharOp(string line, int i, char c,
            int startIdx)
        {
            bool isBinaryChar = c == '+' || c == '-' || c == '*' ||
                c == '/' || c == '%' || c == '<' || c == '>';

            if (isBinaryChar && IsBinaryOpContext(line, i, startIdx))
            {
                return i + 1;
            }

            if (c == '=' && IsBinaryOpContext(line, i, startIdx) &&
                (i + 1 >= line.Length ||
                (line[i + 1] != '=' && line[i + 1] != '>')))
            {
                return i + 1;
            }

            return -1;
        }

        /// <summary>
        /// Determines whether position <paramref name="i"/> in the
        /// line is in a binary operator context (preceded by
        /// <c>)</c>, <c>]</c>, identifier, <c>_</c>, or <c>"</c>).
        /// </summary>
        /// <param name="line">The line to inspect.</param>
        /// <param name="i">The position of the candidate operator.</param>
        /// <param name="startIdx">The position to start scanning from.</param>
        /// <returns>True if the operator is in a binary context.</returns>
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
