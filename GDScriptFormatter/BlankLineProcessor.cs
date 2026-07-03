using System.Collections.Generic;

using static GDScriptFormatter.DeclarationClassifier;
using static GDScriptFormatter.MemberClassifier;

namespace GDScriptFormatter
{
    /// <summary>
    /// Applies blank-line rules: ensures the correct number of blank lines
    /// around blocks and declarations, collapses excess blank lines, and
    /// trims trailing whitespace.
    /// </summary>
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Ensures the correct number of blank lines above and below blocks/declarations, including:
        /// - one blank line above and below code blocks and multi-line statements
        /// - two blank lines above and below func/nested class declarations (only at the same indentation depth)
        /// - one blank line after file-level header lines
        /// - one blank line between different variable groups
        /// - comments attached to the following declaration
        /// </summary>
        internal static List<string> ApplyBlankLineRules(List<string> lines,
            bool[] isContinuation)
        {
            var nonBlank = new List<NonBlankEntry>(lines.Count);
            bool prevWasBlank = false;
            bool isFirst = true;
            int lineIdx = 0;

            foreach (var line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    prevWasBlank = true;
                    lineIdx++;
                    continue;
                }

                bool hadBlankAbove = !isFirst && prevWasBlank;
                int indent = IndentationProcessor.LineIndentLevel(line);

                bool cont = isContinuation != null &&
                    lineIdx < isContinuation.Length &&
                    isContinuation[lineIdx];

                nonBlank.Add(new NonBlankEntry(hadBlankAbove, line, indent,
                    cont));

                prevWasBlank = false;
                isFirst = false;
                lineIdx++;
            }

            var result = new List<string>(nonBlank.Count);
            var resultIndents = new List<int>(nonBlank.Count);
            int currentBlanksAbove = 0;

            for (int i = 0; i < nonBlank.Count; i++)
            {
                string line = nonBlank[i].Line;
                string trimmed = line.Trim();
                int lineIndent = nonBlank[i].Indent;
                int wantBlankAbove = 0;

                if (result.Count > 0)
                {
                    string prevTrimmed = result[result.Count - 1].Trim();
                    int prevIndent = resultIndents[resultIndents.Count - 1];

                    wantBlankAbove = ComputeDesiredBlanksAbove(
                        prevTrimmed, trimmed, nonBlank, i,
                        prevIndent, lineIndent);
                }

                while (currentBlanksAbove < wantBlankAbove)
                {
                    result.Add(string.Empty);
                    resultIndents.Add(-1);
                    currentBlanksAbove++;
                }

                while (currentBlanksAbove > wantBlankAbove)
                {
                    result.RemoveAt(result.Count - 1);
                    resultIndents.RemoveAt(resultIndents.Count - 1);
                    currentBlanksAbove--;
                }

                result.Add(line);
                resultIndents.Add(lineIndent);
                currentBlanksAbove = 0;
            }

            // Post-processing: remove blank lines immediately before closing braces
            result = RemoveBlanksBeforeClosingBraces(result);
            // Post-processing: add blank lines after closing braces when followed by a statement at same indent
            result = AddBlankAfterClosingBraces(result);

            return result;
        }

        /// <summary>
        /// Computes how many blank lines should appear above the current line.
        /// </summary>
        private static int ComputeDesiredBlanksAbove(string prevTrimmed,
            string curTrimmed, List<NonBlankEntry> nonBlank, int curIdx,
            int prevIndent, int curIndent)
        {
            // Guard clauses

            if (curTrimmed.Length == 0)
            {
                return 0;
            }

            if (prevTrimmed.Length == 0)
            {
                return 0;
            }

            // If the current line is a continuation of the previous line
            // (unclosed bracket or backslash), only allow blank lines
            // between two continuation lines (handled by the multi-line
            // statement rules below).  When transitioning from a
            // non-continuation to a continuation line, never insert
            // blank lines — the opening bracket line stays attached.

            if (nonBlank[curIdx].IsContinuation)
            {
                if (curIdx == 0 || !nonBlank[curIdx - 1].IsContinuation)
                {
                    return 0;
                }
            }

            if (IsAttachedComment(prevTrimmed, curTrimmed, nonBlank, curIdx))
            {
                return 0;
            }

            bool sameIndent = prevIndent == curIndent;
            bool deeperThanPrev = curIndent > prevIndent;

            int want = 0;
            // Continuation lines (anonymous func() lambdas inside argument
            // lists, etc.) should not trigger most of the formatting rules.
            // Only the preserve-author and multi-line statement rules apply.

            if (!nonBlank[curIdx].IsContinuation)
            {
                want = ApplyFuncClassBlankRule(prevTrimmed, curTrimmed,
                    sameIndent);

                if (want == 0)
                {
                    want = ApplyBlockStartBlankRule(prevTrimmed, curTrimmed,
                        sameIndent, deeperThanPrev);
                }

                if (want == 0)
                {
                    want = ApplyTopLevelMemberBlankRule(prevTrimmed, curTrimmed,
                        sameIndent, nonBlank, curIdx);
                }

                if (want == 0)
                {
                    want = ApplySetterGetterBlockRule(prevTrimmed, curTrimmed,
                        sameIndent);
                }

                if (want == 0)
                {
                    want = ApplyFileHeaderBlankRule(prevTrimmed, curTrimmed,
                        deeperThanPrev);
                }

                if (want == 0)
                {
                    want = ApplyDocCommentBlankRule(prevTrimmed, curTrimmed,
                        nonBlank, curIdx);
                }

                if (want == 0)
                {
                    want = ApplyDedentBlankRule(curIndent, prevIndent);
                }
            }

            // Preserve author-inserted blank lines (applies to all lines,
            // including continuations).

            if (want == 0)
            {
                want = ApplyPreserveAuthorBlankRule(nonBlank[curIdx],
                    prevTrimmed, curTrimmed);
            }

            if (want == 0)
            {
                want = ApplyMultiLineStatementBlankRule(nonBlank, curIdx,
                    curIndent, prevIndent);
            }

            // Annotation suppression override (checked last so it can
            // override previous rules)

            if (ApplyAnnotationSuppressRule(prevTrimmed, curTrimmed) != 0)
            {
                return 0;
            }

            // elif/else suppression: never insert blank lines before elif
            // or else — they are continuations of the preceding if/elif
            // block and should remain adjacent.

            if (IsElifOrElseBlock(curTrimmed))
            {
                return 0;
            }

            return want;
        }
    }
}
