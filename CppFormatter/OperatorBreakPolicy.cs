using System.Collections.Generic;
using LafnyaToolkit.Core.Text;

namespace CppFormatter
{
    /// <summary>
    /// Encapsulates the operator-aware break-point detection logic
    /// used by <see cref="LineLengthProcessor"/>. Provides methods
    /// to scan a line for stream-operator (<c>&lt;&lt;</c>/
    /// <c>&gt;&gt;</c>) and binary-operator (+, -, *, /, %) positions,
    /// to determine whether a given character position is a safe
    /// break point, and to perform one-pass splits at all detected
    /// operator positions. Stateless; the shared instance is
    /// exposed via <see cref="Instance"/>.
    /// </summary>
    internal sealed class OperatorBreakPolicy
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly OperatorBreakPolicy Instance = new OperatorBreakPolicy();

        /// <summary>
        /// Two-character operators whose break point sits right after
        /// the operator. Excludes <c>&lt;&lt;</c>/<c>&gt;&gt;</c>
        /// (stream ops use <see cref="IsStreamOpContext"/>) and
        /// single-character operators.
        /// </summary>
        private static readonly string[] TwoCharBreakOps =
        {
            "==", "!=", "<=", ">=", "=>", "+=", "-=", "&&", "||"
        };

        private OperatorBreakPolicy()
        {
        }

        /// <summary>
        /// Finds a safe break point within Code tokens, returning a
        /// position in <c>[startIdx, MaxLineLength]</c> when possible
        /// (or the first over-length break point otherwise). In
        /// addition to two-character and single-character operators,
        /// supports stream <c>&lt;&lt;</c>/<c>&gt;&gt;</c> in stream
        /// context and binary <c>+</c>/<c>-</c>/<c>*</c>/<c>/</c>/
        /// <c>%</c>/<c>&lt;</c>/<c>&gt;</c>/<c>=</c> in binary context.
        /// </summary>
        public int FindSafeBreakPoint(string line, bool[] isCode, int startIdx)
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

                if (bp < 0 && c == '<' && i + 1 < line.Length && line[i + 1] == '<' && IsStreamOpContext(line, i, startIdx))
                {
                    bp = i;
                    i++;
                }
                else if (bp < 0 && c == '>' && i + 1 < line.Length && line[i + 1] == '>' && IsStreamOpContext(line, i, startIdx))
                {
                    bp = i;
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
                else if (bp < 0 && i > startIdx && IsBinaryOpContext(line, i, startIdx) && (c == '+' || c == '-' || c == '*' || c == '/' || c == '%' || c == '<' || c == '>'))
                {
                    if (c == '-' && i + 1 < line.Length && line[i + 1] == '>')
                    {
                        i++;
                        continue;
                    }

                    bp = i + 1;
                }
                else if (bp < 0 && c == '=' && i > startIdx && IsBinaryOpContext(line, i, startIdx) && (i + 1 >= line.Length || (line[i + 1] != '=' && line[i + 1] != '>')))
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
        /// Determines whether the break point is immediately after a
        /// semicolon in code context.
        /// </summary>
        public bool IsSemicolonBreak(string line, bool[] isCode, int breakAt)
        {
            if (breakAt <= 0 || breakAt > line.Length)
            {
                return false;
            }

            int semiPos = breakAt - 1;
            return semiPos < isCode.Length && isCode[semiPos] && line[semiPos] == ';';
        }

        /// <summary>
        /// Determines whether a position with <c>&lt;&lt;</c> or
        /// <c>&gt;&gt;</c> is in a stream operator context: the
        /// preceding non-whitespace character is a value token
        /// (<c>)</c>, <c>]</c>, identifier character, <c>_</c>, <c>"</c>,
        /// or <c>'</c>). This avoids breaking inside template
        /// parameter lists (e.g., <c>vector&lt;vector&lt;int&gt;&gt;</c>).
        /// </summary>
        public bool IsStreamOpContext(string line, int i, int startIdx)
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
            return pc == ')' || pc == ']' || char.IsLetterOrDigit(pc) || pc == '_' || pc == '"' || pc == '\'';
        }

