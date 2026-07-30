using System.Collections.Generic;
using LafnyaToolkit.Core.Text;

namespace GDScriptFormatter
{
    /// <summary>
    /// Applies blank-line rules: ensures the correct number of blank
    /// lines around blocks and declarations, collapses excess blank
    /// lines, and trims trailing whitespace. Split into a primary
    /// orchestrator (this file) plus per-rule helpers under
    /// <c>BlankLineRules/</c> and shared helpers in
    /// <c>BlankLineHelpers.cs</c> / <c>BlankLinePostProcess.cs</c>.
    /// </summary>
    public sealed partial class BlankLineProcessor
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly BlankLineProcessor Instance = new BlankLineProcessor();

        private BlankLineProcessor()
        {
        }

        /// <summary>
        /// Ensures the correct number of blank lines above and below
        /// blocks/declarations, including: one blank line above and
        /// below code blocks and multi-line statements, two blank
        /// lines above and below func/nested class declarations (only
        /// at the same indentation depth), one blank line after
        /// file-level header lines, one blank line between different
        /// variable groups, and comments attached to the following
        /// declaration.
        /// </summary>
        /// <param name="lines">The input lines.</param>
        /// <param name="isContinuation">Per-line continuation flag; entry i corresponds to line i. May be null when continuation detection is not available.</param>
        /// <returns>The lines with blank-line rules applied.</returns>
        public List<string> ApplyBlankLineRules(List<string> lines,
            bool[] isContinuation)
        {
            var nonBlank = new List<NonBlankEntry>(lines.Count);
            var hadBlankAboveList = new List<bool>(lines.Count);
            var contList = new List<bool>(lines.Count);
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
                int idx = lineIdx;

                bool cont = isContinuation != null &&
                    lineIdx < isContinuation.Length &&
                    isContinuation[lineIdx];

                nonBlank.Add(new NonBlankEntry(line, idx, false));
                hadBlankAboveList.Add(hadBlankAbove);
                contList.Add(cont);

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
                int lineIndent = IndentationProcessor.Instance.LineIndentLevel(line);
                int wantBlankAbove = 0;

                if (result.Count > 0)
                {
                    string prevTrimmed = result[result.Count - 1].Trim();
                    int prevIndent = resultIndents[resultIndents.Count - 1];

                    wantBlankAbove = ComputeDesiredBlanksAbove(
                        prevTrimmed, trimmed, nonBlank, hadBlankAboveList,
                        contList, i, prevIndent, lineIndent);
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

            result = RemoveBlanksBeforeClosingBraces(result);
            result = AddBlankAfterClosingBraces(result);

            return result;
        }

        /// <summary>
        /// Computes how many blank lines should appear above the
        /// current line.
        /// </summary>
        /// <param name="prevTrimmed">The previous emitted line, trimmed.</param>
        /// <param name="curTrimmed">The current line, trimmed.</param>
        /// <param name="nonBlank">The list of non-blank entries.</param>
        /// <param name="hadBlankAbove">Per-entry flag indicating whether a blank line existed above the entry in the original input.</param>
        /// <param name="contList">Per-entry flag indicating whether the entry is a continuation of the previous line.</param>
        /// <param name="curIdx">The current index in the non-blank list.</param>
        /// <param name="prevIndent">The previous line's indent level.</param>
        /// <param name="curIndent">The current line's indent level.</param>
        /// <returns>The desired number of blank lines above the current line.</returns>
        private static int ComputeDesiredBlanksAbove(string prevTrimmed,
            string curTrimmed, List<NonBlankEntry> nonBlank,
            List<bool> hadBlankAbove, List<bool> contList, int curIdx,
            int prevIndent, int curIndent)
        {
            if (curTrimmed.Length == 0)
            {
                return 0;
            }

            if (prevTrimmed.Length == 0)
            {
                return 0;
            }

            if (contList[curIdx])
            {
                if (curIdx == 0 || !contList[curIdx - 1])
                {
                    return 0;
                }
            }

            if (IsAttachedComment(prevTrimmed, curTrimmed, nonBlank,
                hadBlankAbove, curIdx))
            {
                return 0;
            }

            bool sameIndent = prevIndent == curIndent;
            bool deeperThanPrev = curIndent > prevIndent;

            int want = 0;

            if (!contList[curIdx])
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
                        sameIndent, nonBlank, contList, curIdx);
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
                        nonBlank, hadBlankAbove, curIdx);
                }

                if (want == 0)
                {
                    want = ApplyDedentBlankRule(curIndent, prevIndent);
                }
            }

            if (want == 0 && contList[curIdx] &&
                GDScriptTextUtils.Instance.IsBlockStartLine(curTrimmed) &&
                !IsElifOrElseBlock(curTrimmed) &&
                !GDScriptTextUtils.Instance.IsBlockStartLine(prevTrimmed) &&
                !prevTrimmed.EndsWith("(") &&
                prevIndent <= curIndent &&
                !nonBlank[curIdx - 1].Line.TrimEnd().EndsWith("\\"))
            {
                want = 1;
            }

            if (want == 0 && contList[curIdx] &&
                curIndent < prevIndent &&
                !curTrimmed.StartsWith("#") &&
                !curTrimmed.StartsWith(")") &&
                !curTrimmed.StartsWith("]") &&
                !curTrimmed.StartsWith("}") &&
                !IsElifOrElseBlock(curTrimmed))
            {
                want = 1;
            }

            if (want == 0)
            {
                want = ApplyPreserveAuthorBlankRule(hadBlankAbove, curIdx,
                    prevTrimmed, curTrimmed);
            }

            if (want == 0)
            {
                want = ApplyMultiLineStatementBlankRule(nonBlank, contList,
                    curIdx, curIndent, prevIndent);
            }

            if (ApplyAnnotationSuppressRule(prevTrimmed, curTrimmed) != 0)
            {
                return 0;
            }

            if (IsElifOrElseBlock(curTrimmed))
            {
                return 0;
            }

            return want;
        }
    }
}
