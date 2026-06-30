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

            /// <summary>The bracket depth at the start of this line, before processing any brackets on this line.
            /// Used to distinguish outermost continuation closing brackets (depth 1 → drop to parent indent)
            /// from nested continuation closing brackets (depth &gt; 1 → keep continuation indent).</summary>
            public int StartBracketDepth;

            /// <summary>The bracket depth at the end of this line, after processing all brackets on this line.
            /// Used to determine whether a continuation line starting with a closing bracket should keep
            /// continuation indent (EndBracketDepth > 0 means still inside nested brackets). This is
            /// stable across formatting passes even when synthetic parentheses are introduced by line
            /// splitting, unlike StartBracketDepth which shifts when wrapping parentheses are added.</summary>
            public int EndBracketDepth;
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

            int[] depths = ComputeDepthsFromStack(lines, lineInfo,
                preserveIndent);

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

                if (lineInfo[i].IsContinuation)
                {
                    // A line that starts with a closing bracket returns
                    // to the parent indent level rather than continuing
                    // at the continuation indent — but only when closing
                    // the outermost bracket (EndBracketDepth == 0).
                    // When inside nested brackets (EndBracketDepth > 0),
                    // keep the continuation indent so that synthetically
                    // introduced wrapping brackets (e.g. from = (...) line
                    // splitting) do not lose their indentation on a second
                    // formatting pass. Using EndBracketDepth instead of
                    // StartBracketDepth ensures stability across formatting
                    // passes because EndBracketDepth reflects the bracket
                    // depth after the line is fully processed, which is
                    // independent of synthetic parentheses added on a prior
                    // formatting pass.

                    if (content.Length == 0 ||
                        (content[0] != ')' && content[0] != ']' &&
                        content[0] != '}') ||
                        lineInfo[i].EndBracketDepth > 0)
                    {
                        baseDepth++;
                    }

                    // Additional fix for continuation lines that close the
                    // outermost bracket: block-stack entries pushed by
                    // colon-terminated lines inside the continuation context
                    // (e.g., func(): inside callback parens) inflate
                    // depths[i]. The closing bracket should return to the
                    // indentation level of the line that opened the bracket,
                    // which is the last non-continuation line's depth.

                    if (content.Length > 0 &&
                        (content[0] == ')' || content[0] == ']') &&
                        lineInfo[i].EndBracketDepth == 0 &&
                        depths[i] > 0)
                    {
                        for (int j = i - 1; j >= 0; j--)
                        {
                            if (!lineInfo[j].IsContinuation)
                            {
                                baseDepth = depths[j];
                                break;
                            }
                        }
                    }
                }

                result.Add(new string(' ',
                    baseDepth * TextUtils.IndentSize) + content);
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
                info[i].StartBracketDepth = parenBracketDepth;
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

                    // Fallback: if the code mask didn't identify the closing brace
                    // (e.g. because garbled text in a string literal confused the
                    // tokenizer), detect it from the trimmed line content instead.
                    // A line whose first non-whitespace character is '}' and which
                    // contains no other code tokens before it is a close-brace line.

                    if (!info[i].IsCloseBrace && trimmed.Length > 0 &&
                        trimmed[0] == '}')
                    {
                        info[i].IsCloseBrace = true;
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

                info[i].EndBracketDepth = parenBracketDepth;
                // Colon check must happen AFTER processing this line's
                // brackets so that a closing )/]/} before the colon is
                // properly accounted for.

                if (lastCodeIdx >= 0 && parenBracketDepth == 0)
                {
                    if (text[lastCodeIdx] == ':')
                    {
                        info[i].ColonTerminated = true;
                    }
                }

                // Special case: block-starting keywords inside continuation
                // contexts (e.g., inside brackets). Normal colon detection
                // skips colons inside brackets to avoid treating dictionary
                // keys as block starters. But keywords like func, if, for,
                // while, match, elif, else always open a block regardless
                // of bracket context — especially inside anonymous function
                // bodies.

                if (!info[i].ColonTerminated && lastCodeIdx >= 0 &&
                    text[lastCodeIdx] == ':' && firstCodeIdx >= 0)
                {
                    // Skip leading whitespace to reach the first word.
                    // The tokenizer treats whitespace as Code, so
                    // firstCodeIdx may point to a space rather than a
                    // keyword.
                    int wordStart = firstCodeIdx;

                    while (wordStart < text.Length &&
                        wordStart < isCode.Length &&
                        isCode[wordStart] &&
                        char.IsWhiteSpace(text[wordStart]))
                    {
                        wordStart++;
                    }

                    if (wordStart < text.Length &&
                        wordStart < isCode.Length &&
                        isCode[wordStart])
                    {
                        int wordEnd = wordStart;

                        while (wordEnd < text.Length &&
                            wordEnd < isCode.Length &&
                            isCode[wordEnd] &&
                            !char.IsWhiteSpace(text[wordEnd]) &&
                            text[wordEnd] != '(')
                        {
                            wordEnd++;
                        }

                        string firstWord = text.Substring(wordStart,
                            wordEnd - wordStart);

                        if (firstWord == "func" || firstWord == "if" ||
                            firstWord == "for" || firstWord == "while" ||
                            firstWord == "match" || firstWord == "elif" ||
                            firstWord == "else")
                        {
                            info[i].ColonTerminated = true;
                        }
                    }
                }
            }

            return info;
        }

        /// <summary>
        /// Stack-based indentation computation from original indentation depth: colon-terminated lines and brace-terminated lines
        /// open a new block, indenting subsequent lines by +1; close-brace lines and returning to shallower indentation pop blocks.
        /// Lines inside triple-quoted strings (preserveIndent[i] == true) are skipped for stack manipulation so that their
        /// content-leading does not incorrectly pop the block stack.
        /// </summary>
        private static int[] ComputeDepthsFromStack(List<string> lines,
            LineAnalysis[] lineInfo, bool[] preserveIndent)
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

                // Lines inside triple-quoted strings must not affect the
                // block stack — their content-leading may be shallower
                // than the enclosing block's indent (e.g. raw """ string
                // content starting at column 0 inside a function body).

                if (preserveIndent[i])
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

                // Continuation lines (inside brackets) have an unreliable
                // origDepth because their indentation depends on the line
                // that opened the bracket, not on their own block nesting.
                // Using origDepth to pop the stack would propagate incorrect
                // indentation from the original source and cause non-idempotent
                // formatting. Only non-continuation lines are allowed to pop
                // the stack based on origDepth, since their indentation
                // reflects the actual block structure. Close-brace lines ({)
                // are handled by IsCloseBrace above, not by this pop logic.

                if (!lineInfo[i].IsContinuation)
                {
                    while (stack.Count > 0 &&
                        origDepth < stack[stack.Count - 1])
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }
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
