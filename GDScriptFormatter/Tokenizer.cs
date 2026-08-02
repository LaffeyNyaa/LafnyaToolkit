using System.Collections.Generic;

using LafnyaToolkit.Core.Tokenization;

namespace GDScriptFormatter
{
    /// <summary>
    /// GDScript-specific tokenizer: recognizes GDScript 2.x prefixed
    /// string literals (raw strings r/R, StringName &amp;, NodePath ^),
    /// triple-quoted strings, single-line strings, and # comments.
    /// The prefix characters r/R/&amp;/^ are emitted as separate Code
    /// tokens; the following string literal is scanned with the standard
    /// string logic.
    /// </summary>
    public sealed class GDScriptTokenizer : TokenizerBase
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly GDScriptTokenizer Instance =
            new GDScriptTokenizer();

        private GDScriptTokenizer()
        {
        }

        /// <summary>
        /// Scans the source at <paramref name="position"/> and returns
        /// either a recognized non-code token (string, comment) or the
        /// length of a single-character Code sigil to emit (raw prefix,
        /// StringName, NodePath), or zero to indicate ordinary code.
        /// </summary>
        /// <param name="source">The full source text.</param>
        /// <param name="position">The current character position.</param>
        /// <param name="token">When the return value is positive, the token to emit.</param>
        /// <returns>The number of characters consumed (positive), or zero if this character is ordinary code.</returns>
        protected override int ScanNextToken(
            string source,
            int position,
            out Token token
        )
        {
            int n = source.Length;
            char c = source[position];

            if (IsTripleQuoteOpen(source, position, n))
            {
                return ScanTripleString(
                    source,
                    position,
                    n,
                    c,
                    out token
                );
            }

            if (c == '"' || c == '\'')
            {
                return ScanString(
                    source,
                    position,
                    n,
                    c,
                    out token
                );
            }

            if (c == '#')
            {
                return ScanComment(
                    source,
                    position,
                    n,
                    out token
                );
            }

            if (IsRawStringPrefix(source, position, n))
            {
                token = new Token(TokenKind.Code, c.ToString(), position);
                return 1;
            }

            if (IsStringSigilPrefix(source, position, n, c))
            {
                token = new Token(TokenKind.Code, c.ToString(), position);
                return 1;
            }

            token = default(Token);
            return 0;
        }

        /// <summary>
        /// Determines whether the position points to a triple-quoted string
        /// opening (<c>"""</c> or <c>'''</c>).
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="i">The current position.</param>
        /// <param name="n">The source length.</param>
        /// <returns>True if a triple-quoted string starts at i.</returns>
        private static bool IsTripleQuoteOpen(
            string source,
            int i,
            int n
        )
        {
            char c = source[i];

            return (c == '"' || c == '\'') && i + 2 < n &&
                source[i + 1] == c && source[i + 2] == c;
        }

        /// <summary>
        /// Scans a triple-quoted string starting at i and emits it as a
        /// <see cref="TokenKind.VerbatimString"/> token.
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="i">The start position (pointing at the first quote).</param>
        /// <param name="n">The source length.</param>
        /// <param name="quote">The quote character.</param>
        /// <param name="token">The emitted token.</param>
        /// <returns>The number of characters consumed.</returns>
        private static int ScanTripleString(
            string source,
            int i,
            int n,
            char quote,
            out Token token
        )
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

            token = new Token(TokenKind.VerbatimString,
                source.Substring(start, i - start), start);

            return i - start;
        }

        /// <summary>
        /// Scans a single-line string starting at i and emits it as a
        /// <see cref="TokenKind.String"/> token. Recognizes backslash
        /// escapes and stops at a newline or the closing quote.
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="i">The start position (pointing at the opening quote).</param>
        /// <param name="n">The source length.</param>
        /// <param name="quote">The quote character.</param>
        /// <param name="token">The emitted token.</param>
        /// <returns>The number of characters consumed.</returns>
        private static int ScanString(
            string source,
            int i,
            int n,
            char quote,
            out Token token
        )
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

            token = new Token(TokenKind.String,
                source.Substring(start, i - start), start);

            return i - start;
        }

        /// <summary>
        /// Scans a single-line comment starting at i and emits it as a
        /// <see cref="TokenKind.SingleLineComment"/> token.
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="i">The start position (pointing at #).</param>
        /// <param name="n">The source length.</param>
        /// <param name="token">The emitted token.</param>
        /// <returns>The number of characters consumed.</returns>
        private static int ScanComment(
            string source,
            int i,
            int n,
            out Token token
        )
        {
            int start = i;

            while (i < n && source[i] != '\n')
            {
                i++;
            }

            token = new Token(TokenKind.SingleLineComment,
                source.Substring(start, i - start), start);

            return i - start;
        }

        /// <summary>
        /// Determines whether the position is a raw string prefix
        /// (r or R followed by a quote) that is not part of a longer
        /// identifier (the previous source character, if any, must not be
        /// a word character).
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="i">The current position.</param>
        /// <param name="n">The source length.</param>
        /// <returns>True if r/R at i is a raw string prefix.</returns>
        private static bool IsRawStringPrefix(
            string source,
            int i,
            int n
        )
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

            if (i > 0 && IsWordChar(source[i - 1]))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the position is a StringName (&amp;) or
        /// NodePath (^) prefix immediately followed by a quote.
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="i">The current position.</param>
        /// <param name="n">The source length.</param>
        /// <param name="c">The character at i.</param>
        /// <returns>True if &amp;/^ at i is a string sigil prefix.</returns>
        private static bool IsStringSigilPrefix(
            string source,
            int i,
            int n,
            char c
        )
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
        /// Determines whether a character is a word character (letter,
        /// digit, underscore).
        /// </summary>
        /// <param name="c">The character to test.</param>
        /// <returns>True if the character is a letter, digit, or underscore.</returns>
        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }
    }
}
