using System.Collections.Generic;
using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace CSharpFormatter
{
    /// <summary>
    /// Recomputes indentation for each line based on brace nesting
    /// depth, continuation indicators, enum-block membership, and
    /// switch case scope.
    /// </summary>
    internal sealed class IndentationProcessor
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly IndentationProcessor Instance = new IndentationProcessor();

        private IndentationProcessor()
        {
        }

        /// <summary>
        /// Recomputes leading whitespace for each line according to
        /// nesting depth. Lines that fall entirely inside a
        /// VerbatimString, MultiLineComment, InterpolatedString, or
        /// InterpolatedVerbatimString token retain their original
        /// leading whitespace.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full source text corresponding to <paramref name="lines"/>.</param>
        /// <param name="tokens">Pre-computed tokens of <paramref name="text"/> (avoid re-tokenization).</param>
        /// <param name="isCode">Pre-computed code mask of <paramref name="text"/>.</param>
        /// <param name="isCodeLine">Per-line flag indicating whether the line's first non-whitespace character is in a code region.</param>
        /// <returns>The re-indented line list.</returns>
        public List<string> Reindent(List<string> lines,
            string text, List<Token> tokens, bool[] isCode,
            bool[] isCodeLine)
        {
            int[] depths = new int[lines.Count];
            bool[] preserveIndent = ComputePreserveIndent(lines, tokens);
            bool[] inEnumBlock = ComputeInEnumBlock(lines, text, isCode);

            bool[] caseBody = ComputeCaseScope(lines, text, isCode,
                isCodeLine);

            int depth = 0;
            int lineIdx = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\n')
                {
                    lineIdx++;

                    if (lineIdx < depths.Length)
                    {
                        depths[lineIdx] = depth;
                    }

                    continue;
                }

                if (isCode[i] && c == '{')
                {
                    depth++;
                }
                else if (isCode[i] && c == '}')
                {
                    depth--;

                    if (depth < 0)
                    {
                        depth = 0;
                    }

                    if (lineIdx < depths.Length)
                    {
                        depths[lineIdx] = depth;
                    }
                }
            }

            var result = new List<string>(lines.Count);
            int[] lineStarts = CSharpTokenizer.Instance.ComputeLineStarts(lines);

            for (int i = 0; i < lines.Count; i++)
            {
                if (preserveIndent[i])
                {
                    result.Add(lines[i]);
                    continue;
                }

                string content = lines[i].TrimStart();

                if (content.Length == 0)
                {
                    result.Add(string.Empty);
                    continue;
                }

                int baseDepth = depths[i];

                if (i > 0 && !inEnumBlock[i] &&
                    IsContinuationIndicator(lines[i - 1], lineStarts[i - 1],
                    text, isCode))
                {
                    if (depths[i] <= depths[i - 1])
                    {
                        baseDepth++;
                    }
                }

                if (caseBody[i])
                {
                    baseDepth++;
                }

                result.Add(new string(' ', baseDepth * TextUtils.IndentSize) +
                    content);
            }

            return result;
        }

        /// <summary>
        /// Computes whether each line should preserve its original
        /// leading whitespace: returns true iff the line's starting
        /// position lies inside a VerbatimString, MultiLineComment,
        /// InterpolatedString, or InterpolatedVerbatimString token.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="tokens">The token list for the full text.</param>
        /// <returns>A boolean array; true means preserve the line's original indent.</returns>
        private static bool[] ComputePreserveIndent(List<string> lines,
            List<Token> tokens)
        {
            var preserveIndent = new bool[lines.Count];
            int[] lineStarts = CSharpTokenizer.Instance.ComputeLineStarts(lines);
            int tokenPos = 0;

            foreach (var token in tokens)
            {
                int tokenStart = tokenPos;
                int tokenEnd = tokenPos + token.Text.Length;

                if (token.Kind == TokenKind.VerbatimString ||
                    token.Kind == TokenKind.MultiLineComment ||
                    token.Kind == TokenKind.InterpolatedString ||
                    token.Kind == TokenKind.InterpolatedVerbatimString)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lineStarts[i] > tokenStart &&
                            lineStarts[i] < tokenEnd)
                        {
                            preserveIndent[i] = true;
                        }
                    }
                }

                tokenPos = tokenEnd;
            }

            return preserveIndent;
        }

        /// <summary>
        /// Computes whether each line is inside an enum block.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>A boolean array; true means the line is inside an enum block.</returns>
        private static bool[] ComputeInEnumBlock(List<string> lines,
            string text, bool[] isCode)
        {
            var inEnumBlock = new bool[lines.Count];
            int[] lineStarts = CSharpTokenizer.Instance.ComputeLineStarts(lines);
            var enumRanges = new List<KeyValuePair<int, int>>();
            int depth = 0;
            int enumDepth = -1;
            int enumStart = -1;
            bool pendingEnum = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = text[i];

                if (c == 'e' && (i == 0 ||
                    !TextUtils.IsWordChar(text[i - 1])) &&
                    TextUtils.MatchesWord(text, i, "enum"))
                {
                    pendingEnum = true;
                }

                if (c == '{')
                {
                    if (pendingEnum)
                    {
                        enumStart = i;
                        enumDepth = depth + 1;
                        pendingEnum = false;
                    }

                    depth++;
                }
                else if (c == '}')
                {
                    depth--;

                    if (depth < 0)
                    {
                        depth = 0;
                    }

                    if (enumDepth >= 0 && depth < enumDepth)
                    {
                        enumRanges.Add(new KeyValuePair<int, int>(
                            enumStart, i));

                        enumStart = -1;
                        enumDepth = -1;
                    }
                }
                else if (c == ';')
                {
                    pendingEnum = false;
                }
            }

            foreach (var range in enumRanges)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lineStarts[i] > range.Key &&
                        lineStarts[i] < range.Value)
                    {
                        inEnumBlock[i] = true;
                    }
                }
            }

            return inEnumBlock;
        }

        /// <summary>
        /// Computes which lines inside a switch block belong to a
        /// case body (i.e., need one extra indentation level). Uses
        /// <paramref name="isCodeLine"/> to ensure only code-region
        /// <c>case</c>/<c>default</c> labels are recognised.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="isCodeLine">Per-line code-region flag.</param>
        /// <returns>A boolean array; true means the line belongs to a case body.</returns>
        private static bool[] ComputeCaseScope(List<string> lines,
            string text, bool[] isCode, bool[] isCodeLine)
        {
            var caseBody = new bool[lines.Count];
            int[] lineStarts = CSharpTokenizer.Instance.ComputeLineStarts(lines);
            var switchRanges = new List<KeyValuePair<int, int>>();
            var braceStack = new Stack<KeyValuePair<bool, int>>();
            bool pendingSwitch = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = text[i];

                if (c == 's' && (i == 0 ||
                    !TextUtils.IsWordChar(text[i - 1])) &&
                    TextUtils.MatchesWord(text, i, "switch"))
                {
                    pendingSwitch = true;
                }

                if (c == '{')
                {
                    braceStack.Push(new KeyValuePair<bool, int>(
                        pendingSwitch, i));

                    pendingSwitch = false;
                }
                else if (c == '}')
                {
                    if (braceStack.Count > 0)
                    {
                        var top = braceStack.Pop();

                        if (top.Key)
                        {
                            switchRanges.Add(new KeyValuePair<int, int>(
                                top.Value, i));
                        }
                    }
                }
                else if (c == ';')
                {
                    pendingSwitch = false;
                }
            }

            switchRanges.Sort((a, b) => a.Key.CompareTo(b.Key));

            foreach (var range in switchRanges)
            {
                int braceStart = range.Key;
                int braceEnd = range.Value;
                var innerRanges = new List<KeyValuePair<int, int>>();

                foreach (var r in switchRanges)
                {
                    if (r.Key > braceStart && r.Value < braceEnd)
                    {
                        innerRanges.Add(r);
                    }
                }

                bool inCaseBody = false;

                for (int li = 0; li < lines.Count; li++)
                {
                    int ls = lineStarts[li];

                    if (ls <= braceStart || ls >= braceEnd)
                    {
                        continue;
                    }

                    int lineEndPos = ls + lines[li].Length;

                    if (braceEnd >= ls && braceEnd < lineEndPos)
                    {
                        inCaseBody = false;
                        continue;
                    }

                    bool inInner = false;

                    foreach (var ir in innerRanges)
                    {
                        if (ls > ir.Key && ls < ir.Value)
                        {
                            inInner = true;
                            break;
                        }
                    }

                    string trimmed = lines[li].Trim();

                    if (!inInner && isCodeLine[li] &&
                        LineClassifier.Instance.IsCaseLabelLine(trimmed))
                    {
                        inCaseBody = true;
                    }
                    else if (inCaseBody)
                    {
                        caseBody[li] = true;
                    }
                }
            }

            return caseBody;
        }

        /// <summary>
        /// Determines whether the specified line ends with a
        /// continuation indicator. Delegates to
        /// <see cref="LineClassifier.IsContinuationIndicator"/>.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The line's start offset in the full text.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>True if the line ends with a continuation indicator.</returns>
        private static bool IsContinuationIndicator(string line,
            int lineStart, string text, bool[] isCode)
        {
            return LineClassifier.Instance.IsContinuationIndicator(line,
                lineStart, text, isCode);
        }
    }
}
