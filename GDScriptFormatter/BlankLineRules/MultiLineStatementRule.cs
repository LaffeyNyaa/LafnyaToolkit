using System.Collections.Generic;

using LafnyaToolkit.Core.Text;

namespace GDScriptFormatter
{
    /// <summary>
    /// Multi-line-statement rule: returns 1 blank line around
    /// multi-line statements: when the previous non-blank line was a
    /// continuation and the current line is not (unless entering a
    /// deeper block or the continuation is a block header), when a
    /// following line is a continuation, or when the current line
    /// ends with an opening brace and the next line is indented
    /// deeper (brace-terminated multi-line construct).
    /// </summary>
    public sealed partial class BlankLineProcessor
    {
        private static int ApplyMultiLineStatementBlankRule(
            List<NonBlankEntry> nonBlank, List<bool> contList, int curIdx,
            int curIndent, int prevIndent)
        {
            if (curIdx > 0 &&
                contList[curIdx - 1] &&
                !contList[curIdx] &&
                curIndent <= prevIndent &&
                !GDScriptTextUtils.Instance.IsBlockStartLine(
                nonBlank[curIdx - 1].Line.Trim()))
            {
                return 1;
            }

            if (curIdx + 1 < nonBlank.Count &&
                contList[curIdx + 1] &&
                !contList[curIdx] &&
                prevIndent == curIndent)
            {
                return 1;
            }

            if (curIdx + 1 < nonBlank.Count &&
                !contList[curIdx] &&
                nonBlank[curIdx].Line.Trim().EndsWith("{") &&
                nonBlank[curIdx + 1].Line.Trim().Length > 0 &&
                IndentationProcessor.Instance.LineIndentLevel(
                nonBlank[curIdx + 1].Line) > curIndent &&
                prevIndent == curIndent)
            {
                return 1;
            }

            if (curIdx > 0 &&
                contList[curIdx - 1] &&
                contList[curIdx])
            {
                string prevTrimmedCheck = nonBlank[curIdx - 1].Line.Trim();

                if ((prevTrimmedCheck.StartsWith(")") ||
                    prevTrimmedCheck.StartsWith("]") ||
                    prevTrimmedCheck.StartsWith("}")) &&
                    IndentationProcessor.Instance.LineIndentLevel(
                    nonBlank[curIdx - 1].Line) >=
                    IndentationProcessor.Instance.LineIndentLevel(
                    nonBlank[curIdx].Line))
                {
                    return 1;
                }
            }

            if (curIdx + 1 < nonBlank.Count &&
                contList[curIdx] &&
                contList[curIdx + 1] &&
                prevIndent == curIndent)
            {
                string curTrimmed = nonBlank[curIdx].Line.Trim();

                if (curTrimmed.EndsWith("(") ||
                    curTrimmed.EndsWith("{") ||
                    (curTrimmed.EndsWith("[") &&
                    !(curIdx > 0 &&
                    contList[curIdx - 1] &&
                    curTrimmed.StartsWith("%"))))
                {
                    return 1;
                }
            }

            return 0;
        }
    }
}
