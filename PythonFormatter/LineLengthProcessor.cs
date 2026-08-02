using System.Collections.Generic;
using System.Text;

using LafnyaToolkit.Core.Text;

namespace PythonFormatter
{
    /// <summary>
    /// Splits lines that exceed the configured maximum length at safe
    /// break points. Continuation lines are indented one level deeper
    /// than the statement base indent. Safe break points are
    /// operator-first: after <c>,</c>, <c>+</c>, <c>-</c>, <c>*</c>,
    /// <c>/</c>, <c>%</c>, <c>=</c>, <c>&lt;</c>, <c>&gt;</c>, and the
    /// two-character compound operators. Breaks never occur inside a
    /// string, multi-line string, or comment token. Stateless; the
    /// shared instance is exposed via <see cref="Instance"/>.
    /// </summary>
    internal sealed class LineLengthProcessor
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly LineLengthProcessor Instance =
            new LineLengthProcessor();

        private LineLengthProcessor()
        {
        }

        /// <summary>Maximum length of a single line.</summary>
        public const int MaxLineLength = 80;

        /// <summary>Number of spaces per indent level.</summary>
        public const int IndentSize = 4;

        /// <summary>
        /// Splits lines exceeding the maximum length at safe break
        /// points; continuation lines are indented one level deeper
        /// than the statement base indent.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <returns>The lines with long lines split.</returns>
        public List<string> ApplyLineLengthLimit(List<string> lines)
        {
            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                if (line.Length <= MaxLineLength)
                {
                    result.Add(line);
                    continue;
                }

                var split = SplitLongLine(line, null);
                result.AddRange(split);
            }

            return result;
        }

        /// <summary>
        /// Recursively splits a single line so each segment does not
        /// exceed the maximum length; only breaks at Code token
        /// boundaries. Never breaks inside String/VerbatimString/
        /// Comment tokens. If no safe break point is found, the
        /// original line is preserved.
        /// </summary>
        /// <param name="line">The line to split.</param>
        /// <param name="fixedContIndent">The fixed continuation indent
        /// reused across all continuation segments; pass null to
        /// compute from the original line's indent on the first split.</param>
        /// <returns>The list of split segments.</returns>
        private static List<string> SplitLongLine(string line,
            string fixedContIndent)
        {
            if (line.Length <= MaxLineLength)
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
                fixedContIndent = indent + new string(' ', IndentSize);
            }

            var tokens = PythonTokenizer.Instance.Tokenize(line);

            bool[] isCode = PythonTokenizer.Instance.BuildCodeMask(line,
                tokens);

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
        /// Finds a safe break point within Code tokens: prefers the
        /// latest break point that does not exceed the maximum length;
        /// if none, returns the first break point beyond the maximum
        /// length. Breaks after operators: <c>,</c>, <c>;</c>,
        /// <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>, <c>%</c>,
        /// <c>==</c>, <c>!=</c>, <c>&lt;</c>, <c>&gt;</c>,
        /// <c>&lt;=</c>, <c>&gt;=</c>, <c>=</c>, <c>+=</c>, <c>-=</c>,
        /// <c>and</c>, <c>or</c>, <c>not</c>, <c>is</c>, <c>in</c>,
        /// <c>not in</c>, <c>is not</c>. Does NOT break at
        /// <c>.</c> (member access) or <c>(</c> / <c>[</c> / <c>{</c>
        /// opening brackets (Python's natural continuation already
        /// provides the wrap point at the opening bracket).
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="startIdx">The scan start position.</param>
        /// <returns>The break point index, or -1 if none found.</returns>
        private static int FindSafeBreakPoint(
            string line,
            bool[] isCode,
            int startIdx
        )
        {
            int bestInRange = -1;
            int firstOutOfRange = -1;
            int i = startIdx;

            while (i < line.Length)
            {
                if (i >= isCode.Length || !isCode[i])
                {
                    i++;
                    continue;
                }

                char c = line[i];
                int bp = -1;

                if (c == ',')
                {
                    bp = i + 1;
                }
                else if (c == ':' && i + 1 < line.Length && line[i + 1] != '=')
                {
                    bp = i + 1;
                }
                else if (c == '+' || c == '-' || c == '*' || c == '/' ||
                    c == '%')
                {
                    if (i > startIdx && IsBinaryOpContext(line, i, startIdx))
                    {
                        bp = i + 1;
                    }
                }
                else if (c == '<')
                {
                    if (i + 1 < line.Length && line[i + 1] == '=')
                    {
                        bp = i + 2;
                    }
                    else if (i > startIdx &&
                        IsBinaryOpContext(
                            line,
                            i,
                            startIdx
                        ))
                    {
                        bp = i + 1;
                    }
                }
                else if (c == '>')
                {
                    if (i + 1 < line.Length && line[i + 1] == '=')
                    {
                        bp = i + 2;
                    }
                    else if (i > startIdx &&
                        IsBinaryOpContext(
                            line,
                            i,
                            startIdx
                        ))
                    {
                        bp = i + 1;
                    }
                }
                else if (c == '=')
                {
                    if (i + 1 < line.Length && line[i + 1] == '=')
                    {
                        bp = i + 2;
                    }
                    else if (i + 1 < line.Length && line[i + 1] == '!')
                    {
                    }
                    else if (i > startIdx &&
                        IsBinaryOpContext(
                            line,
                            i,
                            startIdx
                        ))
                    {
                        bp = i + 1;
                    }
                }
                else if (c == '!')
                {
                    if (i + 1 < line.Length && line[i + 1] == '=')
                    {
                        bp = i + 2;
                    }
                }

                if (bp > 0)
                {
                    if (bp <= MaxLineLength)
                    {
                        bestInRange = bp;
                    }
                    else if (firstOutOfRange < 0)
                    {
                        firstOutOfRange = bp;
                    }
                }

                if (i + 1 < line.Length &&
                    (line[i + 1] == '=' || line[i + 1] == '<' ||
                        line[i + 1] == '>'))
                {
                    i += 2;
                }
                else
                {
                    i++;
                }
            }

            if (bestInRange > 0)
            {
                return bestInRange;
            }

            return firstOutOfRange;
        }

        /// <summary>
        /// Determines whether the character at position
        /// <paramref name="i"/> of <paramref name="line"/> is in a
        /// binary operator context: the previous non-whitespace
        /// character is <c>)</c>, <c>]</c>, an identifier character,
        /// <c>_</c>, or a string-close character. Used to exclude
        /// unary operators and the unary <c>-</c> in literals.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="i">The current operator position.</param>
        /// <param name="startIdx">The scan start position.</param>
        /// <returns>True if in a binary operator context; otherwise
        /// false.</returns>
        private static bool IsBinaryOpContext(
            string line,
            int i,
            int startIdx
        )
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

            return pc == ')' || pc == ']' || TextUtils.IsWordChar(pc) ||
                pc == '"' || pc == '\'';
        }
    }
}
