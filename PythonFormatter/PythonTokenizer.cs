using System;

using LafnyaToolkit.Core.Tokenization;

namespace PythonFormatter
{
    /// <summary>
    /// Tokenizes a Python source character stream into a token sequence,
    /// preserving the original text and trivia. Recognizes single-line
    /// comments, ordinary string literals (with optional prefix
    /// characters), triple-quoted strings (treated as multi-line verbatim
    /// strings), and bytes/f-string/r-string variants. All other
    /// characters are accumulated as ordinary Code tokens by the shared
    /// <see cref="TokenizerBase"/> pipeline.
    /// </summary>
    internal sealed class PythonTokenizer : TokenizerBase
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly PythonTokenizer Instance = new PythonTokenizer();
        private PythonTokenizer()
        {
        }

        /// <summary>
        /// Scans the source starting at <paramref name="position"/> and
        /// returns either a recognized non-code token (single-line
        /// comment, string literal, triple-quoted multi-line string) or
        /// zero to indicate ordinary code. The recognized token kinds
        /// are:
        /// <list type="bullet">
        ///   <item><description>single-line comment <c>#</c> up to end of line (not inside a string)</description></item>
        ///   <item><description>triple-quoted string with optional prefix (<c>"""</c> or <c>'''</c>) spanning multiple lines</description></item>
        ///   <item><description>ordinary string literal with optional prefix, using single or double quotes with backslash escape handling</description></item>
        /// </list>
        /// </summary>
        /// <param name="source">The full source text.</param>
        /// <param name="position">The character position to scan from.</param>
        /// <param name="token">When the return value is positive, the token to emit.</param>
        /// <returns>The number of characters consumed, or zero if this character is ordinary code.</returns>
        protected override int ScanNextToken(string source, int position,
            out Token token)
        {
            token = default(Token);
            int n = source.Length;
            char c = source[position];

            if (c == '#')
            {
                int start = position;

                while (position < n && source[position] != '\n')
                {
                    position++;
                }

                token = new Token(TokenKind.SingleLineComment,
                    source.Substring(start, position - start), start);

                return position - start;
            }

            if (c == '"' || c == '\'')
            {
                if (position + 2 < n && source[position + 1] == c &&
                    source[position + 2] == c)
                {
                    return ScanTripleQuoted(source, position, 0, out token);
                }

                return ScanSingleLineString(source, position, 0, out token);
            }

            int prefixLen = TryMatchStringPrefix(source, position, n);

            if (prefixLen > 0)
            {
                char quote = source[position + prefixLen];

                if (position + prefixLen + 2 < n &&
                    source[position + prefixLen + 1] == quote &&
                    source[position + prefixLen + 2] == quote)
                {
                    return ScanTripleQuoted(source, position, prefixLen,
                        out token);
                }

                return ScanSingleLineString(source, position, prefixLen,
                    out token);
            }

            return 0;
        }

        /// <summary>
        /// Determines whether the characters at <paramref name="position"/>
        /// form a Python string prefix. Recognized prefixes: <c>r</c>,
        /// <c>R</c>, <c>b</c>, <c>B</c>, <c>u</c>, <c>U</c>, <c>f</c>,
        /// <c>F</c>, and any two-character combination of these (e.g.
        /// <c>rb</c>, <c>br</c>, <c>fr</c>, <c>rf</c>, <c>bR</c>, etc.).
        /// </summary>
        /// <param name="source">The full source text.</param>
        /// <param name="position">The character position to inspect.</param>
        /// <param name="n">The total length of the source.</param>
        /// <returns>The number of characters consumed by the prefix
        /// (1 or 2), or 0 if no prefix is present.</returns>
        private static int TryMatchStringPrefix(string source, int position,
            int n)
        {
            if (position >= n)
            {
                return 0;
            }

            char c = source[position];

            if (c != 'r' && c != 'R' && c != 'b' && c != 'B' &&
                c != 'u' && c != 'U' && c != 'f' && c != 'F')
            {
                return 0;
            }

            if (position + 1 < n)
            {
                char nxt = source[position + 1];

                if (nxt == 'r' || nxt == 'R' || nxt == 'b' || nxt == 'B' ||
                    nxt == 'u' || nxt == 'U' || nxt == 'f' || nxt == 'F')
                {
                    if (position + 2 < n)
                    {
                        char after = source[position + 2];

                        if (after == '"' || after == '\'')
                        {
                            return 2;
                        }
                    }
                }
            }

            if (position + 1 < n)
            {
                char after = source[position + 1];

                if (after == '"' || after == '\'')
                {
                    return 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// Scans a triple-quoted string starting at <paramref name="position"/>
        /// (which has already been confirmed to be a prefix + triple-quote
        /// sequence). Honours Python's escape rules: a backslash followed
        /// by the same quote character is treated as a literal quote and
        /// does not end the string. The string may span multiple lines.
        /// </summary>
        /// <param name="source">The full source text.</param>
        /// <param name="position">The character position at which the prefix begins.</param>
        /// <param name="prefixLen">The number of characters in the prefix (1 or 2).</param>
        /// <param name="token">The produced token.</param>
        /// <returns>The total number of characters consumed by the prefix
        /// and the triple-quoted body.</returns>
        private static int ScanTripleQuoted(string source, int position,
            int prefixLen, out Token token)
        {
            int start = position;
            int n = source.Length;
            char quote = source[position + prefixLen];
            int i = position + prefixLen + 3;

            while (i < n)
            {
                if (source[i] == '\\' && i + 1 < n)
                {
                    i += 2;
                    continue;
                }

                if (source[i] == quote && i + 2 < n &&
                    source[i + 1] == quote && source[i + 2] == quote)
                {
                    i += 3;

                    token = new Token(TokenKind.VerbatimString,
                        source.Substring(start, i - start), start);

                    return i - start;
                }

                i++;
            }

            token = new Token(TokenKind.VerbatimString,
                source.Substring(start, n - start), start);

            return n - start;
        }

        /// <summary>
        /// Scans a single-line string literal starting at
        /// <paramref name="position"/> (which has already been confirmed to
        /// be a prefix + quote sequence). Honours Python's escape rules:
        /// a backslash followed by the same quote character is treated as
        /// a literal quote and does not end the string. The string ends at
        /// the matching quote or at the end of the line, whichever comes
        /// first (per Python's actual behaviour for unclosed strings on
        /// one line, the formatter preserves the rest of the line as code
        /// to avoid corrupting trailing text).
        /// </summary>
        /// <param name="source">The full source text.</param>
        /// <param name="position">The character position at which the prefix begins.</param>
        /// <param name="prefixLen">The number of characters in the prefix (1 or 2).</param>
        /// <param name="token">The produced token.</param>
        /// <returns>The total number of characters consumed by the prefix
        /// and the string body.</returns>
        private static int ScanSingleLineString(string source, int position,
            int prefixLen, out Token token)
        {
            int start = position;
            int n = source.Length;
            char quote = source[position + prefixLen];
            int i = position + prefixLen + 1;

            while (i < n && source[i] != '\n')
            {
                if (source[i] == '\\' && i + 1 < n)
                {
                    i += 2;
                    continue;
                }

                if (source[i] == quote)
                {
                    i++;

                    token = new Token(TokenKind.String,
                        source.Substring(start, i - start), start);

                    return i - start;
                }

                i++;
            }

            token = new Token(TokenKind.String,
                source.Substring(start, i - start), start);

            return i - start;
        }
    }
}
