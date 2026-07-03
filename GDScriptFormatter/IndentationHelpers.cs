using System.Collections.Generic;

namespace GDScriptFormatter
{
    /// <summary>
    /// Recomputes indentation for each line based on colon/brace block depth,
    /// bracket continuation, and triple-quoted string preservation.
    /// </summary>
    internal static partial class IndentationProcessor
    {
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
            int suppressedBraceDepth = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                int leadingSpaces = line.Length - trimmed.Length;
                int origDepth = leadingSpaces / TextUtils.IndentSize;

                info[i].OriginalDepth = origDepth;
                info[i].StartBracketDepth = parenBracketDepth;
                info[i].IsContinuation = parenBracketDepth > 0;

                if (i > 0 && LineContinuationAnalyzer.EndsWithBackslash(text,
                    isCode,
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
                        suppressedBraceDepth++;
                        continue;
                    }

                    if (c == '(' || c == '[' || c == '{')
                    {
                        parenBracketDepth++;
                    }
                    else if (c == ')' || c == ']')
                    {
                        if (parenBracketDepth > 0)
                        {
                            parenBracketDepth--;
                        }
                    }
                    else if (c == '}')
                    {
                        if (suppressedBraceDepth > 0)
                        {
                            suppressedBraceDepth--;
                        }
                        else if (parenBracketDepth > 0)
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
                    // Scan backwards from lastCodeIdx past any trailing
                    // whitespace in the code region. The code mask marks
                    // whitespace between code tokens and comments as Code,
                    // so lastCodeIdx may point to a space rather than the
                    // actual last meaningful character (e.g. a colon before
                    // a `#` inline comment).
                    int actualLast = lastCodeIdx;

                    while (actualLast >= 0 && actualLast < isCode.Length &&
                        isCode[actualLast] &&
                        char.IsWhiteSpace(text[actualLast]))
                    {
                        actualLast--;
                    }

                    if (actualLast >= 0 && text[actualLast] == ':')
                    {
                        info[i].ColonTerminated = true;
                    }
                }

                if (!info[i].ColonTerminated)
                {
                    CheckColonUnderBrackets(ref info[i], text, isCode,
                        firstCodeIdx, lastCodeIdx);
                }
            }

            return info;
        }

        /// <summary>
        /// Checks whether a colon-terminated line inside brackets should still be
        /// treated as block-starting (colon-terminated) based on the leading keyword.
        /// Block-starting keywords (func, if, for, while, match, elif, else) always
        /// open a block regardless of bracket context — especially inside anonymous
        /// function bodies.
        /// </summary>
        private static void CheckColonUnderBrackets(
            ref LineAnalysis info,
            string text,
            bool[] isCode,
            int firstCodeIdx,
            int lastCodeIdx)
        {
            if (lastCodeIdx < 0 || text[lastCodeIdx] != ':' || firstCodeIdx < 0)
            {
                return;
            }

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

            // If wordStart points to a non-Code character after skipping
            // whitespace, the first meaningful content on this line is
            // inside a string/comment literal (e.g., a match case pattern
            // like "__SPEECH_CONFLICT__":). In this case, all characters
            // before ':' are non-Code, so there are no Code brackets and
            // bracketDepthBeforeColon would be 0. Treat it as a block
            // starter — correct for match case patterns inside lambda bodies.

            if (wordStart >= text.Length ||
                wordStart >= isCode.Length ||
                !isCode[wordStart])
            {
                info.ColonTerminated = true;
                return;
            }

            int wordEnd = wordStart;

            while (wordEnd < text.Length &&
                wordEnd < isCode.Length &&
                isCode[wordEnd] &&
                !char.IsWhiteSpace(text[wordEnd]) &&
                text[wordEnd] != '(' &&
                text[wordEnd] != ':')
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
                info.ColonTerminated = true;
                return;
            }

            // Check if this is a match case pattern inside brackets.
            // A case pattern line has no unclosed brackets before its
            // trailing colon (the colon operates at the line's bracket
            // level, not inside any call arguments). This distinguishes
            // case patterns like "pattern": from lines like
            // some_func(func(...): where the colon is inside unclosed
            // call brackets.
            int bracketDepthBeforeColon = 0;

            for (int ci = wordStart;
            ci < lastCodeIdx && ci < isCode.Length; ci++)
            {
                if (!isCode[ci])
                {
                    continue;
                }

                char c = text[ci];

                if (c == '(' || c == '[' || c == '{')
                {
                    bracketDepthBeforeColon++;
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (bracketDepthBeforeColon > 0)
                    {
                        bracketDepthBeforeColon--;
                    }
                }
            }

            if (bracketDepthBeforeColon == 0)
            {
                info.ColonTerminated = true;
            }
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
            // Tracks stack pushes made by colon-terminated continuation lines
            // (e.g. func/if/for/while/match/elif/else inside brackets).
            // Each entry stores (stackHeightAfterPush, origDepthOfLine).
            var continuationColonPushes = new List<(int height, int origDepth)>
                ();

            bool previousWasColonOrBrace = false;

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

                if (lineInfo[i].IsCloseBrace && !lineInfo[i].IsContinuation)
                {
                    HandleCloseBrace(i, stack, depths);
                    previousWasColonOrBrace = false;
                    continue;
                }

                // Comment lines must not affect the block stack — they
                // are invisible to GDScript's block structure and should
                // not trigger HandleNonContinuationPop or any other stack
                // manipulation.

