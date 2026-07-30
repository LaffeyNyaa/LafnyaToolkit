namespace CppFormatter
{
    /// <summary>
    /// Consecutive-namespace rule: when the current and previous
    /// lines are both namespace declarations, do NOT insert a blank
    /// line between them. Highest-priority rule in the chain so that
    /// any later rule that would otherwise fire on namespaces is
    /// overridden.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> (with
        /// no blank above) when both <paramref name="trimmed"/> and
        /// <paramref name="prevTrimmed"/> start with the
        /// <c>namespace</c> keyword.
        /// </summary>
        internal BlankLineRuleResult ApplyConsecutiveNamespacesRule(
            string trimmed, string prevTrimmed)
        {
            bool curIsNamespace = LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(trimmed, "namespace");
            bool prevIsNamespace = LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(prevTrimmed, "namespace");

            if (curIsNamespace && prevIsNamespace)
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }
    }
}