        /// <summary>
        /// Scans a line for stream operator (<c>&lt;&lt;</c>)
        /// positions in stream context.
        /// </summary>
        public bool HasStreamOperators(string line, int startIdx, out List<int> positions)
        {
            positions = new List<int>();

            for (int i = startIdx; i < line.Length - 1; i++)
            {
                if (line[i] == '<' && line[i + 1] == '<' && IsStreamOpContext(line, i, startIdx))
                {
                    positions.Add(i);
                    i++;
                }
            }

            return positions.Count > 0;
        }

        /// <summary>
        /// Performs a one-pass split of a line at all stream operator
        /// positions, placing each <c>&lt;&lt;</c> at the start of its
        /// own continuation line.
        /// </summary>
        public List<string> SplitAtStreamOperators(string line, List<int> positions, string fixedContIndent, string baseIndent)
        {
            int indentLen = 0;

            while (indentLen < line.Length && line[indentLen] == ' ')
            {
                indentLen++;
            }

            string indent = line.Substring(0, indentLen);

            string contIndent = fixedContIndent != null ? fixedContIndent : (indent + new string(' ', TextUtils.IndentSize));

            var result = new List<string>();
            result.Add(line.Substring(0, positions[0]).TrimEnd());

            for (int j = 0; j < positions.Count; j++)
            {
                int end = (j + 1 < positions.Count) ? positions[j + 1] : line.Length;

                string segment = contIndent + line.Substring(positions[j], end - positions[j]).TrimStart();
                result.Add(segment.TrimEnd());
            }

            return result;
        }

        /// <summary>
        /// Scans a line for binary operator (+, -, *, /, %) positions
        /// in binary context (preceded by an expression term). Excludes
        /// pointer member access (-&gt;). Uses the code mask to skip
        /// non-code regions (comments, string literals).
        /// </summary>
        public bool HasBinaryOperators(string line, bool[] isCode, int startIdx, out List<int> positions)
        {
            positions = new List<int>();

            for (int i = startIdx; i < line.Length; i++)
            {
                if (i >= isCode.Length || !isCode[i])
                {
                    continue;
                }

                char c = line[i];

                if ((c == '+' || c == '-' || c == '*' || c == '/' || c == '%') && IsBinaryOpContext(line, i, startIdx))
                {
                    if (c == '-' && i + 1 < line.Length && line[i + 1] == '>')
                    {
                        i++;
                        continue;
                    }

                    positions.Add(i);
                }
            }

            return positions.Count > 0;
        }

        /// <summary>
        /// Performs a one-pass split of a line at all binary operator
        /// positions, placing each operator at the start of its own
        /// continuation line.
        /// </summary>
        public List<string> SplitAtBinaryOperators(string line, List<int> positions, string fixedContIndent, string baseIndent)
        {
            int indentLen = 0;

            while (indentLen < line.Length && line[indentLen] == ' ')
            {
                indentLen++;
            }

            string indent = line.Substring(0, indentLen);

            string contIndent = fixedContIndent != null ? fixedContIndent : (indent + new string(' ', TextUtils.IndentSize));

            var result = new List<string>();
            result.Add(line.Substring(0, positions[0]).TrimEnd());

            for (int j = 0; j < positions.Count; j++)
            {
                int end = (j + 1 < positions.Count) ? positions[j + 1] : line.Length;

                string segment = contIndent + line.Substring(positions[j], end - positions[j]).TrimStart();
                result.Add(segment.TrimEnd());
            }

            return result;
        }

        /// <summary>
        /// Determines whether the position at <paramref name="line"/>[<paramref name="i"/>]
        /// is in a binary operator context: the preceding
        /// non-whitespace character is a value token (<c>)</c>,
        /// <c>]</c>, identifier character, <c>_</c>, or <c>"</c>).
        /// </summary>
        public bool IsBinaryOpContext(string line, int i, int startIdx)
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
            return pc == ')' || pc == ']' || char.IsLetterOrDigit(pc) || pc == '_' || pc == '"';
        }
    }
}
