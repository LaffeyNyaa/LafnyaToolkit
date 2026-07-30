using System.Collections.Generic;
using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace CSharpFormatter
{
    /// <summary>
    /// Applies blank-line rules: ensures exactly one blank line
    /// around blocks and declarations, collapses excess blank lines,
    /// and trims trailing whitespace. Split into this primary
    /// orchestrator plus per-rule files under
    /// <c>BlankLineRules/</c> and shared helpers in
    /// <c>BlankLineRules/BlankLineHelpers.cs</c>.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly BlankLineProcessor Instance = new BlankLineProcessor();

        private BlankLineProcessor()
        {
        }

        /// <summary>
        /// Ensures exactly one blank line above and below blocks/
        /// declarations (applying the start/end exceptions), inserts
        /// blank lines around multi-line statements (statements split
        /// across several lines due to line-length wrapping), and
        /// suppresses blank lines between a try/catch/finally block's
        /// closing brace and the following catch/finally clause.
        /// Uses <paramref name="isCodeLine"/> to ensure only
        /// code-region keywords trigger blank-line insertion, and
        /// consults <paramref name="lineContinuesNext"/> and
        /// <paramref name="lineEndsStatement"/> to detect multi-line
        /// statement boundaries.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="isCodeLine">Per-line flag indicating whether the line's first non-whitespace character is in a code region.</param>
        /// <param name="lineContinuesNext">Per-line flag indicating whether the line ends with a continuation operator and thus continues on the next line.</param>
        /// <param name="lineEndsStatement">Per-line flag indicating whether the line ends a statement (last code character is <c>;</c> or <c>}</c>).</param>
        /// <returns>The processed line list.</returns>
        internal List<string> ApplyBlankLineRules(List<string> lines,
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

                BlankLinePredicates p = ComputePredicates(entry, i,
                    nonBlank, trimmed, isCodeLine, lineContinuesNext,
                    lineEndsStatement);

                if (BlankLineMainRules.Dispatch(p, result))
                {
                    result.Add(string.Empty);
                }

                result.Add(line);
            }

            return result;
        }

        /// <summary>
        /// Collapses 2 or more consecutive blank lines into 1.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <returns>The processed line list.</returns>
        internal List<string> CollapseBlankLines(List<string> lines)
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
        internal List<string> TrimTrailingWhitespace(
            List<string> lines)
        {
            var result = new List<string>(lines.Count);

            foreach (var line in lines)
            {
                result.Add(line.TrimEnd());
            }

            return result;
        }

        /// <summary>
        /// Computes the predicates for the current non-blank entry.
        /// Centralises all pre-condition checks used by the per-rule
        /// methods.
        /// </summary>
        /// <param name="entry">The current entry.</param>
        /// <param name="index">The index in <paramref name="nonBlank"/>.</param>
        /// <param name="nonBlank">The full non-blank list.</param>
        /// <param name="trimmed">The current line, trimmed.</param>
        /// <param name="isCodeLine">Per-line code-region flag array.</param>
        /// <param name="lineContinuesNext">Per-line continuation-flag array.</param>
        /// <param name="lineEndsStatement">Per-line statement-end flag array.</param>
        /// <returns>The populated predicates struct.</returns>
        private static BlankLinePredicates ComputePredicates(
            NonBlankEntry entry, int index,
            List<NonBlankEntry> nonBlank, string trimmed,
            bool[] isCodeLine, bool[] lineContinuesNext,
            bool[] lineEndsStatement)
        {
            int origIdx = entry.OriginalIndex;
            int prevOrigIdx = index > 0 ? nonBlank[index - 1].OriginalIndex : -1;
            string prevTrimmed = index > 0
                ? nonBlank[index - 1].Line.Trim() : string.Empty;

            var p = new BlankLinePredicates
            {
                Trimmed = trimmed,
                PrevTrimmed = prevTrimmed,
                EntryHadBlankAbove = entry.HadBlankAbove
            };

            p.LineIsCode = origIdx < isCodeLine.Length &&
                isCodeLine[origIdx];

            p.IsBlockStart = p.LineIsCode &&
                LineClassifier.Instance.IsBlockStartLine(trimmed);

            p.PrevIsCode = prevOrigIdx >= 0 &&
                prevOrigIdx < isCodeLine.Length &&
                isCodeLine[prevOrigIdx];

            p.PrevIsBlockEnd =
                LineClassifier.Instance.IsBlockEndLine(prevTrimmed);

            p.CurrentIsBlockEnd =
                LineClassifier.Instance.IsBlockEndLine(trimmed);

            p.PrevIsBlockStartBrace = prevTrimmed == "{" ||
                TextUtils.EndsWithOpenBrace(prevTrimmed);

            p.PrevIsComment = BlankLineHelpers.IsCommentLine(prevTrimmed);

            p.PrevIsDocComment = prevTrimmed.StartsWith("///");

            p.PrevIsRegularComment = !p.PrevIsDocComment && p.PrevIsComment;

            p.CurrentIsDocComment = trimmed.StartsWith("///");

            p.CurrentIsCatchOrFinally = p.LineIsCode &&
                (TextUtils.StartsWithKeyword(trimmed, "catch") ||
                TextUtils.StartsWithKeyword(trimmed, "finally"));

            p.CurrentIsElse = p.LineIsCode &&
                TextUtils.StartsWithKeyword(trimmed, "else");

            p.CurrentContinues = p.LineIsCode &&
                origIdx < lineContinuesNext.Length &&
                lineContinuesNext[origIdx];

            p.PrevLineContinuedIntoCurrent = origIdx > 0 &&
                (origIdx - 1) < lineContinuesNext.Length &&
                lineContinuesNext[origIdx - 1];

            p.CurrentIsMultiLineStart = p.CurrentContinues &&
                !p.PrevLineContinuedIntoCurrent;

            p.PrevIsMultiLineEnd = p.PrevIsCode &&
                prevOrigIdx > 0 &&
                prevOrigIdx < lineEndsStatement.Length &&
                lineEndsStatement[prevOrigIdx] &&
                (prevOrigIdx - 1) < lineContinuesNext.Length &&
                lineContinuesNext[prevOrigIdx - 1];

            p.CurrentIsPlainStmt = BlankLineHelpers.IsPlainSingleLineStatement(
                trimmed, origIdx, isCodeLine, lineEndsStatement);

            p.PrevIsPlainStmt = BlankLineHelpers.IsPlainSingleLineStatement(
                prevTrimmed, prevOrigIdx, isCodeLine, lineEndsStatement);

            return p;
        }
    }
}
