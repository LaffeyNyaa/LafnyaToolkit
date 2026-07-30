using System.Collections.Generic;
using System.Text;
using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace CppFormatter
{
    /// <summary>
    /// Splits lines exceeding the maximum length at safe token
    /// boundaries. Uses <see cref="OperatorBreakPolicy"/> for the
    /// stream/binary-operator handling. Lines entirely inside a
    /// multi-line string or comment token are preserved verbatim.
    /// Stateless; the shared instance is exposed via
    /// <see cref="Instance"/>.
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
        /// level (except after semicolons, where base indent is used).
        /// Lines entirely inside a multi-line string or comment token
        /// are preserved verbatim and never split. <paramref name="lineContinuesNext"/>
        /// flags whether each line ends with a continuation indicator;
        /// when a line is itself a continuation of the previous line,
        /// its split segments reuse the line's current indent (no
        /// extra level) so that splitting a continuation line does
        /// not cascade into deeper indents on a second pass.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full source text corresponding to <paramref name="lines"/>.</param>
        /// <param name="lineContinuesNext">Per-line flags indicating whether the line ends with a continuation indicator.</param>
        /// <param name="preTokens">Pre-computed tokens of <paramref name="text"/> (optional).</param>
        /// <param name="preIsCode">Pre-computed code mask of <paramref name="text"/> (optional).</param>
        /// <returns>The processed line list.</returns>
        public List<string> ApplyLineLengthLimit(List<string> lines, string text, bool[] lineContinuesNext, List<Token> preTokens = null, bool[] preIsCode = null)
        {
            var tokens = preTokens ?? CppTokenizer.Instance.Tokenize(text);
            bool[] isCode = preIsCode ?? CppTokenizer.Instance.BuildCodeMask(text, tokens);
            bool[] protectedLines = CppTokenizer.Instance.ComputeProtectedLines(text, tokens, lines.Count);

            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                if (i < protectedLines.Length && protectedLines[i])
                {
                    result.Add(lines[i]);
                    continue;
                }

                string trimmedLine = lines[i].TrimStart();

                if (trimmedLine.StartsWith("#"))
                {
                    result.Add(lines[i]);
                    continue;
                }

                string line = lines[i];
                bool isContinuation = lineContinuesNext != null && i > 0 && i - 1 < lineContinuesNext.Length && lineContinuesNext[i - 1];

                if (!isContinuation && TryUnwrapStreamChain(lines, line, i, out var unwrapped) && unwrapped.Length > TextUtils.MaxLineLength)
                {
                    var split = SplitLongLine(unwrapped, null, null);
                    result.AddRange(split);
                    SkipContinuationLines(lines, lineContinuesNext, ref i);
                    continue;
                }

                if (line.Length <= TextUtils.MaxLineLength)
                {
                    result.Add(line);
                    continue;
                }

                string fixedContIndent;

                if (isContinuation)
                {
                    int indentLen = 0;

                    while (indentLen < line.Length && line[indentLen] == ' ')
                    {
                        indentLen++;
                    }

                    fixedContIndent = line.Substring(0, indentLen);
                }
                else
                {
                    fixedContIndent = null;
                }

                var split2 = SplitLongLine(line, fixedContIndent, null);
                result.AddRange(split2);
            }

            return result;
        }

        /// <summary>
        /// Recursively splits a single line so that each segment does
        /// not exceed 80 characters. <paramref name="fixedContIndent"/>
        /// is the fixed continuation indent reused across all
        /// continuation segments so that 3+ segment splits do not
        /// cascade; pass null on the first call to trigger computation
        /// from the original line's indent. <paramref name="baseIndent"/>
        /// is the base indent of the original line; pass null on the
        /// first call, and it will be computed from the leading
        /// whitespace. When a break occurs at a semicolon in a
        /// recursive call, the continuation indent is reset to
        /// baseIndent so that trailing doc comments (<c>/**&lt;</c>)
        /// use the correct indent.
        /// </summary>
        private List<string> SplitLongLine(string line, string fixedContIndent, string baseIndent)
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

            if (baseIndent == null)
            {
                baseIndent = indent;
            }

            var tokens = CppTokenizer.Instance.Tokenize(line);
            bool[] isCode = CppTokenizer.Instance.BuildCodeMask(line, tokens);

            if (OperatorBreakPolicy.Instance.HasStreamOperators(line, indentLen, out var streamPositions))
            {
                var streamResult = OperatorBreakPolicy.Instance.SplitAtStreamOperators(line, streamPositions, fixedContIndent, baseIndent);

                if (streamResult.Count > 0 && streamResult[0].Length > TextUtils.MaxLineLength)
                {
                    var split = SplitLongLine(streamResult[0], null, null);
                    streamResult.RemoveAt(0);
                    streamResult.InsertRange(0, split);
                }

                return streamResult;
            }

            if (OperatorBreakPolicy.Instance.HasBinaryOperators(line, isCode, indentLen, out var binaryPositions))
            {
                var binaryResult = OperatorBreakPolicy.Instance.SplitAtBinaryOperators(line, binaryPositions, fixedContIndent, baseIndent);

                if (binaryResult.Count > 0 && binaryResult[0].Length > TextUtils.MaxLineLength)
                {
                    var split = SplitLongLine(binaryResult[0], null, null);
                    binaryResult.RemoveAt(0);
                    binaryResult.InsertRange(0, split);
                }

                return binaryResult;
            }

            int breakAt = OperatorBreakPolicy.Instance.FindSafeBreakPoint(line, isCode, indentLen);

            if (breakAt < 0 || breakAt >= line.Length)
            {
                return new List<string> { line };
            }

            if (fixedContIndent == null)
            {
                if (OperatorBreakPolicy.Instance.IsSemicolonBreak(line, isCode, breakAt))
                {
                    fixedContIndent = indent;
                }
                else
                {
                    fixedContIndent = indent + new string(' ', TextUtils.IndentSize);
                }
            }
            else if (OperatorBreakPolicy.Instance.IsSemicolonBreak(line, isCode, breakAt))
            {
                fixedContIndent = baseIndent;
            }

            string first = line.Substring(0, breakAt).TrimEnd();
            string rest = fixedContIndent + line.Substring(breakAt).TrimStart();

            if (first.Length == 0 || first.Length >= line.Length)
            {
                return new List<string> { line };
            }

            var result = new List<string> { first };
            result.AddRange(SplitLongLine(rest, fixedContIndent, baseIndent));
            return result;
        }

        /// <summary>
        /// Checks whether <paramref name="line"/> ends with a stream
        /// operator (<c>&lt;&lt;</c> or <c>&gt;&gt;</c>) in stream
        /// context, indicating it is the first line of a wrapped
        /// multi-line stream expression. If so, merges all
        /// continuation lines into a single unwrapped expression
        /// string returned via <paramref name="unwrapped"/>.
        /// </summary>
        private static bool TryUnwrapStreamChain(List<string> lines, string line, int startIndex, out string unwrapped)
        {
            string trimmed = line.TrimEnd();

            if (trimmed.Length < 2)
            {
                unwrapped = null;
                return false;
            }

            if (!(trimmed[trimmed.Length - 2] == '<' && trimmed[trimmed.Length - 1] == '<'))
            {
                unwrapped = null;
                return false;
            }

            int lastCodeIdx = trimmed.Length - 3;

            while (lastCodeIdx >= 0 && trimmed[lastCodeIdx] == ' ')
            {
                lastCodeIdx--;
            }

            if (lastCodeIdx < 0)
            {
                unwrapped = null;
                return false;
            }

            char pc = trimmed[lastCodeIdx];

            if (!(pc == ')' || pc == ']' || char.IsLetterOrDigit(pc) || pc == '_' || pc == '"' || pc == '\''))
            {
                unwrapped = null;
                return false;
            }

            int indentLen = CountLeadingSpaces(line);
            var parts = new List<string>();
            parts.Add(line.Substring(0, trimmed.Length - 2).TrimEnd());

            int j = startIndex + 1;

            while (j < lines.Count)
            {
                string next = lines[j];

                if (string.IsNullOrWhiteSpace(next))
                {
                    break;
                }

                int nextIndent = CountLeadingSpaces(next);

                if (nextIndent <= indentLen)
                {
                    break;
                }

                string nextTrimmed = next.TrimEnd();

                if (nextTrimmed.EndsWith("<<") && nextTrimmed.Length >= 2)
                {
                    nextTrimmed = nextTrimmed.Substring(0, nextTrimmed.Length - 2).TrimEnd();
                }

                parts.Add(nextTrimmed.TrimStart());
                j++;
            }

            if (parts.Count <= 1)
            {
                unwrapped = null;
                return false;
            }

            var sb = new StringBuilder(parts[0]);

            for (int k = 1; k < parts.Count; k++)
            {
                string part = parts[k];

                if (part.Length == 0)
                {
                    continue;
                }

                if (part.StartsWith("<<") || part.StartsWith(">>"))
                {
                    sb.Append(' ');
                    sb.Append(part);
                }
                else
                {
                    sb.Append(" << ");
                    sb.Append(part);
                }
            }

            unwrapped = sb.ToString();
            return true;
        }

        /// <summary>
        /// Advances <paramref name="i"/> past all continuation lines
        /// following the current line, so the outer loop skips them.
        /// Uses indent-based detection (continuation has greater indent
        /// than the first line of the chain).
        /// </summary>
        private static void SkipContinuationLines(List<string> lines, bool[] lineContinuesNext, ref int i)
        {
            int indentLen = CountLeadingSpaces(lines[i]);

            while (i + 1 < lines.Count)
            {
                string next = lines[i + 1];

                if (string.IsNullOrWhiteSpace(next))
                {
                    break;
                }

                int nextIndent = CountLeadingSpaces(next);

                if (nextIndent > indentLen)
                {
                    i++;
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Counts the number of leading space characters in a line.
        /// </summary>
        private static int CountLeadingSpaces(string line)
        {
            int count = 0;

            while (count < line.Length && line[count] == ' ')
            {
                count++;
            }

            return count;
        }
    }
}
