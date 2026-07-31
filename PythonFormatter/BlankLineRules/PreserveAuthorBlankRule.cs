namespace PythonFormatter
{
    /// <summary>
    /// Preserve-author-blank rule: when no other rule decided, an
    /// author-inserted blank line between two adjacent plain
    /// single-line statements at the same indent level is preserved.
    /// This rule never inserts a new blank line; it only preserves
    /// existing ones.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when both
        /// the current and previous non-blank lines are plain
        /// single-line statements and the input had a blank line
        /// between them.
        /// </summary>
        internal BlankLineRuleResult ApplyPreserveAuthorBlankRule(
            PythonNonBlankEntry entry, PythonNonBlankEntry prevEntry)
        {
            if (!entry.HadBlankAbove)
            {
                return BlankLineRuleResult.None;
            }

            if (entry.PrevIndent < 0)
            {
                return BlankLineRuleResult.None;
            }

            if (entry.Indent != entry.PrevIndent)
            {
                return BlankLineRuleResult.None;
            }

            string trimmed = entry.Line.TrimStart();
            string prevTrimmed = prevEntry.Line.TrimStart();

            if (LineClassifier.Instance.IsBlockStartLine(trimmed) ||
                LineClassifier.Instance.IsBlockStartLine(prevTrimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (LineClassifier.Instance.IsImportStatement(trimmed) ||
                LineClassifier.Instance.IsImportStatement(prevTrimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (BlankLineHelpers.IsCommentLine(trimmed) ||
                BlankLineHelpers.IsCommentLine(prevTrimmed))
            {
                return BlankLineRuleResult.None;
            }

            return BlankLineRuleResult.Decided;
        }
    }
}
