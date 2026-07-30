namespace CppFormatter
{
    /// <summary>
    /// Access-specifier rule: returns a blank line above an
    /// access specifier (<c>public:</c>, <c>protected:</c>,
    /// <c>private:</c>) when the previous non-blank line is not
    /// a block-opening brace and not another access specifier.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> (with a
        /// blank above) when the current line is an access specifier
        /// and the previous non-blank line is not a block-opening
        /// brace or another access specifier.
        /// </summary>
        internal BlankLineRuleResult ApplyAccessSpecifierBlankRule(
            string trimmed, string prevTrimmed)
        {
            if (!CppTextUtils.Instance.IsAccessSpecifier(trimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (prevTrimmed.Length == 0)
            {
                return BlankLineRuleResult.None;
            }

            if (LafnyaToolkit.Core.Text.TextUtils.EndsWithOpenBrace(prevTrimmed) ||
                CppTextUtils.Instance.IsAccessSpecifier(prevTrimmed))
            {
                return BlankLineRuleResult.None;
            }

            return BlankLineRuleResult.Decided;
        }
    }
}
