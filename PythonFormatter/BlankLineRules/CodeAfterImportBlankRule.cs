namespace PythonFormatter
{
    /// <summary>
    /// Code-after-import blank rule: returns a blank line above the
    /// first code statement that follows a contiguous block of
    /// imports at the same indent level. Ensures the import block is
    /// visually separated from the rest of the file.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when the
        /// previous non-blank line is an import and the current line
        /// is not an import and is not a comment.
        /// </summary>
        internal BlankLineRuleResult ApplyCodeAfterImportBlankRule(
            PythonNonBlankEntry entry, PythonNonBlankEntry prevEntry)
        {
            string trimmed = entry.Line.TrimStart();
            string prevTrimmed = prevEntry.Line.TrimStart();

            if (LineClassifier.Instance.IsImportStatement(trimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (!LineClassifier.Instance.IsImportStatement(prevTrimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (BlankLineHelpers.IsCommentLine(trimmed))
            {
                return BlankLineRuleResult.None;
            }

            return BlankLineRuleResult.Decided;
        }
    }
}
