namespace PythonFormatter
{
    /// <summary>
    /// Import-after-code blank rule: returns a blank line above an
    /// import statement when the previous non-blank line is a code
    /// statement at the same indent level (i.e. an import block
    /// appears in the middle of code, or follows the first non-import
    /// top-level statement).
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when the
        /// current line is an import and the previous non-blank line
        /// is not a comment and not an import at the same indent.
        /// </summary>
        internal BlankLineRuleResult ApplyImportAfterCodeBlankRule(
            PythonNonBlankEntry entry, PythonNonBlankEntry prevEntry)
        {
            string trimmed = entry.Line.TrimStart();
            string prevTrimmed = prevEntry.Line.TrimStart();

            if (!LineClassifier.Instance.IsImportStatement(trimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (LineClassifier.Instance.IsImportStatement(prevTrimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (BlankLineHelpers.IsCommentLine(prevTrimmed))
            {
                return BlankLineRuleResult.None;
            }

            return BlankLineRuleResult.Decided;
        }
    }
}
