namespace LafnyaToolkit.Core.Tokenization
{
    /// <summary>
    /// A text replacement range: the half-open range [Start, End) in the
    /// source text is replaced by <see cref="NewText"/>. Used to record
    /// edits that a processor wants applied, applied later in start-position
    /// order by <see cref="LafnyaToolkit.Core.Text.TextUtils.ApplyReplacements"/>.
    /// </summary>
    public struct Replacement
    {
        /// <summary>The start position (inclusive).</summary>
        public int Start;

        /// <summary>The end position (exclusive).</summary>
        public int End;

        /// <summary>The replacement text.</summary>
        public string NewText;

        /// <summary>
        /// Creates a new replacement record.
        /// </summary>
        /// <param name="start">The start position.</param>
        /// <param name="end">The end position.</param>
        /// <param name="newText">The replacement text.</param>
        public Replacement(
            int start,
            int end,
            string newText
        )
        {
            Start = start;
            End = end;
            NewText = newText;
        }
    }
}
