using LafnyaToolkit.Core.Tokenization;

namespace JavaFormatter
{
    /// <summary>
    /// Tokenizes a Java source character stream into a token sequence,
    /// preserving the original text and trivia. Recognizes single-line
    /// comments, nested multi-line comments, regular string literals,
    /// character literals, and text-block literals (Java 13+). All other
    /// characters are accumulated as ordinary code tokens by the shared
    /// <see cref="TokenizerBase"/> pipeline.
    /// </summary>
    internal sealed class JavaTokenizer : TokenizerBase
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly JavaTokenizer Instance = new JavaTokenizer();
        private JavaTokenizer()
        {
        }

        /// <summary>
        /// Scans the source starting at <paramref name="position"/> and
        /// returns either a recognized non-code token (single-line
        /// comment, nested multi-line comment, string, char, or text
        /// block) or zero to indicate ordinary code. The recognized
        /// token kinds are:
        /// <list type="bullet">
        ///   <item><description>single-line comment <c>//</c> up to end of line</description></item>
        ///   <item><description>nested multi-line comment (slash-star ... star-slash) with arbitrary depth</description></item>
        ///   <item><description>text block (triple-quoted string) with backslash escape handling</description></item>
        ///   <item><description>regular string literal <c>"..."</c> with backslash escape handling</description></item>
        ///   <item><description>character literal <c>'...'</c> with backslash escape handling</description></item>
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

            if (c == '/' && position + 1 < n && source[position + 1] == '/')
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

            if (c == '/' && position + 1 < n && source[position + 1] == '*')
            {
                int start = position;
                position += 2;
                int depth = 1;

                while (position < n && depth > 0)
                {
                    if (source[position] == '/' && position + 1 < n &&
                        source[position + 1] == '*')
                    {
                        depth++;
                        position += 2;
                        continue;
                    }

                    if (source[position] == '*' && position + 1 < n &&
                        source[position + 1] == '/')
                    {
                        depth--;
                        position += 2;
                        continue;
                    }

                    position++;
                }

                token = new Token(TokenKind.MultiLineComment,
                    source.Substring(start, position - start), start);

                return position - start;
            }

            if (c == '"' && position + 2 < n && source[position + 1] == '"' &&
                source[position + 2] == '"')
            {
                int start = position;
                position += 3;

                while (position < n)
                {
                    if (source[position] == '"')
                    {
                        if (position + 2 < n && source[position + 1] == '"' &&
                            source[position + 2] == '"')
                        {
                            position += 3;
                            break;
                        }

                        position++;
                        continue;
                    }

                    if (source[position] == '\\')
                    {
                        if (position + 1 < n && source[position + 1] == '\n')
                        {
                            position += 2;
                            continue;
                        }

                        if (position + 2 < n && source[position + 1] == '\r' &&
                            source[position + 2] == '\n')
                        {
                            position += 3;
                            continue;
                        }

                        position += 2;
                        continue;
                    }

                    position++;
                }

                token = new Token(TokenKind.VerbatimString,
                    source.Substring(start, position - start), start);

                return position - start;
            }

            if (c == '"')
            {
                int start = position;
                position++;

                while (position < n)
                {
                    if (source[position] == '\\')
                    {
                        if (position + 1 < n)
                        {
                            position += 2;
                        }
                        else
                        {
                            position++;
                        }

                        continue;
                    }

                    if (source[position] == '"')
                    {
                        position++;
                        break;
                    }

                    position++;
                }

                token = new Token(TokenKind.String, source.Substring(start,
                    position - start), start);

                return position - start;
            }

            if (c == '\'')
            {
                int start = position;
                position++;

                while (position < n)
                {
                    if (source[position] == '\\')
                    {
                        if (position + 1 < n)
                        {
                            position += 2;
                        }
                        else
                        {
                            position++;
                        }

                        continue;
                    }

                    if (source[position] == '\'')
                    {
                        position++;
                        break;
                    }

                    position++;
                }

                token = new Token(TokenKind.Char, source.Substring(start,
                    position - start), start);

                return position - start;
            }

            return 0;
        }
    }
}
