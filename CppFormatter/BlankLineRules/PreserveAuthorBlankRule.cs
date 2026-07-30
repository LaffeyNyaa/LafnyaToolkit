using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Preserve-author rule: preserves an existing blank line
    /// between two adjacent single-line statements when the author
    /// had inserted one. Only PRESERVES an existing blank
    /// (HadBlankAbove); never adds one.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> (with a
        /// blank above) when both the current and previous lines are
        /// plain single-line statements and a blank line existed
        /// above the current line in the original input.
        /// </summary>
        internal BlankLineRuleResult ApplyPreserveAuthorBlankRule(
            CppNonBlankEntry entry,
            List<CppNonBlankEntry> nonBlank,
            int i,
            string trimmed,
            string prevTrimmed)
        {
            if (!entry.HadBlankAbove || i <= 0)
            {
                return BlankLineRuleResult.None;
            }

            if (!BlankLineHelpers.IsPlainSingleLineStatement(trimmed, entry.IsProtected))
            {
                return BlankLineRuleResult.None;
            }

            if (!BlankLineHelpers.IsPlainSingleLineStatement(prevTrimmed, nonBlank[i - 1].IsProtected))
            {
                return BlankLineRuleResult.None;
            }

            return BlankLineRuleResult.Decided;
        }
    }
}
