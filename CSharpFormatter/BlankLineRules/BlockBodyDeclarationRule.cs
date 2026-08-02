namespace CSharpFormatter
{
    /// <summary>
    /// Block-body declaration rule: returns a blank line above a
    /// method, constructor, property, indexer, or other declaration
    /// whose body is a block, when it directly follows a statement.
    /// Skipped when the previous line is a block-opening brace (the
    /// declaration is the first member of its parent block) or a
    /// comment attached to the declaration.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineVerdict.AddBlankAbove"/> when
        /// the current line begins a block-bodied declaration and the
        /// previous non-blank line is neither a block-opening brace
        /// nor a comment.
        /// </summary>
        /// <param name="p">Pre-computed predicates for the current line.</param>
        /// <returns>The rule verdict.</returns>
        internal BlankLineVerdict ApplyBlockBodyDeclarationRule(
            in BlankLinePredicates p)
        {
            if (p.CurrentIsBlockBodyDeclaration &&
                p.PrevTrimmed.Length > 0 &&
                !p.PrevIsBlockStartBrace &&
                !p.PrevIsComment)
            {
                return BlankLineVerdict.AddBlankAbove;
            }

            return BlankLineVerdict.None;
        }
    }
}