                if (trimmed.StartsWith("#"))
                {
                    // Comment lines must not pop the block stack (they are
                    // invisible to GDScript's block structure). Use the
                    // minimum of origDepth and stack.Count so that:
                    //   - Comments inside blocks use the block's depth
                    //     (stack.Count caps origDepth to prevent runaway
                    //     deep indents from wrong original indentation)
                    //   - Comments at peer level (e.g., between if and
                    //     else) preserve their original shallow depth
                    //     rather than being pulled into the preceding block
                    depths[i] = origDepth < stack.Count
                    ? origDepth : stack.Count;
                    previousWasColonOrBrace = false;
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
                    if (!previousWasColonOrBrace)
                    {
                        HandleNonContinuationPop(origDepth, stack,
                            continuationColonPushes);
                    }

                    previousWasColonOrBrace = false;
                }

                // For continuation lines that are NOT colon-terminated and
                // NOT close-braces, pop stack entries that were pushed by
                // colon-terminated continuation lines when the current line's
                // origDepth is at or below the colon entry's origDepth.
                // This correctly handles block exits inside continuation
                // contexts (e.g., returning to the func-body level after an
                // if-block inside a func lambda passed as a call argument).

                if (lineInfo[i].IsContinuation &&
                    !lineInfo[i].ColonTerminated &&
                    !lineInfo[i].IsCloseBrace &&
                    // Lines starting with a closing bracket are exiting the
                // continuation context — they should not trigger the
                // continuation-colon pop logic.
                trimmed.Length > 0 &&
                    trimmed[0] != ')' &&
                    trimmed[0] != ']' &&
                    trimmed[0] != '}')
                {
                    PopContinuationColonEntries(origDepth, stack,
                        continuationColonPushes);
                }

                // For colon-terminated continuation lines, also pop matching
                // continuation colon pushes when at the same origDepth.
                // This correctly handles else:/elif: which should appear at
                // the same indentation level as their matching if.
                // Lines starting with a closing bracket ()/: ]: }:) are
                // exiting the continuation context — the colon opens a new
                // block at the current stack level rather than popping
                // previous continuation colon pushes.

                if (lineInfo[i].IsContinuation &&
                    lineInfo[i].ColonTerminated &&
                    trimmed.Length > 0 &&
                    trimmed[0] != ')' &&
                    trimmed[0] != ']' &&
                    trimmed[0] != '}')
                {
                    PopContinuationColonEntries(origDepth, stack,
                        continuationColonPushes,
                        currentLineIsColonTerminated: true);
                }

                depths[i] = stack.Count;

                if (lineInfo[i].ColonTerminated ||
                    (lineInfo[i].BraceTerminated &&
                    !lineInfo[i].IsContinuation))
                {
                    stack.Add(stack.Count + 1);
                    // Only set previousWasColonOrBrace for non-continuation
                    // lines. Continuation-colon lines (e.g. func() inside a
                    // call argument's bracket context) do not represent real
                    // block nesting — their blocks are scoped within the
                    // continuation context. Setting previousWasColonOrBrace
                    // for them would prevent the next non-continuation line
                    // (e.g. a top-level func after the closing ')') from
                    // properly popping the stack.

                    if (!lineInfo[i].IsContinuation)
                    {
                        previousWasColonOrBrace = true;
                    }

                    // Record colon pushes from continuation lines so
                    // they can be properly popped when the block exits.

                    if (lineInfo[i].IsContinuation &&
                        lineInfo[i].ColonTerminated)
                    {
                        continuationColonPushes.Add(
                            (stack.Count, origDepth));
                    }
                }
            }

            return depths;
        }

        private static void HandleCloseBrace(int i, List<int> stack,
            int[] depths)
        {
            if (stack.Count > 0)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            depths[i] = stack.Count;
        }

        private static void HandleNonContinuationPop(int origDepth,
            List<int> stack,
            List<(int height, int origDepth)> continuationColonPushes)
        {
            while (stack.Count > 0 &&
                origDepth < stack[stack.Count - 1])
            {
                stack.RemoveAt(stack.Count - 1);
            }

            // When non-continuation lines pop the stack, any
            // continuationColonPushes entries whose recorded height
            // exceeds the new stack size are stale — they were
            // pushed by colon-terminated continuation lines inside
            // blocks that have now been closed. Cleaning them up
            // prevents stale entries from persisting across
            // function/block boundaries and incorrectly affecting
            // continuation lines in other scopes.

            while (continuationColonPushes.Count > 0 &&
                continuationColonPushes[

            continuationColonPushes.Count - 1].height >
                stack.Count)
            {
                continuationColonPushes.RemoveAt(
                    continuationColonPushes.Count - 1);
            }
        }

        private static void PopContinuationColonEntries(int origDepth,
            List<int> stack,
            List<(int height, int origDepth)> continuationColonPushes,
            bool currentLineIsColonTerminated = false)
        {
            while (continuationColonPushes.Count > 0)
            {
                int entryOrigDepth =
                    continuationColonPushes[
                continuationColonPushes.Count - 1].origDepth;
                // Always pop when strictly shallower (dedented).

                if (origDepth < entryOrigDepth)
                {
                    // fall through to pop
                }

                // Pop at the same depth regardless of colon termination.
                // A non-colon-terminated line at the same origDepth as a
                // continuation-colon entry exits that block (e.g. a sibling
                // statement after an if block inside a func lambda).

                else if (origDepth == entryOrigDepth)
                {
                    // fall through to pop
                }
                else
                {
                    break;
                }

                var entry =
                    continuationColonPushes[
                continuationColonPushes.Count - 1];
                int targetCount = entry.height - 1;

                while (stack.Count > targetCount)
                {
                    stack.RemoveAt(stack.Count - 1);
                }

                continuationColonPushes.RemoveAt(
                    continuationColonPushes.Count - 1);
            }
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
    }
}
