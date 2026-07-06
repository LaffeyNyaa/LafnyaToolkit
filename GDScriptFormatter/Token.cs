namespace GDScriptFormatter
{
    /// <summary>
    /// Represents the kinds of tokens that can be recognized in GDScript source code.
    /// </summary>
    internal enum TokenKind
    {
        /// <summary>Ordinary code (identifiers, keywords, operators, punctuation, annotations, node-path sigils, etc.).</summary>
        Code,

        /// <summary>Single-line string literal "..." or '...' (with escapes, terminated by a newline).</summary>
        String,

        /// <summary>Triple-quoted string literal """...""" or '''...''' (raw string, may span multiple lines).</summary>
        TripleString,

        /// <summary>Single-line comment #... to end of line (including ## doc comments).</summary>
        Comment
    }

    /// <summary>
    /// Represents a token and its original text.
    /// </summary>
    internal struct Token
    {
        /// <summary>The token kind.</summary>
        public TokenKind Kind;

        /// <summary>The token's original text (without any normalization).</summary>
        public string Text;
    }
}
