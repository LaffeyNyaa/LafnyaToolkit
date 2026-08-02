namespace JavaFormatter
{
    /// <summary>
    /// Plain-statement blank rule: returns a blank line above a plain
    /// single-line statement when the previous non-blank line is also a
    /// plain single-line statement and the input had a blank line
    /// between them.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when both
        /// the current and previous non-blank lines are plain
        /// single-line statements and the input had a blank line
        /// between them.
        /// </summary>
        internal BlankLineRuleResult ApplyPlainStatementBlankRule(
            string trimmed,
            string prevTrimmed,
            bool hadBlankAbove,
            bool lineStartsInCode,
            bool prevStartsInCode)
        {
            bool currentIsPlainStmt =
                BlankLineHelpers.IsPlainSingleLineStatement(trimmed,
                    lineStartsInCode);

            bool prevIsPlainStmt =
                BlankLineHelpers.IsPlainSingleLineStatement(prevTrimmed,
                    prevStartsInCode);

            if (currentIsPlainStmt && prevIsPlainStmt && hadBlankAbove)
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }
    }
}
