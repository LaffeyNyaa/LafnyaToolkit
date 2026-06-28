using System.Collections.Generic;

namespace CSharpFormatter
{
    /// <summary>
    /// Applies blank-line rules: ensures exactly one blank line around
    /// blocks and declarations, collapses excess blank lines, and trims
    /// trailing whitespace.
    /// </summary>
    internal static class BlankLineProcessor
    {
        /// <summary>
        /// Records a non-blank line together with its original index in
        /// the input list and whether a blank line preceded it. Used by
        /// <see cref="ApplyBlankLineRules"/> to correctly index into the
        /// <paramref name="isCodeLine"/> array after blank lines have been
        /// collapsed.
        /// </summary>
        private struct NonBlankEntry
        {
            /// <summary>The original index of this line in the input
            /// list.</summary>
            public int OriginalIndex;
            /// <summary>Whether a blank line immediately preceded this
            /// line in the input.</summary>
            public bool HadBlankAbove;
            /// <summary>The line text.</summary>
            public string Line;

            public NonBlankEntry(int originalIndex, bool hadBlankAbove,
                string line)
            {
                OriginalIndex = originalIndex;
                HadBlankAbove = hadBlankAbove;
                Line = line;
            }
        }

        /// <summary>
        /// Ensures exactly one blank line above and below blocks/
        /// declarations (applying the start/end exceptions), inserts
        /// blank lines around multi-line statements (statements split
        /// across several lines due to line-length wrapping), and
        /// suppresses blank lines between a try/catch/finally block's
        /// closing brace and the following catch/finally clause. Uses
        /// <paramref name="isCodeLine"/> to ensure only code-region
        /// keywords trigger blank-line insertion, and consults
        /// <paramref name="lineContinuesNext"/> and
        /// <paramref name="lineEndsStatement"/> to detect multi-line
        /// statement boundaries.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="isCodeLine">Per-line flag indicating whether the
        /// line's first non-whitespace character is in a code region.
        /// </param>
        /// <param name="lineContinuesNext">Per-line flag indicating
        /// whether the line ends with a continuation operator and thus
        /// continues on the next line.</param>
        /// <param name="lineEndsStatement">Per-line flag indicating
        /// whether the line ends a statement (last code character is
        /// <c>;</c> or <c>}</c>).</param>
        /// <returns>The processed line list.</returns>
        public static List<string> ApplyBlankLineRules(List<string> lines,
            bool[] isCodeLine, bool[] lineContinuesNext,
            bool[] lineEndsStatement)
        {
            var nonBlank = new List<NonBlankEntry>(lines.Count);
            bool prevWasBlank = false;
            bool isFirst = true;

            for (int idx = 0; idx < lines.Count; idx++)
            {
                string line = lines[idx];
                if (line.Trim().Length == 0)
                {
                    prevWasBlank = true;
                    continue;
                }

                bool hadBlankAbove = !isFirst && prevWasBlank;
                nonBlank.Add(new NonBlankEntry(idx, hadBlankAbove, line));
                prevWasBlank = false;
                isFirst = false;
            }

            var result = new List<string>(nonBlank.Count);
            for (int i = 0; i < nonBlank.Count; i++)
            {
                NonBlankEntry entry = nonBlank[i];
                string line = entry.Line;
                string trimmed = line.Trim();
                int origIdx = entry.OriginalIndex;
                bool lineIsCode = origIdx < isCodeLine.Length &&
                    isCodeLine[origIdx];
                bool isBlockStart = lineIsCode &&
                    TextUtils.IsBlockStartLine(trimmed);
                bool wantBlankAbove = false;

                if (result.Count > 0)
                {
                    NonBlankEntry prevEntry = nonBlank[i - 1];
                    int prevOrigIdx = prevEntry.OriginalIndex;
                    string prevLine = prevEntry.Line;
                    string prevTrimmed = prevLine.Trim();
                    bool prevIsCode = prevOrigIdx < isCodeLine.Length &&
                        isCodeLine[prevOrigIdx];

                    bool prevIsBlockEnd = TextUtils.IsBlockEndLine(prevTrimmed);
                    bool prevIsBlockStartBrace = prevTrimmed == "{" ||
                        TextUtils.EndsWithOpenBrace(prevTrimmed);
                    bool prevIsComment = IsCommentLine(prevTrimmed);

                    // Existing rule: block-start gets a blank above unless the
                    // previous line opens a block.
                    if (isBlockStart && prevTrimmed.Length > 0 &&
                        !prevIsBlockStartBrace)
                    {
                        wantBlankAbove = true;
                    }

                    // Existing rule: after a block-end, add a blank above unless
                    // the current line is itself a block-end.
                    if (!wantBlankAbove && prevIsBlockEnd &&
                        trimmed.Length > 0 && trimmed != "}" &&
                        !trimmed.StartsWith("}"))
                    {
                        wantBlankAbove = true;
                    }

                    // Existing rule: preserve blank lines between using directives
                    // when the input already had one.
                    if (!wantBlankAbove &&
                        TextUtils.IsUsingDirective(trimmed) &&
                        TextUtils.IsUsingDirective(prevTrimmed) &&
                        entry.HadBlankAbove)
                    {
                        wantBlankAbove = true;
                    }

                    // NEW rule: multi-line statement end -> add a blank below (i.e.,
                    // add a blank above the current line). A multi-line-statement
                    // end is a line that ends a statement (; or }) AND whose
                    // previous non-blank line was a continuation (ended with a
                    // continuation operator). Block-tail exception: do not add a
                    // blank if the current line is itself a block-end (} or };).
                    // NOTE: We check lineContinuesNext[prevOrigIdx - 1] (whether the
                    // line BEFORE prev continued into prev), not
                    // lineContinuesNext[prevOrigIdx] (whether prev continues to its
                    // next line). The latter is always false for a statement-end
                    // line, which would silently disable this rule.
                    bool prevIsMultiLineEnd = prevIsCode &&
                        prevOrigIdx > 0 &&
                        prevOrigIdx < lineEndsStatement.Length &&
                        lineEndsStatement[prevOrigIdx] &&
                        (prevOrigIdx - 1) < lineContinuesNext.Length &&
                        lineContinuesNext[prevOrigIdx - 1];
                    bool currentIsBlockEnd = TextUtils.IsBlockEndLine(trimmed);

                    if (!wantBlankAbove && prevIsMultiLineEnd &&
                        !currentIsBlockEnd)
                    {
                        wantBlankAbove = true;
                    }

                    // NEW rule: multi-line statement start -> add a blank above. A
                    // multi-line-statement start is a line that ends with a
                    // continuation operator AND whose previous non-blank line was
                    // NOT a continuation. Block-head exception: previous line is
                    // "{" or ends with "{". Comment-attachment exception: previous
                    // line is a comment (the comment is attached to the
                    // declaration).
                    bool currentContinues = lineIsCode &&
                        origIdx < lineContinuesNext.Length &&
                        lineContinuesNext[origIdx];
                    bool prevLineContinuedIntoCurrent = origIdx > 0 &&
                        (origIdx - 1) < lineContinuesNext.Length &&
                        lineContinuesNext[origIdx - 1];
                    bool currentIsMultiLineStart = currentContinues &&
                        !prevLineContinuedIntoCurrent;

                    if (!wantBlankAbove && currentIsMultiLineStart &&
                        !prevIsBlockStartBrace && !prevIsComment)
                    {
                        wantBlankAbove = true;
                    }

                    // NEW rule: try/catch/finally suppression. If the current line
                    // starts with "catch" or "finally" (in a code region) and the
                    // previous non-blank line is a block-end (} or };), suppress
                    // any blank above. This overrides the block-end rule and the
                    // multi-line rules above so that try/catch/finally clauses sit
                    // directly adjacent to the preceding block's closing brace.
                    bool currentIsCatchOrFinally = lineIsCode &&
                        (TextUtils.StartsWithKeyword(trimmed, "catch") ||
                         TextUtils.StartsWithKeyword(trimmed, "finally"));

                    if (currentIsCatchOrFinally && prevIsBlockEnd)
                    {
                        wantBlankAbove = false;
                    }
                }

                if (wantBlankAbove)
                {
                    result.Add(string.Empty);
                }

                result.Add(line);
            }

            return result;
        }

        /// <summary>
        /// Determines whether a trimmed line is a comment line (single-line
        /// comment, XML doc comment, or block-comment continuation/end).
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>true if the line is a comment line.</returns>
        private static bool IsCommentLine(string trimmed)
        {
            return trimmed.StartsWith("//") || trimmed.StartsWith("/*") ||
                trimmed.StartsWith("*");
        }

        /// <summary>
        /// Collapses 2 or more consecutive blank lines into 1.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <returns>The processed line list.</returns>
        public static List<string> CollapseBlankLines(List<string> lines)
        {
            var result = new List<string>(lines.Count);
            int blankRun = 0;

            foreach (var line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    blankRun++;
                    if (blankRun <= 1)
                    {
                        result.Add(string.Empty);
                    }
                }

                else
                {
                    blankRun = 0;
                    result.Add(line);
                }
            }

            return result;
        }

        /// <summary>
        /// Strips trailing whitespace from each line.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <returns>The processed line list.</returns>
        public static List<string> TrimTrailingWhitespace(
            List<string> lines)
        {
            var result = new List<string>(lines.Count);

            foreach (var line in lines)
            {
                result.Add(line.TrimEnd());
            }

            return result;
        }
    }
}
