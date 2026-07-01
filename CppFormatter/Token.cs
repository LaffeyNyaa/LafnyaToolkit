namespace CppFormatter
{
    /// <summary>
    /// Represents a token and its original text.
    /// </summary>
    internal readonly struct Token
    {
        /// <summary>The token kind.</summary>
        public TokenKind Kind { get; }

        /// <summary>The original text of the token (not normalized in any way).</summary>
        public string Text { get; }

        /// <summary>
        /// Creates a new token with the given kind and text.
        /// </summary>
        /// <param name="kind">The token kind.</param>
        /// <param name="text">The original text of the token.</param>
        public Token(TokenKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }
    }
}
