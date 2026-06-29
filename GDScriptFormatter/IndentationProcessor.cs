using System.Collections.Generic;

namespace GDScriptFormatter
{
    /// <summary>
    /// Recomputes indentation for each line based on colon/brace block depth,
    /// bracket continuation, and triple-quoted string preservation.
    /// </summary>
    internal static class IndentationProcessor
    {
        /// <summary>
        /// Per-line analysis information: whether the line ends with colon/brace, whether it is a continuation, and its original indentation depth.
        /// </summary>
        internal struct LineAnalysis
        {
            /// <summary>Whether the line ends with a colon (not inside brackets).</summary>
            public bool ColonTerminated;

            /// <summary>Whether the line ends with { (not inside a string/comment) and the brace is not closed on the same line.</summary>
            public bool BraceTerminated;

            /// <summary>Whether the line starts with } (close-brace line).</summary>
            public bool IsCloseBrace;

            /// <summary>Whether the line is a continuation (bracket depth &gt; 0 or the previous line ended with \).</summary>
            public bool IsContinuation;

            /// <summary>The line's original indentation level (leading spaces / IndentSize).</summary>
            public int OriginalDepth;
        }

        /// <summary>
        /// Colon-based indentation recalculation: infers block depth stack from original indentation,
        /// colon-terminated code lines (not inside brackets) open a new block, bracket depth &gt; 0 or
        /// previous line ending with \ indicates a continuation line (indented one extra level). Lines inside
        /// triple-quoted strings preserve their original indentation. Reuses the caller-provided tokens
        /// and code mask instead of re-tokenizing.
        /// </summary>
        /// <param name="lines">The input lines.</param>
        /// <param name="text">The full text corresponding to the lines.</param>
        /// <param name="tokens">The tokenization of text (reused).</param>
        /// <param name="isCode">The code mask of text (reused).</param>
        /// <returns>The re-indented lines.</returns>
        internal static List<string> Reindent(List<string> lines, string text,
            List<Token> tokens, bool[] isCode)
        {
            bool[] preserveIndent = ComputePreserveIndent(lines, tokens);
            var lineStarts = ComputeLineStarts(lines);
            var lineInfo = ComputeLineInfo(lines, text, isCode, lineStarts);
            int[] depths = ComputeDepthsFromStack(lines, lineInfo);

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

                if (lineInfo[i].IsContinuation && baseDepth > 0)
                {
                    baseDepth++;
                }

                result.Add(new string(' ', baseDepth * TextUtils.IndentSize) +
                    content);
            }

            return result;
        }

        /// <summary>
        /// Computes the starting offset of each line.
        /// </summary>
        internal static int[] ComputeLineStarts(List<string> lines)
        {
            var lineStarts = new int[lines.Count];
            int pos = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                lineStarts[i] = pos;
                pos += lines[i].Length;

                if (i < lines.Count - 1)
                {
                    pos++;
                }
            }

