using System.Collections.Generic;

namespace GDScriptFormatter
{
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns 1 blank line around multi-line statements: when the previous
        /// non-blank line was a continuation and the current line is not (unless
        /// entering a deeper block or the continuation is a block header), when
        /// a following line is a continuation, or when the current line ends with
        /// an opening brace and the next line is indented deeper (brace-terminated
        /// multi-line construct).
        /// </summary>
        private static int ApplyMultiLineStatementBlankRule(List<NonBlankEntry>
            nonBlank, int curIdx, int curIndent, int prevIndent)
        {
            // Multi-line statement below: if the immediate previous
            // non-blank line was a continuation and the current line is
            // not, add a blank line (unless entering a deeper block,
            // e.g. a multi-line if-condition followed by its body).
            // Also skip when the continuation line itself is a block
            // start (ends with colon) — that is an if/for/while header
            // whose body should stay attached.

            if (curIdx > 0 &&
                nonBlank[curIdx - 1].IsContinuation &&
                !nonBlank[curIdx].IsContinuation &&
                curIndent <= prevIndent &&
                !TextUtils.IsBlockStartLine(
                nonBlank[curIdx - 1].Line.Trim()))
            {
                return 1;
            }

            // Multi-line statement above: if the next non-blank line is a
            // continuation and the current line is not, add a blank line
            // before the multi-line statement begins. Only applies when
            // the current line is a peer of the previous line (same
            // indent) — not when entering a new block.

            if (curIdx + 1 < nonBlank.Count &&
                nonBlank[curIdx + 1].IsContinuation &&
                !nonBlank[curIdx].IsContinuation &&
                prevIndent == curIndent)
            {
                return 1;
            }

            // Brace-terminated multi-line: when the current line ends with
            // an opening brace '{' (e.g., a dictionary literal) and the next
            // non-blank line is indented deeper, insert a blank line above.
            // This handles cases where the brace is consumed by
            // IndentationProcessor (marked as BraceTerminated) so the
            // continuation mechanism does not apply.

            if (curIdx + 1 < nonBlank.Count &&
                !nonBlank[curIdx].IsContinuation &&
                nonBlank[curIdx].Line.Trim().EndsWith("{") &&
                nonBlank[curIdx + 1].Indent > curIndent &&
                prevIndent == curIndent)
            {
                return 1;
            }

            // Continuation-aware: multi-line statement just closed.
            // When the previous line starts with a closing bracket
            // and the current line is at the same or shallower indent,
            // the multi-line construct ended — add a blank line.

            if (curIdx > 0 &&
                nonBlank[curIdx - 1].IsContinuation &&
                nonBlank[curIdx].IsContinuation)
            {
                string prevTrimmedCheck = nonBlank[curIdx - 1].Line.Trim();

                if ((prevTrimmedCheck.StartsWith(")") ||
                    prevTrimmedCheck.StartsWith("]") ||
                    prevTrimmedCheck.StartsWith("}")) &&
                    nonBlank[curIdx - 1].Indent >= nonBlank[curIdx].Indent)
                {
                    return 1;
                }
            }

            // Continuation-aware: multi-line statement just started.
            // If the current line ends with '(', '[', or '{' and the
            // next non-blank line is also a continuation, this line
            // opens a multi-line construct — add a blank line above.

            if (curIdx + 1 < nonBlank.Count &&
                nonBlank[curIdx].IsContinuation &&
                nonBlank[curIdx + 1].IsContinuation &&
                prevIndent == curIndent)
            {
                string curTrimmed = nonBlank[curIdx].Line.Trim();

                if (curTrimmed.EndsWith("(") ||
                    curTrimmed.EndsWith("[") ||
                    curTrimmed.EndsWith("{"))
                {
                    return 1;
                }
            }

            return 0;
        }
    }
}
