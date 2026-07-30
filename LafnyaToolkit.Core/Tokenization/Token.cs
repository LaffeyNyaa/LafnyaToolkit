namespace LafnyaToolkit.Core.Tokenization
{
    /// <summary>
    /// A single token produced by the tokenizer: its kind, its original text,
    /// and the character position at which it begins in the source.
    /// </summary>
    public readonly struct Token
    {
        /// <summary>The token kind.</summary>
        public TokenKind Kind { get; }

        /// <summary>The original text of the token (not normalized in any way).</summary>
        public string Text { get; }

        /// <summary>The zero-based character position where the token starts in the source.</summary>
        public int Start { get; }

        /// <summary>
        /// Creates a new token with the given kind, text, and start position.
        /// </summary>
        /// <param name="kind">The token kind.</param>
        /// <param name="text">The original text of the token.</param>
        /// <param name="start">The starting character position in the source.</param>
        public Token(TokenKind kind, string text, int start)
        {
            Kind = kind;
            Text = text;
            Start = start;
        }

        /// <summary>
        /// Creates a new token with the given kind and text; start defaults to zero.
        /// </summary>
        /// <param name="kind">The token kind.</param>
        /// <param name="text">The original text of the token.</param>
        public Token(TokenKind kind, string text)
        {
            Kind = kind;
            Text = text;
            Start = 0;
        }
    }
}
