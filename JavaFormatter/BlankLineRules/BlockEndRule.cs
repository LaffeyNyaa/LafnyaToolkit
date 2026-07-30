namespace JavaFormatter
{
    /// <summary>
    /// Block-end rule: returns a blank line above the first statement
    /// that follows a block-ending line (<c>}</c> or <c>};</c>) when
    /// the current line is itself not a close brace.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when the
        /// previous non-blank line is a block end and the current line
        /// is a new, non-closing-brace statement.
        /// </summary>
        internal BlankLineRuleResult ApplyBlockEndRule(
            string trimmed,
            string prevTrimmed,
            bool prevIsBlockEnd,
            bool currentStartsWithCloseBrace)
        {
            if (prevIsBlockEnd && trimmed.Length > 0 &&
                !currentStartsWithCloseBrace)
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }
    }
}
