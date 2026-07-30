namespace CSharpFormatter
{
    /// <summary>
    /// If/else rule: suppresses any blank line above an <c>else</c>
    /// clause that follows a block-ending line, so that <c>else</c>
    /// sits directly adjacent to the preceding <c>if</c> block's
    /// closing brace.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineVerdict.SuppressBlank"/> when
        /// the current line starts with <c>else</c> (in a code
        /// region) and the previous non-blank line is a block-end.
        /// </summary>
        /// <param name="p">Pre-computed predicates for the current line.</param>
        /// <returns>The rule verdict.</returns>
        internal BlankLineVerdict ApplyIfElseRule(
            in BlankLinePredicates p)
        {
            if (p.CurrentIsElse && p.PrevIsBlockEnd)
            {
                return BlankLineVerdict.SuppressBlank;
            }

            return BlankLineVerdict.None;
        }
    }
}
