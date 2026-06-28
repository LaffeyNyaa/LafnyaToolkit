using System.Collections.Generic;
using System.Text;

namespace CSharpFormatter
{
    /// <summary>
    /// Represents the kinds of tokens that can be recognized in C# source code.
    /// </summary>
    internal enum TokenKind
    {
        /// <summary>Ordinary code (identifiers, keywords, operators,
        /// punctuation, etc.).</summary>
        Code,
        /// <summary>Regular string literal "..." (with escapes).</summary>
        String,
        /// <summary>Verbatim string literal @"..." ("" represents an embedded
        /// quote).</summary>
        VerbatimString,
        /// <summary>Character literal '...' (with escapes).</summary>
        Char,
        /// <summary>Single-line comment //... to end of line.</summary>
        SingleLineComment,
        /// <summary>Multi-line comment /* ... */.</summary>
        MultiLineComment,
        /// <summary>Preprocessor directive #... covering the entire line
        /// (including backslash line continuations).</summary>
        Preprocessor
    }

    /// <summary>
    /// Represents a token together with its original text.
    /// </summary>
    internal struct Token
    {
        /// <summary>The token kind.</summary>
        public TokenKind Kind;
        /// <summary>The original text of the token (without any
        /// normalization).</summary>
        public string Text;
    }

    /// <summary>
    /// Tokenizes a C# source character stream into a token sequence,
    /// preserving the original text and trivia.
    /// </summary>
    internal static class Tokenizer
    {
        /// <summary>
        /// Tokenizes the source code and returns a token list.
        /// </summary>
        /// <param name="source">The original source code string.</param>
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

                    while (i < n)
                    {
                        if (source[i] == '*' && i + 1 < n && source[i + 1] ==
                            '/')
                        {
                            i += 2;
                            break;
                        }

                        i++;
                    }

                    tokens.Add(new Token { Kind = TokenKind.MultiLineComment,
                        Text = source.Substring(start, i - start) });
                    continue;
                }

                if (c == '@' && i + 1 < n && source[i + 1] == '"')
                {
                    FlushCode(tokens, code);
                    int start = i;
                    i += 2;

                    while (i < n)
                    {
                        if (source[i] == '"')
                        {
                            if (i + 1 < n && source[i + 1] == '"')
                            {
                                i += 2;
                                continue;
                            }

                            i++;
                            break;
                        }

                        i++;
                    }

                    tokens.Add(new Token { Kind = TokenKind.VerbatimString,
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

                if (c == '#' && IsLineStart(source, i))
                {
                    FlushCode(tokens, code);
                    int start = i;

                    while (i < n)
                    {
                        if (source[i] == '\\' && i + 1 < n && source[i + 1] ==
                            '\n')
                        {
                            i += 2;
                            continue;
                        }

                        if (source[i] == '\\' && i + 2 < n && source[i + 1] ==
                            '\r' && source[i + 2] == '\n')
                        {
                            i += 3;
                            continue;
                        }

                        if (source[i] == '\n')
                        {
                            break;
                        }

                        i++;
                    }

                    tokens.Add(new Token { Kind = TokenKind.Preprocessor,
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
        /// Reconstructs the original text by concatenating the token list
        /// (should match the original text).
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
        /// Builds a boolean array marking, for each character position,
        /// whether it belongs to a Code token, given the text and token list.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="tokens">The token list (should be the tokenization
        /// result of <paramref name="text"/>).</param>
        /// <returns>A boolean array; true means the position is a Code token
        /// character.</returns>
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

        private static bool IsLineStart(string source, int index)
        {
            int j = index - 1;

            while (j >= 0)
            {
                char ch = source[j];

                if (ch == '\n')
                {
                    return true;
                }

                if (ch != ' ' && ch != '\t' && ch != '\r')
                {
                    return false;
                }

                j--;
            }

            return true;
        }
    }
}
