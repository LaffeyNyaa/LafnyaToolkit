using System.Collections.Generic;
using System.Text;

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

    /// <summary>
    /// Splits a GDScript source character stream into a token sequence, preserving original text and trivia.
    /// </summary>
    internal static class Tokenizer
    {
        /// <summary>
        /// Tokenizes the source code and returns a token list.
        /// </summary>
        /// <param name="source">The original source string.</param>
        /// <returns>A token list in order of appearance.</returns>
        public static List<Token> Tokenize(string source)
        {
            var tokens = new List<Token>();
            var code = new StringBuilder();
            int i = 0;
            int n = source.Length;

            while (i < n)
            {
                char c = source[i];

                if ((c == '"' || c == '\'') && i + 2 < n &&
                    source[i + 1] == c && source[i + 2] == c)
                {
                    FlushCode(tokens, code);
                    int start = i;
                    char quote = c;
                    i += 3;

                    while (i < n)
                    {
                        if (source[i] == quote && i + 2 < n &&
                            source[i + 1] == quote && source[i + 2] == quote)
                        {
                            i += 3;
                            break;
                        }

                        i++;
                    }

                    tokens.Add(new Token { Kind = TokenKind.TripleString,
                        Text = source.Substring(start, i - start) });
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    FlushCode(tokens, code);
                    int start = i;
                    char quote = c;
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

                        if (source[i] == '\n')
                        {
                            break;
                        }

                        if (source[i] == quote)
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

                if (c == '#')
                {
                    FlushCode(tokens, code);
                    int start = i;

                    while (i < n && source[i] != '\n')
                    {
                        i++;
                    }

                    tokens.Add(new Token { Kind = TokenKind.Comment,
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
        /// Reconstructs the token list back into a string (should match the original text).
        /// </summary>
        /// <param name="tokens">The token list.</param>
        /// <returns>The reconstructed string.</returns>
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
        /// Builds a boolean array indicating whether each character position belongs to a Code token, given the text and token list.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="tokens">The token list (should be the tokenization result of text).</param>
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

        /// <summary>
        /// Flushes accumulated Code characters into a Code token (if non-empty).
        /// </summary>
        /// <param name="tokens">The token list.</param>
        /// <param name="code">The StringBuilder accumulating ordinary code.</param>
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
