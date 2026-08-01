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
        public static readonly IndentationProcessor Instance =
            new IndentationProcessor();

        private IndentationProcessor()
        {
        }

        /// <summary>
        /// Recomputes leading whitespace for each line according to
        /// nesting depth. Lines that fall entirely inside a
        /// VerbatimString, MultiLineComment, InterpolatedString, or
        /// InterpolatedVerbatimString token retain their original
        /// leading whitespace.
        ///
        /// A closing brace (<c>}</c>) only lowers the recorded depth
        /// of the line it appears on when the line has no preceding
        /// code-region content (i.e., the line consists solely of
        /// the closing brace, optionally followed by whitespace or
        /// a comment). This prevents the re-indent pass from
        /// collapsing the continuation indent of a multi-line
        /// statement such as <c>if (cond) { foo(); }</c> on a
        /// second format pass.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full source text corresponding to <paramref name="lines"/>.</param>
        /// <param name="tokens">Pre-computed tokens of <paramref name="text"/> (avoid re-tokenization).</param>
        /// <param name="isCode">Pre-computed code mask of <paramref name="text"/>.</param>
        /// <param name="isCodeLine">Per-line flag indicating whether the line's first non-whitespace character is in a code region.</param>
        /// <returns>The re-indented line list.</returns>
        public List<string> Reindent(
            List<string> lines,
            string text,
            List<Token> tokens,
            bool[] isCode,
            bool[] isCodeLine
        )
        {
            int[] depths = new int[lines.Count];
            bool[] preserveIndent = ComputePreserveIndent(lines, tokens);
            bool[] inEnumBlock = ComputeInEnumBlock(lines, text, isCode);

            bool[] caseBody = ComputeCaseScope(lines, text, isCode,
                isCodeLine);

            int depth = 0;
            int lineIdx = 0;
            bool[] lineHasCodeContent = new bool[lines.Count];

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

                if (isCode[i] && c != '{' && c != '}' && c != ' ' &&
                    c != '\t' && c != '\r')
                {
                    if (lineIdx < lineHasCodeContent.Length)
                    {
                        lineHasCodeContent[lineIdx] = true;
                    }
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

                    if (lineIdx < depths.Length &&
                        !lineHasCodeContent[lineIdx])
                    {
                        depths[lineIdx] = depth;
                    }
                }
            }

            int[] lineStarts =
                CSharpTokenizer.Instance.ComputeLineStarts(lines);

            bool[] inInitializer = ComputeInitializerScope(lines, text,
                isCode, lineStarts);

            var result = new List<string>(lines.Count);

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

                // Lines starting with && or || are continuations of
                // the previous logical expression (placed there by
                // the line-length splitter). Use the guard logic to
                // check if the line already has the expected indent,
                // so that re-running the IndentationProcessor is
                // idempotent regardless of whether the previous line
                // is a continuation indicator. This check must come
                // before the regular continuation check so that ||/&&
                // lines are always handled by the guard logic.

                if (i > 0 && !inEnumBlock[i] && !inInitializer[i] &&
                    StartsWithLogicalOp(lines[i]))
                {
                    if (depths[i] <= depths[i - 1])
                    {
                        int currentIndent = 0;

                        while (currentIndent < lines[i].Length &&
                            lines[i][currentIndent] == ' ')
                        {
                            currentIndent++;
                        }

                        int expectedIndent =
                            (depths[i] + 1) * TextUtils.IndentSize;

                        if (currentIndent < expectedIndent)
                        {
                            baseDepth++;
                        }
                        else
                        {
                            // Line already has the expected (or more)
                            // continuation indent. Set baseDepth to
                            // depths[i] + 1 so the output is stable
                            // on subsequent passes, preventing
                            // oscillation between indent levels.
                            baseDepth = depths[i] + 1;
                        }
                    }
                }
                else if (i > 0 && !inEnumBlock[i] && !inInitializer[i] &&
                    IsContinuationIndicator(lines[i - 1],
                    lineStarts[i - 1], text, isCode))
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
        private static bool[] ComputePreserveIndent(
            List<string> lines,
            List<Token> tokens
        )
        {
            var preserveIndent = new bool[lines.Count];

            int[] lineStarts =
                CSharpTokenizer.Instance.ComputeLineStarts(lines);

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
        private static bool[] ComputeInEnumBlock(
            List<string> lines,
            string text,
            bool[] isCode
        )
        {
            var inEnumBlock = new bool[lines.Count];

            int[] lineStarts =
                CSharpTokenizer.Instance.ComputeLineStarts(lines);

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
        private static bool[] ComputeCaseScope(
            List<string> lines,
            string text,
            bool[] isCode,
            bool[] isCodeLine
        )
        {
            var caseBody = new bool[lines.Count];

            int[] lineStarts =
                CSharpTokenizer.Instance.ComputeLineStarts(lines);

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
        private static bool IsContinuationIndicator(
            string line,
            int lineStart,
            string text,
            bool[] isCode
        )
        {
            return LineClassifier.Instance.IsContinuationIndicator(line,
                lineStart, text, isCode);
        }

        /// <summary>
        /// Determines whether the trimmed line starts with a logical
        /// operator (<c>&&</c> or <c>||</c>). Such lines are
        /// continuations of a logical expression from the previous
        /// line, placed at the start of a continuation line by the
        /// line-length splitter.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <returns>True if the line starts with <c>&&</c> or <c>||</c>.</returns>
        private static bool StartsWithLogicalOp(string line)
        {
            string trimmed = line.TrimStart();
            return trimmed.StartsWith("&&") || trimmed.StartsWith("||");
        }

        /// <summary>
        /// Computes whether each line falls inside a collection or
        /// object initializer block. These are <c>{ ... }</c> blocks
        /// whose opening brace is on a line starting with <c>{</c>
        /// (first code character) and whose previous non-blank line
        /// is a continuation indicator. Inside such blocks, the
        /// <c>,</c> at the end of each element is a separator, not a
        /// line continuation, so the continuation-indent from one
        /// element to the next must be suppressed.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="lineStarts">The starting offsets of each line in <paramref name="text"/>.</param>
        /// <returns>A boolean array; true means the line is inside a collection/object initializer block.</returns>
        private static bool[] ComputeInitializerScope(
            List<string> lines,
            string text,
            bool[] isCode,
            int[] lineStarts
        )
        {
            var inInitializer = new bool[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                if (inInitializer[i])
                {
                    continue;
                }

                // Find the first non-whitespace character of the line
                int firstNonWs = 0;

                while (firstNonWs < lines[i].Length &&
                    (lines[i][firstNonWs] == ' ' ||
                    lines[i][firstNonWs] == '\t'))
                {
                    firstNonWs++;
                }

                if (firstNonWs >= lines[i].Length)
                {
                    continue;
                }

                int firstCodePos = lineStarts[i] + firstNonWs;
                // The first code character must be `{`

                if (firstCodePos >= isCode.Length ||
                    !isCode[firstCodePos] ||
                    text[firstCodePos] != '{')
                {
                    continue;
                }

                // The previous non-blank line must be a continuation
                int prev = i - 1;

                while (prev >= 0 && lines[prev].Trim().Length == 0)
                {
                    prev--;
                }

                if (prev < 0 ||
                    !IsContinuationIndicator(lines[prev],
                    lineStarts[prev], text, isCode))
                {
                    continue;
                }

                // Find the matching `}` for this `{`
                int depth = 1;
                int endPos = -1;

                for (int ti = firstCodePos + 1;
                    ti < text.Length && depth > 0; ti++)
                {
                    if (isCode[ti])
                    {
                        if (text[ti] == '{')
                        {
                            depth++;
                        }
                        else if (text[ti] == '}')
                        {
                            depth--;

                            if (depth == 0)
                            {
                                endPos = ti;
                            }
                        }
                    }
                }

                if (endPos < 0)
                {
                    continue;
                }

                // Mark all lines between the opening `{` and
                // matching `}` as inside an initializer block

                for (int j = 0; j < lines.Count; j++)
                {
                    if (lineStarts[j] > firstCodePos &&
                        lineStarts[j] < endPos)
                    {
                        inInitializer[j] = true;
                    }
                }
            }

            return inInitializer;
        }
    }
}
