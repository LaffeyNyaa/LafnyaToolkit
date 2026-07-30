namespace CSharpFormatter
{
    /// <summary>
    /// Using-directive rule: preserves an author-inserted blank line
    /// between two adjacent <c>using</c> directives. This is a
    /// preserve-only rule: it never adds a blank where the input did
    /// not have one.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineVerdict.AddBlankAbove"/> when
        /// both the current and previous lines are <c>using</c>
        /// directives AND the input had a blank line above the
        /// current line.
        /// </summary>
        /// <param name="p">Pre-computed predicates for the current line.</param>
        /// <returns>The rule verdict.</returns>
        internal BlankLineVerdict ApplyUsingDirectiveRule(
            in BlankLinePredicates p)
        {
            if (LineClassifier.Instance.IsUsingDirective(p.Trimmed) &&
                LineClassifier.Instance.IsUsingDirective(p.PrevTrimmed) &&
                p.EntryHadBlankAbove)
            {
                return BlankLineVerdict.AddBlankAbove;
            }

            return BlankLineVerdict.None;
        }
    }
}
