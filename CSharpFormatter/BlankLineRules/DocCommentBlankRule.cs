namespace CSharpFormatter
{
    /// <summary>
    /// Doc-comment blank rule: a <c>///</c> documentation-comment
    /// line should have a blank line above it when the previous
    /// non-blank line is a code statement. Exceptions (no blank
    /// added): the previous line is itself a <c>///</c> doc comment
    /// (multi-line doc continuation), a regular comment (<c>//</c>,
    /// <c>/*</c>, <c>*</c>), or a block-opening brace
    /// (<c>{</c> or ends with <c>{</c>).
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineVerdict.AddBlankAbove"/> when
        /// the current line starts a doc comment and the previous
        /// non-blank line is unrelated to the comment block.
        /// </summary>
        /// <param name="p">Pre-computed predicates for the current line.</param>
        /// <returns>The rule verdict.</returns>
        internal BlankLineVerdict ApplyDocCommentRule(
            in BlankLinePredicates p)
        {
            if (!p.CurrentIsDocComment)
            {
                return BlankLineVerdict.None;
            }

            if (p.PrevTrimmed.Length > 0 && !p.PrevIsDocComment &&
                !p.PrevIsRegularComment && !p.PrevIsBlockStartBrace)
            {
                return BlankLineVerdict.AddBlankAbove;
            }

            return BlankLineVerdict.None;
        }
    }
}
