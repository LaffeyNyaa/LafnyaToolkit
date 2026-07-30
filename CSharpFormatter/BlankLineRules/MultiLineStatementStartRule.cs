namespace CSharpFormatter
{
    /// <summary>
    /// Multi-line statement start rule: returns a blank line above
    /// the first segment of a multi-line statement when the previous
    /// non-blank line is not a block-opening brace or a comment
    /// (which is treated as attached to the following declaration).
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineVerdict.AddBlankAbove"/> when
        /// the current line is the start of a multi-line statement
        /// and the previous non-blank line is neither a block-opening
        /// brace nor a comment.
        /// </summary>
        /// <param name="p">Pre-computed predicates for the current line.</param>
        /// <returns>The rule verdict.</returns>
        internal BlankLineVerdict ApplyMultiLineStartRule(
            in BlankLinePredicates p)
        {
            if (p.CurrentIsMultiLineStart &&
                !p.PrevIsBlockStartBrace && !p.PrevIsComment)
            {
                return BlankLineVerdict.AddBlankAbove;
            }

            return BlankLineVerdict.None;
        }
    }
}
