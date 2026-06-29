namespace CppFormatter
{
    /// <summary>
    /// Represents the types of tokens recognizable in C++ source code.
    /// </summary>
    internal enum TokenKind
    {
        /// <summary>Ordinary code (identifiers, keywords, operators, punctuation, etc.).</summary>
        Code,

        /// <summary>Ordinary string literal "..." and its prefixed variants L"..."/u8"..."/u"..."/U"..." (with escape sequences).</summary>
        String,

        /// <summary>Raw string literal R"delim(...)delim" and its prefixed variants LR"..."/u8R"..."/uR"..."/UR"..." (escape sequences not processed).</summary>
        VerbatimString,

        /// <summary>Character literal '...' (with escape sequences).</summary>
        Char,

        /// <summary>Single-line comment //... to end of line.</summary>
        SingleLineComment,

        /// <summary>Multi-line comment /* ... */.</summary>
        MultiLineComment,

        /// <summary>Preprocessor directive #... entire line (including backslash continuation).</summary>
        Preprocessor
    }
}
