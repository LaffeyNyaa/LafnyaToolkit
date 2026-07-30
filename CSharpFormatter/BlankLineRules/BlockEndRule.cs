namespace CSharpFormatter
{
    /// <summary>
    /// Block-end rule: returns a blank line above the first
    /// statement that follows a block-ending line (<c>}</c> or
    /// <c>};</c>). Skipped when the current line is itself a
    /// block-end (chained <c>}</c>).
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineVerdict.AddBlankAbove"/> when
        /// the previous non-blank line ends a block and the current
        /// line is a new, non-block-end statement.
        /// </summary>
        /// <param name="p">Pre-computed predicates for the current line.</param>
        /// <returns>The rule verdict.</returns>
        internal BlankLineVerdict ApplyBlockEndRule(
            in BlankLinePredicates p)
        {
            if (p.PrevIsBlockEnd &&
                p.Trimmed.Length > 0 && p.Trimmed != "}" &&
                !p.Trimmed.StartsWith("}"))
            {
                return BlankLineVerdict.AddBlankAbove;
            }

            return BlankLineVerdict.None;
        }
    }
}
