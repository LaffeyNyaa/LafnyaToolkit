namespace CSharpFormatter
{
    /// <summary>
    /// Author-blank-preservation rule: preserves an author-inserted
    /// blank line between two adjacent single-line statements.
    /// Preserve-only: this rule never adds a blank where the input
    /// did not have one. Both the current and the previous
    /// non-blank line must be plain single-line statements. This
    /// rule is idempotent: on a second pass the preserved blank is
    /// still present.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineVerdict.AddBlankAbove"/> when
        /// both the current and previous lines are plain
        /// single-line statements AND the input had a blank line
        /// above the current line.
        /// </summary>
        /// <param name="p">Pre-computed predicates for the current line.</param>
        /// <returns>The rule verdict.</returns>
        internal BlankLineVerdict ApplyAuthorBlankRule(
            in BlankLinePredicates p)
        {
            if (p.EntryHadBlankAbove &&
                p.CurrentIsPlainStmt && p.PrevIsPlainStmt)
            {
                return BlankLineVerdict.AddBlankAbove;
            }

            return BlankLineVerdict.None;
        }
    }
}
