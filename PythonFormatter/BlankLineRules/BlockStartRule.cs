namespace PythonFormatter
{
    /// <summary>
    /// Block-start rule: returns a blank line above a block-start
    /// keyword line (def, class, if, for, while, try, with, match,
    /// case, async def) when the previous non-blank line is not a
    /// block-opening construct (such as a decorator or another
    /// block-start keyword at the same level).
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when the
        /// current line is a block-start keyword and the previous
        /// non-blank line is not a decorator and not itself a
        /// block-start at the same or deeper level.
        /// </summary>
        internal BlankLineRuleResult ApplyBlockStartRule(
            PythonNonBlankEntry entry, PythonNonBlankEntry prevEntry)
        {
            string trimmed = entry.Line.TrimStart();
            string prevTrimmed = prevEntry.Line.TrimStart();

            if (entry.PrevIndent < 0)
            {
                return BlankLineRuleResult.None;
            }

            if (LineClassifier.Instance.IsDecoratorLine(prevTrimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (LineClassifier.Instance.IsBlockContinuationLine(trimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (!IsIndentedBlockStart(trimmed, entry.Indent))
            {
                return BlankLineRuleResult.None;
            }

            if (LineClassifier.Instance.IsBlockStartLine(prevTrimmed) &&
                prevEntry.Indent >= entry.Indent)
            {
                return BlankLineRuleResult.None;
            }

            return BlankLineRuleResult.Decided;
        }

        private static bool IsIndentedBlockStart(string trimmed, int indent)
        {
            if (indent <= 0)
            {
                return false;
            }

            if (LineClassifier.Instance.IsTopLevelDefClass(trimmed))
            {
                return false;
            }

            return LineClassifier.Instance.IsBlockStartLine(trimmed);
        }
    }
}
