namespace CSharpFormatter
{
    /// <summary>
    /// Try/catch/finally rule: suppresses any blank line above a
    /// <c>catch</c> or <c>finally</c> clause that follows a
    /// block-ending line, so that try/catch/finally clauses sit
    /// directly adjacent to the preceding block's closing brace.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineVerdict.SuppressBlank"/> when
        /// the current line starts with <c>catch</c> or
        /// <c>finally</c> (in a code region) and the previous
        /// non-blank line is a block-end.
        /// </summary>
        /// <param name="p">Pre-computed predicates for the current line.</param>
        /// <returns>The rule verdict.</returns>
        internal BlankLineVerdict ApplyCatchFinallyRule(
            in BlankLinePredicates p)
        {
            if (p.CurrentIsCatchOrFinally && p.PrevIsBlockEnd)
            {
                return BlankLineVerdict.SuppressBlank;
            }

            return BlankLineVerdict.None;
        }
    }
}