            return lineStarts;
        }

        /// <summary>
        /// Analyzes per-line properties: colon/brace termination, continuation, original indentation depth.
        /// Continuation detection is based on parenthesis, square bracket, and brace depth. A line
        /// ending with a trailing { (BraceTerminated) does NOT increment the bracket depth — its
        /// body is indented via the stack — so that block-style dicts are not double-indented.
        /// Inline-open dicts/braces (e.g. "var m = {k: v,") DO increment the depth so that
        /// subsequent continuation lines are detected and preserved as continuations.
        /// </summary>
        internal static LineAnalysis[] ComputeLineInfo(List<string> lines,
            string text, bool[] isCode, int[] lineStarts)
        {
            var info = new LineAnalysis[lines.Count];
            int parenBracketDepth = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                int leadingSpaces = line.Length - trimmed.Length;
                int origDepth = leadingSpaces / TextUtils.IndentSize;

                info[i].OriginalDepth = origDepth;
                info[i].IsContinuation = parenBracketDepth > 0;

                if (i > 0 && EndsWithBackslash(text, isCode,
                    lineStarts[i - 1], lines[i - 1].Length))
                {
                    info[i].IsContinuation = true;
                }

                info[i].ColonTerminated = false;
                info[i].BraceTerminated = false;
                info[i].IsCloseBrace = false;

                int firstCodeIdx = -1;
                int lastCodeIdx = -1;

                if (trimmed.Length > 0)
                {
                    int lineEnd = lineStarts[i] + line.Length;

                    for (int ci = lineStarts[i]; ci < lineEnd &&
                        ci < isCode.Length; ci++)
                    {
                        if (isCode[ci])
                        {
                            if (firstCodeIdx < 0)
                            {
                                firstCodeIdx = ci;
                            }

                            lastCodeIdx = ci;
                        }
                    }

                    if (firstCodeIdx >= 0 && text[firstCodeIdx] == '}')
                    {
                        info[i].IsCloseBrace = true;
                    }

                    if (lastCodeIdx >= 0 && parenBracketDepth == 0)
                    {
                        if (text[lastCodeIdx] == ':')
                        {
                            info[i].ColonTerminated = true;
                        }
                    }

                    if (lastCodeIdx >= 0 && text[lastCodeIdx] == '{')
                    {
                        info[i].BraceTerminated = true;
                    }
                }

                for (int ci = lineStarts[i];
                ci < lineStarts[i] + line.Length && ci < isCode.Length;
                ci++)
                {
                    if (!isCode[ci])
                    {
                        continue;
                    }

                    char c = text[ci];
                    // Skip the trailing { on BraceTerminated lines — it is
                    // handled by the stack via BraceTerminated, so counting
                    // it here would double-indent the body of a block-style
                    // dict.

                    if (info[i].BraceTerminated && ci == lastCodeIdx)
                    {
                        continue;
                    }

                    if (c == '(' || c == '[' || c == '{')
                    {
                        parenBracketDepth++;
                    }

                    else if (c == ')' || c == ']' || c == '}')
                    {
                        if (parenBracketDepth > 0)
                        {
                            parenBracketDepth--;
                        }
                    }
                }
            }

            return info;
        }

        /// <summary>
        /// Stack-based indentation computation from original indentation depth: colon-terminated lines and brace-terminated lines
        /// open a new block, indenting subsequent lines by +1; close-brace lines and returning to shallower indentation pop blocks.
        /// </summary>
        private static int[] ComputeDepthsFromStack(List<string> lines,
            LineAnalysis[] lineInfo)
        {
            int[] depths = new int[lines.Count];
            var stack = new List<int>();

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();

                if (trimmed.Length == 0)
                {
                    depths[i] = stack.Count;
                    continue;
                }

                int origDepth = lineInfo[i].OriginalDepth;

                if (lineInfo[i].IsCloseBrace)
                {
                    if (stack.Count > 0)
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }

                    depths[i] = stack.Count;
                    continue;
                }

                while (stack.Count > 0 && origDepth < stack[stack.Count - 1])
                {
                    stack.RemoveAt(stack.Count - 1);
                }

                depths[i] = stack.Count;

                if (lineInfo[i].ColonTerminated ||
                    lineInfo[i].BraceTerminated)
                {
                    stack.Add(stack.Count + 1);
                }
            }

            return depths;
        }

        /// <summary>
        /// Determines whether each line is inside a triple-quoted string (non-first line), where original indentation must be preserved.
        /// </summary>
        private static bool[] ComputePreserveIndent(List<string> lines,
            List<Token> tokens)
        {
            var preserveIndent = new bool[lines.Count];
            var lineStarts = ComputeLineStarts(lines);
            int tokenPos = 0;

            foreach (var token in tokens)
            {
                int tokenStart = tokenPos;
                int tokenEnd = tokenPos + token.Text.Length;

                if (token.Kind == TokenKind.TripleString)
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
        /// Computes the indentation level of a line (leading spaces / IndentSize).
        /// </summary>
        internal static int LineIndentLevel(string line)
        {
            int spaces = 0;

            while (spaces < line.Length && line[spaces] == ' ')
            {
                spaces++;
            }

            return spaces / TextUtils.IndentSize;
        }

        /// <summary>
        /// Determines whether the line occupying [lineStart, lineStart+lineLength) in text ends with
        /// a continuation backslash that is located in a Code region. Backslashes inside comments or
        /// string literals do not trigger continuation. A doubled backslash (\\) in Code is treated
        /// as a non-continuation to preserve prior behavior.
        /// </summary>
        /// <param name="text">The full text.</param>
        /// <param name="isCode">The code mask of text.</param>
        /// <param name="lineStart">The starting offset of the line in text.</param>
        /// <param name="lineLength">The length of the line (excluding the line terminator).</param>
        /// <returns>True if the line ends with a Code-region continuation backslash.</returns>
        private static bool EndsWithBackslash(string text, bool[] isCode,
            int lineStart, int lineLength)
        {
            int lastIdx = -1;

            for (int i = lineStart + lineLength - 1; i >= lineStart; i--)
            {
                if (i >= text.Length)
                {
                    continue;
                }

                char c = text[i];

                if (c != ' ' && c != '\t')
                {
                    lastIdx = i;
                    break;
                }
            }

            if (lastIdx < 0)
            {
                return false;
            }

            if (lastIdx >= isCode.Length || !isCode[lastIdx])
            {
                return false;
            }

            if (text[lastIdx] != '\\')
            {
                return false;
            }

            if (lastIdx > lineStart && text[lastIdx - 1] == '\\' &&
                lastIdx - 1 < isCode.Length && isCode[lastIdx - 1])
            {
                return false;
            }

            return true;
        }
    }
}
