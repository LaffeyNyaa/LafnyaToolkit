namespace LafnyaToolkit.Core.Tokenization
{
    /// <summary>
    /// A pure-insertion record: at character position <see cref="Position"/>,
    /// insert <see cref="Text"/> without removing any existing characters.
    /// Used by brace enforcement and similar processors to add braces without
    /// disturbing surrounding text.
    /// </summary>
    public struct Insertion
    {
        /// <summary>The character position at which to insert.</summary>
        public int Position;

        /// <summary>The text to insert.</summary>
        public string Text;

        /// <summary>
        /// Creates a new insertion record.
        /// </summary>
        /// <param name="position">The character position.</param>
        /// <param name="text">The text to insert.</param>
        public Insertion(int position, string text)
        {
            Position = position;
            Text = text;
        }
    }
}
