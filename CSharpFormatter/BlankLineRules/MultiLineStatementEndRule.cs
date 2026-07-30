namespace CSharpFormatter
{
    /// <summary>
    /// Multi-line statement end rule: returns a blank line above a
    /// line that follows the end of a multi-line statement. The
    /// block-tail exception: do not add a blank if the current line
    /// is itself a block-end (<c>}</c> or <c>};</c>).
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineVerdict.AddBlankAbove"/> when
        /// the previous non-blank line ended a multi-line statement
        /// and the current line is not a block-end.
        /// </summary>
        /// <param name="p">Pre-computed predicates for the current line.</param>
        /// <returns>The rule verdict.</returns>
        internal BlankLineVerdict ApplyMultiLineEndRule(
            in BlankLinePredicates p)
        {
            if (p.PrevIsMultiLineEnd && !p.CurrentIsBlockEnd)
            {
                return BlankLineVerdict.AddBlankAbove;
            }

            return BlankLineVerdict.None;
        }
    }
}
