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
        /// Lines that start with <c>||</c> or <c>&amp;&amp;</c> are
        /// excluded: they are continuations of a preceding logical
        /// expression (placed at the start of a continuation line by
        /// the line-length splitter), not the start of a new
        /// multi-line statement.
        /// </summary>
        /// <param name="p">Pre-computed predicates for the current line.</param>
        /// <returns>The rule verdict.</returns>
        internal BlankLineVerdict ApplyMultiLineStartRule(
            in BlankLinePredicates p)
        {
            if (p.CurrentIsMultiLineStart &&
                !p.PrevIsBlockStartBrace && !p.PrevIsComment)
            {
                // Lines starting with || or && are continuations of
                // a logical expression from the previous line, not
                // the start of a new multi-line statement.
                string trimmed = p.Trimmed;

                if (trimmed.StartsWith("||") || trimmed.StartsWith("&&"))
                {
                    return BlankLineVerdict.None;
                }

                return BlankLineVerdict.AddBlankAbove;
            }

            return BlankLineVerdict.None;
        }
    }
}
