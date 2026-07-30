namespace JavaFormatter
{
    /// <summary>
    /// Doc-comment rule: returns a blank line above a doc-comment
    /// start line (<c>/**</c>) when the previous non-blank line is not
    /// itself a doc comment, regular comment, or block-opening brace.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when the
        /// current line starts a doc comment and the previous non-blank
        /// line is unrelated to the comment block.
        /// </summary>
        internal BlankLineRuleResult ApplyDocCommentRule(
            string trimmed,
            string prevTrimmed)
        {
            if (!trimmed.StartsWith("/**"))
            {
                return BlankLineRuleResult.None;
            }

            bool prevIsRegularComment =
                prevTrimmed.StartsWith("//") ||
                (prevTrimmed.StartsWith("/*") &&
                !prevTrimmed.StartsWith("/**")) ||
                (prevTrimmed.StartsWith("*") &&
                !prevTrimmed.EndsWith("*/"));

            bool prevIsBlockOpenBrace =
                prevTrimmed == "{" ||
                LafnyaToolkit.Core.Text.TextUtils.EndsWithOpenBrace(prevTrimmed);

            if (prevTrimmed.Length > 0 &&
                !prevIsRegularComment &&
                !prevIsBlockOpenBrace)
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }
    }
}
