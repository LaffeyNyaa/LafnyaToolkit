namespace LafnyaToolkit.Core.Tokenization
{
    /// <summary>
    /// The kinds of tokens the shared tokenizer base recognizes. Concrete
    /// language-specific tokenizers may map these to additional language
    /// concepts, but the core set covers code, ordinary strings, raw/verbatim
    /// strings, character literals, single-line and multi-line comments, and
    /// preprocessor directives.
    /// </summary>
    public enum TokenKind
    {
        /// <summary>Ordinary code (identifiers, keywords, operators, punctuation, etc.).</summary>
        Code,

        /// <summary>Ordinary string literal "..." and its prefixed variants (with escape sequences).</summary>
        String,

        /// <summary>Raw/verbatim string literal R"delim(...)delim" (escape sequences not processed).</summary>
        VerbatimString,

        /// <summary>Character literal '...' (with escape sequences).</summary>
        Char,

        /// <summary>Single-line comment //... to end of line.</summary>
        SingleLineComment,

        /// <summary>Multi-line comment /* ... */.</summary>
        MultiLineComment,

        /// <summary>Preprocessor directive #... entire line (including backslash continuation).</summary>
        Preprocessor,

        /// <summary>Interpolated string literal $"..." (with escapes and brace-depth tracking for interpolation expressions).</summary>
        InterpolatedString,

        /// <summary>Interpolated verbatim string literal $@"..." / @$"..." (with "" escapes and brace-depth tracking).</summary>
        InterpolatedVerbatimString
    }
}
