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
        /// Tokenizes the source code and returns a token list. Recognizes GDScript 2.x prefixed
        /// string literals: raw strings (r/R), StringName (&amp;), and NodePath (^). The prefix
        /// character is emitted as a separate Code token, and the following string literal is
        /// scanned with the standard string logic.
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

                if (IsTripleQuoteOpen(source, i, n))
                {
                    FlushCode(tokens, code);
                    i = ScanTripleString(source, i, n, c, tokens);
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    FlushCode(tokens, code);
                    i = ScanString(source, i, n, c, tokens);
                    continue;
                }

                if (c == '#')
                {
                    FlushCode(tokens, code);
                    i = ScanComment(source, i, n, tokens);
                    continue;
                }

                if (IsRawStringPrefix(source, i, n, code))
                {
                    FlushCode(tokens, code);
                    tokens.Add(new Token { Kind = TokenKind.Code,
                        Text = c.ToString() });
                    i++;
                    continue;
                }

                if (IsStringSigilPrefix(source, i, n, c))
                {
                    FlushCode(tokens, code);
                    tokens.Add(new Token { Kind = TokenKind.Code,
                        Text = c.ToString() });
                    i++;
                    continue;
                }

                code.Append(c);
                i++;
            }

            FlushCode(tokens, code);
            return tokens;
        }

        /// <summary>
        /// Determines whether the position points to a triple-quoted string opening (""" or ''').
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="i">The current position.</param>
        /// <param name="n">The source length.</param>
        /// <returns>True if a triple-quoted string starts at i.</returns>
        private static bool IsTripleQuoteOpen(string source, int i, int n)
        {
            char c = source[i];
            return (c == '"' || c == '\'') && i + 2 < n &&
                source[i + 1] == c && source[i + 2] == c;
        }

        /// <summary>
        /// Scans a triple-quoted string starting at i and appends it as a TripleString token.
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="i">The start position (pointing at the first quote).</param>
        /// <param name="n">The source length.</param>
        /// <param name="quote">The quote character.</param>
        /// <param name="tokens">The token list to append to.</param>
        /// <returns>The position after the triple-quoted string.</returns>
        private static int ScanTripleString(string source, int i, int n,
            char quote, List<Token> tokens)
        {
            int start = i;
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
            return i;
        }

        /// <summary>
        /// Scans a single-line string starting at i and appends it as a String token. Recognizes
        /// backslash escapes and stops at a newline or the closing quote.
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="i">The start position (pointing at the opening quote).</param>
        /// <param name="n">The source length.</param>
        /// <param name="quote">The quote character.</param>
        /// <param name="tokens">The token list to append to.</param>
        /// <returns>The position after the string.</returns>
        private static int ScanString(string source, int i, int n,
            char quote, List<Token> tokens)
        {
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
            return i;
        }

        /// <summary>
        /// Scans a single-line comment starting at i and appends it as a Comment token.
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="i">The start position (pointing at #).</param>
        /// <param name="n">The source length.</param>
        /// <param name="tokens">The token list to append to.</param>
        /// <returns>The position after the comment.</returns>
        private static int ScanComment(string source, int i, int n,
            List<Token> tokens)
        {
            int start = i;

            while (i < n && source[i] != '\n')
            {
                i++;
            }

            tokens.Add(new Token { Kind = TokenKind.Comment,
                Text = source.Substring(start, i - start) });
            return i;
        }

        /// <summary>
        /// Determines whether the position is a raw string prefix (r or R followed by a quote)
        /// that is not part of a longer identifier (the previous accumulated code character, if
        /// any, must not be a word character).
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="i">The current position.</param>
        /// <param name="n">The source length.</param>
        /// <param name="code">The accumulated code buffer (used to inspect the previous char).</param>
        /// <returns>True if r/R at i is a raw string prefix.</returns>
        private static bool IsRawStringPrefix(string source, int i, int n,
            StringBuilder code)
        {
            char c = source[i];
            if (c != 'r' && c != 'R')
            {
                return false;
            }

            if (i + 1 >= n)
            {
                return false;
            }

            char next = source[i + 1];
            if (next != '"' && next != '\'')
            {
                return false;
            }

            if (code.Length > 0 && IsWordChar(code[code.Length - 1]))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the position is a StringName (&amp;) or NodePath (^) prefix
        /// immediately followed by a quote.
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="i">The current position.</param>
        /// <param name="n">The source length.</param>
        /// <param name="c">The character at i.</param>
        /// <returns>True if &amp;/^ at i is a string sigil prefix.</returns>
        private static bool IsStringSigilPrefix(string source, int i, int n,
            char c)
        {
            if (c != '&' && c != '^')
            {
                return false;
            }

            if (i + 1 >= n)
            {
                return false;
            }

            char next = source[i + 1];
            return next == '"' || next == '\'';
        }

        /// <summary>
        /// Determines whether a character is a word character (letter, digit, underscore).
        /// </summary>
        /// <param name="c">The character to test.</param>
        /// <returns>True if the character is a letter, digit, or underscore.</returns>
        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
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
