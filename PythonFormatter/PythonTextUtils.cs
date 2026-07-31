using System.Collections.Generic;
using System.Text;

using LafnyaToolkit.Core.Tokenization;

namespace PythonFormatter
{
    /// <summary>
    /// Python-specific text helpers that complement
    /// <see cref="LafnyaToolkit.Core.Text.TextUtils"/>. Provides
    /// token-aware tab normalization, line splitting, line-start index
    /// computation, trailing-whitespace trimming, and bracket-balance
    /// helpers used by the indentation and line-length processors.
    /// Stateless; the shared instance is exposed via <see cref="Instance"/>.
    /// </summary>
    internal sealed class PythonTextUtils
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly PythonTextUtils Instance = new PythonTextUtils();

        private PythonTextUtils()
        {
        }

        /// <summary>
        /// Replaces tabs with four spaces only inside Code tokens,
        /// preserving tabs inside string literals, multi-line strings,
        /// and comments.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with code-region tabs expanded to four spaces.</returns>
        public string NormalizeTabs(string text)
        {
            var tokens = PythonTokenizer.Instance.Tokenize(text);
            var sb = new StringBuilder(text.Length);

            foreach (var token in tokens)
            {
                if (token.Kind == TokenKind.Code)
                {
                    sb.Append(token.Text.Replace("\t", "    "));
                }
                else
                {
                    sb.Append(token.Text);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Splits text into a list of lines on '\n'. The returned list
        /// never contains the newline character.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The list of lines.</returns>
        public List<string> SplitLines(string text)
        {
            return new List<string>(text.Split('\n'));
        }

        /// <summary>
        /// Computes the starting text position of each line in the
        /// concatenated full text. Lines are separated by a single '\n'.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <returns>An array where element i is the starting position of
        /// line i in the reconstructed text.</returns>
        public int[] ComputeLineStarts(List<string> lines)
        {
            var starts = new int[lines.Count];
            int pos = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                starts[i] = pos;
                pos += lines[i].Length;

                if (i < lines.Count - 1)
                {
                    pos++;
                }
            }

            return starts;
        }

        /// <summary>
        /// Computes a per-line flag indicating whether the line's last
        /// non-whitespace character lies inside a string, multi-line
        /// string, or comment token. Such lines are left untouched by
        /// <see cref="TrimTrailingWhitespace"/> to avoid corrupting raw
        /// content.
        /// </summary>
        /// <param name="text">The full source text.</param>
        /// <returns>A boolean array of per-line "ends inside token" flags.</returns>
        public bool[] BuildLineEndsInsideToken(string text)
        {
            var tokens = PythonTokenizer.Instance.Tokenize(text);
            var starts = new List<int>();
            int pos = 0;

            foreach (var token in tokens)
            {
                int len = token.Text.Length;

                for (int i = 0; i < len; i++)
                {
                    if (token.Text[i] == '\n')
                    {
                        starts.Add(pos + i);
                    }
                }

                pos += len;
            }

            var lines = SplitLines(text);
            var result = new bool[lines.Count];

            if (starts.Count == 0)
            {
                return result;
            }

            int lineIdx = 0;

            foreach (var newlinePos in starts)
            {
                if (lineIdx >= result.Length)
                {
                    break;
                }

                int lastCode = LastNonWhitespaceCodeIndex(text, newlinePos);

                if (lastCode < 0)
                {
                    lineIdx++;
                    continue;
                }

                int containingTokenEnd = -1;

                foreach (var token in tokens)
                {
                    int tokenStart = token.Start;

                    if (token.Kind == TokenKind.Code)
                    {
                        continue;
                    }

                    int tokenEnd = tokenStart + token.Text.Length;

                    if (lastCode >= tokenStart && lastCode < tokenEnd)
                    {
                        containingTokenEnd = tokenEnd;
                        break;
                    }
                }

                if (containingTokenEnd > newlinePos)
                {
                    result[lineIdx] = true;
                }

                lineIdx++;
            }

            return result;
        }

        /// <summary>
        /// Finds the index of the last non-whitespace character in
        /// <paramref name="text"/> that is at or before
        /// <paramref name="endPos"/>. If no such character exists,
        /// returns -1.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="endPos">The exclusive upper bound for the scan
        /// (typically the position of a '\n' character).</param>
        /// <returns>The index of the last non-whitespace character, or
        /// -1 if none exists.</returns>
        private static int LastNonWhitespaceCodeIndex(string text, int endPos)
        {
            int i = endPos - 1;

            while (i >= 0 && (text[i] == ' ' || text[i] == '\t' ||
                text[i] == '\r'))
            {
                i--;
            }

            return i;
        }

        /// <summary>
        /// Trims trailing whitespace from every line in the text. Lines
        /// whose last non-whitespace character lies inside a multi-line
        /// string or comment token (as determined by
        /// <paramref name="lineEndsInsideToken"/>) are left untouched to
        /// preserve raw content.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="lineEndsInsideToken">Per-line flag from
        /// <see cref="BuildLineEndsInsideToken"/>; true means skip that
        /// line.</param>
        /// <returns>The text with trailing whitespace removed from each
        /// line (except those flagged by the mask).</returns>
        public string TrimTrailingWhitespace(string text,
            bool[] lineEndsInsideToken)
        {
            var lines = SplitLines(text);
            var sb = new StringBuilder(text.Length);

            for (int i = 0; i < lines.Count; i++)
            {
                string line;

                if (lineEndsInsideToken != null && i < lineEndsInsideToken.Length
                    && lineEndsInsideToken[i])
                {
                    line = lines[i];
                }
                else
                {
                    line = lines[i].TrimEnd();
                }

                sb.Append(line);

                if (i < lines.Count - 1)
                {
                    sb.Append('\n');
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Computes the net open-bracket count for the code region of a
        /// line. Returns the maximum value of (opens - closes) over the
        /// line; positive values indicate the line ends with an
        /// unclosed bracket that expects a continuation line. A return
        /// value of 0 means the brackets are balanced.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The line's start position in the
        /// full text.</param>
        /// <param name="isCode">The code mask of the full text.</param>
        /// <returns>The net open-bracket count for the line.</returns>
        public int NetOpenBrackets(string line, int lineStart, bool[] isCode)
        {
            int depth = 0;

            for (int i = 0; i < line.Length; i++)
            {
                int p = lineStart + i;

                if (p < 0 || p >= isCode.Length || !isCode[p])
                {
                    continue;
                }

                char c = line[i];

                if (c == '(' || c == '[' || c == '{')
                {
                    depth++;
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    depth--;
                }
            }

            return depth;
        }

        /// <summary>
        /// Computes the running bracket depth at the end of each line
        /// in the input. The depth at the end of line <c>i</c> is the
        /// sum of <see cref="NetOpenBrackets"/> for lines <c>0..i</c>.
        /// A positive depth means the line ends with one or more
        /// unclosed opening brackets (an implicit continuation); a
        /// depth of <c>0</c> means brackets are balanced at the end of
        /// the line. The returned array is indexed by line position in
        /// the precomputed <paramref name="lineStarts"/> order, which
        /// matches <see cref="ComputeLineStarts"/>.
        /// </summary>
        /// <param name="lines">The list of input lines (without
        /// trailing newlines).</param>
        /// <param name="isCode">The code mask of the full text.</param>
        /// <param name="lineStarts">The start position of each line in
        /// the full text, as returned by <see cref="ComputeLineStarts"/>.</param>
        /// <returns>An array of per-line end-of-line running bracket
        /// depths.</returns>
        public int[] ComputeLineEndDepths(List<string> lines, bool[] isCode,
            int[] lineStarts)
        {
            int[] depths = new int[lines.Count];
            int running = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                running += NetOpenBrackets(lines[i], lineStarts[i], isCode);
                depths[i] = running;
            }

            return depths;
        }

        /// <summary>
        /// Joins a list of lines with '\n' and returns the result.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <returns>The joined text.</returns>
        public string JoinLines(List<string> lines)
        {
            return string.Join("\n", lines);
        }
    }
}
