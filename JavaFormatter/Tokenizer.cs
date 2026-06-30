using System.Collections.Generic;
using System.Text;

namespace JavaFormatter
{
    /// <summary>
    /// Represents the kinds of tokens recognizable in Java source code.
    /// </summary>
    internal enum TokenKind
    {
        /// <summary>Plain code (identifiers, keywords, operators, punctuation, etc.).</summary>
        Code,

        /// <summary>Regular string literal "..." (with backslash escapes).</summary>
        String,

        /// <summary>Text block literal """...""" (Java 13+, with incidental whitespace removal).</summary>
        TextBlock,

        /// <summary>Character constant '...' (with backslash escapes).</summary>
        Char,

        /// <summary>Single-line comment //... up to end of line.</summary>
        SingleLineComment,

        /// <summary>Multi-line comment /* ... */ (supports nested comments).</summary>
        MultiLineComment
    }

    /// <summary>
    /// Represents a token and its original text.
    /// </summary>
    internal struct Token
    {
        /// <summary>The token kind.</summary>
        public TokenKind Kind;

        /// <summary>The original token text (without any normalization).</summary>
        public string Text;
    }

    /// <summary>
    /// Tokenizes a Java source character stream into a token sequence,
    /// preserving original text and trivia.
    /// </summary>
    internal static class Tokenizer
    {
        /// <summary>
        /// Tokenizes the source code and returns the token list.
        /// </summary>
        /// <param name="source">The raw source code string.</param>
        /// <returns>The token list, in order of appearance.</returns>
        public static List<Token> Tokenize(string source)
        {
            var tokens = new List<Token>();
            var code = new StringBuilder();
            int i = 0;
            int n = source.Length;

            while (i < n)
            {
                char c = source[i];

                if (c == '/' && i + 1 < n && source[i + 1] == '/')
                {
                    FlushCode(tokens, code);
                    int start = i;

                    while (i < n && source[i] != '\n')
                    {
                        i++;
                    }

                    tokens.Add(new Token { Kind = TokenKind.SingleLineComment,
                        Text = source.Substring(start, i - start) });

                    continue;
                }

                if (c == '/' && i + 1 < n && source[i + 1] == '*')
                {
                    FlushCode(tokens, code);
                    int start = i;
                    i += 2;
                    int depth = 1;

                    while (i < n && depth > 0)
                    {
                        if (source[i] == '/' && i + 1 < n && source[i + 1] ==
                            '*')
                        {
                            depth++;
                            i += 2;
                            continue;
                        }

                        if (source[i] == '*' && i + 1 < n && source[i + 1] ==
                            '/')
                        {
                            depth--;
                            i += 2;
                            continue;
                        }

                        i++;
                    }

                    tokens.Add(new Token { Kind = TokenKind.MultiLineComment,
                        Text = source.Substring(start, i - start) });

                    continue;
                }

                if (c == '"' && i + 2 < n && source[i + 1] == '"' && source[i +
                    2] == '"')
                {
                    FlushCode(tokens, code);
                    int start = i;
                    i += 3;

                    while (i < n)
                    {
                        if (source[i] == '"')
                        {
                            if (i + 2 < n && source[i + 1] == '"' && source[i +
                                2] == '"')
                            {
                                i += 3;
                                break;
                            }

                            i++;
                            continue;
                        }

                        if (source[i] == '\\')
                        {
                            if (i + 1 < n && source[i + 1] == '\n')
                            {
                                i += 2;
                                continue;
                            }

                            if (i + 2 < n && source[i + 1] == '\r' && source[i +
                                2] == '\n')
                            {
                                i += 3;
                                continue;
                            }

                            i += 2;
                            continue;
                        }

                        i++;
                    }

                    tokens.Add(new Token { Kind = TokenKind.TextBlock,
                        Text = source.Substring(start, i - start) });

                    continue;
                }

                if (c == '"')
                {
                    FlushCode(tokens, code);
                    int start = i;
                    i++;

                    while (i < n)
                    {
                        if (source[i] == '\\')
                        {
                            if (i + 1 < n)
                            {
                                i += 2;
                            }
                            else
                            {
                                i++;
                            }

                            continue;
                        }

                        if (source[i] == '"')
                        {
                            i++;
                            break;
                        }

                        i++;
                    }

                    tokens.Add(new Token { Kind = TokenKind.String,
                        Text = source.Substring(start, i - start) });

                    continue;
                }

                if (c == '\'')
                {
                    FlushCode(tokens, code);
                    int start = i;
                    i++;

                    while (i < n)
                    {
                        if (source[i] == '\\')
                        {
                            if (i + 1 < n)
                            {
                                i += 2;
                            }
                            else
                            {
                                i++;
                            }

                            continue;
                        }

                        if (source[i] == '\'')
                        {
                            i++;
                            break;
                        }

                        i++;
                    }

                    tokens.Add(new Token { Kind = TokenKind.Char,
                        Text = source.Substring(start, i - start) });

                    continue;
                }

                code.Append(c);
                i++;
            }

            FlushCode(tokens, code);
            return tokens;
        }

        /// <summary>
        /// Reconstructs the source string from a token list (should match the original text).
        /// </summary>
        /// <param name="tokens">The token list.</param>
        /// <returns>The concatenated string.</returns>
        public static string Reconstruct(List<Token> tokens)
        {
            var sb = new StringBuilder();

            foreach (var t in tokens)
            {
                sb.Append(t.Text);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds a boolean mask indicating whether each character position belongs to a Code token.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="tokens">The token list (should be the tokenization of text).</param>
        /// <returns>A boolean array where true indicates the position is a Code token character.</returns>
        public static bool[] BuildCodeMask(string text, List<Token> tokens)
        {
            var mask = new bool[text.Length];
            int pos = 0;

            foreach (var t in tokens)
            {
                for (int j = 0; j < t.Text.Length; j++)
                {
                    if (pos + j < mask.Length)
                    {
                        mask[pos + j] = t.Kind == TokenKind.Code;
                    }
                }

                pos += t.Text.Length;
            }

            return mask;
        }

        private static void FlushCode(List<Token> tokens, StringBuilder code)
        {
            if (code.Length > 0)
            {
                tokens.Add(new Token { Kind = TokenKind.Code,
                    Text = code.ToString() });

                code.Clear();
            }
        }
    }
}
