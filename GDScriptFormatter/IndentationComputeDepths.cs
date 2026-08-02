using System.Collections.Generic;

namespace GDScriptFormatter
{
    /// <summary>
    /// Stack-based indentation computation. Each line's depth is
    /// derived from the running block stack rather than from raw
    /// original indentation, so the result is independent of how the
    /// source happened to be indented. Includes the named
    /// continuation-colon stack that lets the algorithm handle
    /// block-level constructs (func, if, for, while, match, elif,
    /// else) introduced inside unclosed brackets.
    /// </summary>
    public sealed partial class IndentationProcessor
    {
        /// <summary>
        /// Stack-based indentation computation from original
        /// indentation depth: colon-terminated lines and
        /// brace-terminated lines open a new block, indenting
        /// subsequent lines by +1; close-brace lines and returning to
        /// shallower indentation pop blocks. Lines inside triple-quoted
        /// strings (<paramref name="preserveIndent"/>[i] == true) are
        /// skipped for stack manipulation so that their content-leading
        /// does not incorrectly pop the block stack.
        /// </summary>
        /// <param name="lines">The input lines.</param>
        /// <param name="lineInfo">The per-line analysis.</param>
        /// <param name="preserveIndent">Per-line flag indicating lines inside triple-quoted strings.</param>
        /// <returns>The depth for each line.</returns>
        public int[] ComputeDepthsFromStack(
            List<string> lines,
            LineAnalysis[] lineInfo,
            bool[] preserveIndent
        )
        {
            int[] depths = new int[lines.Count];
            var stack = new List<int>();

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

                if (preserveIndent[i])
                {
                    depths[i] = stack.Count;
                    continue;
                }

                int origDepth = lineInfo[i].OriginalDepth;

                if (lineInfo[i].IsCloseBrace && !lineInfo[i].IsContinuation)
                {
                    HandleCloseBrace(
                        stack,
                        depths,
                        i
                    );
                    previousWasColonOrBrace = false;
                    continue;
                }

                if (trimmed.StartsWith("#"))
                {
                    depths[i] = origDepth < stack.Count
                    ? origDepth : stack.Count;
                    previousWasColonOrBrace = false;
                    continue;
                }

                if (!lineInfo[i].IsContinuation)
                {
                    if (!previousWasColonOrBrace)
                    {
                        HandleNonContinuationPop(
                            origDepth,
                            stack,
                            continuationColonPushes
                        );
                    }

                    previousWasColonOrBrace = false;
                }

                if (lineInfo[i].IsContinuation &&
                    !lineInfo[i].ColonTerminated &&
                    !lineInfo[i].IsCloseBrace &&
                    trimmed.Length > 0 &&
                    trimmed[0] != ')' &&
                    trimmed[0] != ']' &&
                    trimmed[0] != '}')
                {
                    PopContinuationColonEntries(
                        origDepth,
                        stack,
                        continuationColonPushes
                    );
                }

                if (lineInfo[i].IsContinuation &&
                    lineInfo[i].ColonTerminated &&
                    trimmed.Length > 0 &&
                    trimmed[0] != ')' &&
                    trimmed[0] != ']' &&
                    trimmed[0] != '}')
                {
                    PopContinuationColonEntries(
                        origDepth,
                        stack,
                        continuationColonPushes,
                        currentLineIsColonTerminated: true
                    );
                }

                if (lineInfo[i].IsContinuation &&
                    trimmed.Length > 0 &&
                    (trimmed[0] == ')' || trimmed[0] == ']' ||
                        trimmed[0] == '}') &&
                    lineInfo[i].EndBracketDepth == 0)
                {
                    PopContinuationColonEntries(
                        origDepth,
                        stack,
                        continuationColonPushes,
                        currentLineIsColonTerminated: false
                    );
                }

                depths[i] = stack.Count;

                if (lineInfo[i].ColonTerminated ||
                    (lineInfo[i].BraceTerminated &&
                        !lineInfo[i].IsContinuation))
                {
                    stack.Add(stack.Count + 1);

                    if (!lineInfo[i].IsContinuation)
                    {
                        previousWasColonOrBrace = true;
                    }

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

        /// <summary>
        /// Pops the block stack once for a close-brace line and records
        /// the new depth.
        /// </summary>
        /// <param name="stack">The running block stack.</param>
        /// <param name="depths">The depth array to update.</param>
        /// <param name="i">The current line index.</param>
        private static void HandleCloseBrace(
            List<int> stack,
            int[] depths,
            int i
        )
        {
            if (stack.Count > 0)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            depths[i] = stack.Count;
        }

        /// <summary>
        /// Pops the block stack down to the line's original depth and
        /// cleans up any continuation-colon entries that have become
        /// stale (their recorded height exceeds the new stack size, or
        /// their origDepth is &gt;= the current line's origDepth).
        /// </summary>
        /// <param name="origDepth">The current line's original depth.</param>
        /// <param name="stack">The running block stack.</param>
        /// <param name="continuationColonPushes">The continuation-colon stack.</param>
        private static void HandleNonContinuationPop(
            int origDepth,
            List<int> stack,
            List<(int height, int origDepth)> continuationColonPushes
        )
        {
            while (stack.Count > 0 &&
                origDepth < stack[stack.Count - 1])
            {
                stack.RemoveAt(stack.Count - 1);
            }

            while (continuationColonPushes.Count > 0)
            {
                var entry =
                    continuationColonPushes[
                continuationColonPushes.Count - 1];

                if (entry.height > stack.Count ||
                    entry.origDepth >= origDepth)
                {
                    continuationColonPushes.RemoveAt(
                        continuationColonPushes.Count - 1);
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Pops continuation-colon entries whose recorded origDepth is
        /// at or above the current line's origDepth, restoring the
        /// stack height to the height recorded in the entry minus one.
        /// Used by the various continuation-line cases in
        /// <see cref="ComputeDepthsFromStack"/>.
        /// </summary>
        /// <param name="origDepth">The current line's original depth.</param>
        /// <param name="stack">The running block stack.</param>
        /// <param name="continuationColonPushes">The continuation-colon stack.</param>
        /// <param name="currentLineIsColonTerminated">Whether the current line is itself colon-terminated (used to determine the comparison policy).</param>
        private static void PopContinuationColonEntries(
            int origDepth,
            List<int> stack,
            List<(int height, int origDepth)> continuationColonPushes,
            bool currentLineIsColonTerminated = false
        )
        {
            while (continuationColonPushes.Count > 0)
            {
                int entryOrigDepth =
                    continuationColonPushes[
                continuationColonPushes.Count - 1].origDepth;

                if (origDepth < entryOrigDepth)
                {
                }
                else if (origDepth == entryOrigDepth)
                {
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
    }
}
