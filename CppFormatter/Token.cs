namespace CppFormatter
{
    /// <summary>
    /// Represents a token and its original text.
    /// </summary>
    internal struct Token
    {
        /// <summary>The token kind.</summary>
        public TokenKind Kind;
        /// <summary>The original text of the token (not normalized in any way).</summary>
        public string Text;
    }
}
