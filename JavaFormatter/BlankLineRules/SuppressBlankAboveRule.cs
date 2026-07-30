namespace JavaFormatter
{
    /// <summary>
    /// Suppress-blank-above rule: cancels a previously decided
    /// "want blank above" decision when the current line is an
    /// annotation, do-while tail, or block-continuation keyword.
    /// Called only after another rule has already decided to add a
    /// blank line above; this rule exists to opt-out of that decision
    /// for these specific line kinds.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when the
        /// current line is an annotation (<c>@...</c>), a do-while
        /// tail, or a block-continuation keyword (catch/finally/else);
        /// the dispatcher uses this to clear the previously-decided
        /// "want blank above" flag.
        /// </summary>
        internal BlankLineRuleResult ApplySuppressBlankAboveRule(
            bool currentIsAnnotation,
            bool currentIsDoWhileTail,
            bool currentIsBlockCont)
        {
            if (currentIsAnnotation || currentIsDoWhileTail ||
                currentIsBlockCont)
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }
    }
}
