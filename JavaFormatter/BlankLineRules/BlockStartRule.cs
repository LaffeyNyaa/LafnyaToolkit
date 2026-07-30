namespace JavaFormatter
{
    /// <summary>
    /// Block-start rule: returns a blank line above a block-start
    /// keyword line when the previous non-blank line is not a
    /// block-opening brace and does not end with an open brace.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when the
        /// current line is a block-start line and the previous non-blank
        /// line is neither a lone open brace nor ends with an open
        /// brace.
        /// </summary>
        internal BlankLineRuleResult ApplyBlockStartRule(
            string trimmed,
            string prevTrimmed,
            bool isBlockStart,
            bool prevIsOpenBraceOnly,
            bool prevEndsWithOpenBrace)
        {
            if (isBlockStart && prevTrimmed.Length > 0 &&
                !prevIsOpenBraceOnly && !prevEndsWithOpenBrace)
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }
    }
}
