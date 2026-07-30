namespace JavaFormatter
{
    /// <summary>
    /// Consecutive-imports rule: preserves an existing blank line
    /// between two <c>import</c> directives when the author had
    /// inserted one.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when both
        /// the current and previous non-blank lines are import
        /// directives and the input had a blank line between them.
        /// </summary>
        internal BlankLineRuleResult ApplyConsecutiveImportsRule(
            bool currentIsImport,
            bool prevIsImport,
            bool hadBlankAbove)
        {
            if (currentIsImport && prevIsImport && hadBlankAbove)
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }
    }
}
