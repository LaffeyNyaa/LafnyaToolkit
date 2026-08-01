using System;
using System.Text;

using LafnyaToolkit.Core.Tokenization;

namespace CSharpFormatter
{
    /// <summary>
    /// Tokenizes a C# source character stream into a token sequence,
    /// preserving the original text and trivia. Recognizes single-line
    /// and multi-line comments, ordinary strings, verbatim strings
    /// (<c>@"..."</c>), interpolated strings (<c>$"..."</c>),
    /// interpolated verbatim strings (<c>$@"..."</c> / <c>@$"..."</c>),
    /// character literals, and preprocessor directives.
    /// </summary>
    internal sealed class CSharpTokenizer : TokenizerBase
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly CSharpTokenizer Instance = new CSharpTokenizer();
        private CSharpTokenizer()
        {
        }

        /// <summary>
        /// Scans the source starting at <paramref name="position"/> and
        /// returns either a recognized non-code token (string, comment,
        /// preprocessor) or zero to indicate ordinary code.
        /// </summary>
        /// <param name="source">The full source text.</param>
        /// <param name="position">The character position to scan from.</param>
        /// <param name="token">When the return value is positive, the token to emit.</param>
        /// <returns>The number of characters consumed, or zero if this character is ordinary code.</returns>
        protected override int ScanNextToken(
            string source,
            int position,
            out Token token
        )
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

                while (position < n)
                {
                    if (source[position] == '*' && position + 1 < n &&
                        source[position + 1] == '/')
                    {
                        position += 2;
                        break;
                    }

                    position++;
                }

                token = new Token(TokenKind.MultiLineComment,
                    source.Substring(start, position - start), start);

                return position - start;
            }

            if ((c == '$' && position + 1 < n && source[position + 1] == '@' &&
                position + 2 < n && source[position + 2] == '"') ||
                (c == '@' && position + 1 < n && source[position + 1] == '$' &&
                position + 2 < n && source[position + 2] == '"'))
            {
                int start = position;
                position += 3;
                int braceDepth = 0;

                while (position < n)
                {
                    if (source[position] == '"' && position + 1 < n &&
                        source[position + 1] == '"')
                    {
                        position += 2;
                        continue;
                    }

                    if (source[position] == '{')
                    {
                        braceDepth++;
                    }
                    else if (source[position] == '}')
                    {
                        if (braceDepth > 0)
                        {
                            braceDepth--;
                        }
                    }
                    else if (source[position] == '"' && braceDepth == 0)
                    {
                        position++;
                        break;
                    }

                    position++;
                }

                token = new Token(TokenKind.InterpolatedVerbatimString,
                    source.Substring(start, position - start), start);

                return position - start;
            }

            if (c == '@' && position + 1 < n && source[position + 1] == '"' &&
                (position + 2 >= n || source[position + 2] != '$'))
            {
                int start = position;
                position += 2;

                while (position < n)
                {
                    if (source[position] == '"')
                    {
                        if (position + 1 < n && source[position + 1] == '"')
                        {
                            position += 2;
                            continue;
                        }

                        position++;
                        break;
                    }

                    position++;
                }

                token = new Token(TokenKind.VerbatimString,
                    source.Substring(start, position - start), start);

                return position - start;
            }

            if (c == '$' && position + 1 < n && source[position + 1] == '"')
            {
                int start = position;
                position += 2;
                int braceDepth = 0;

                while (position < n)
                {
                    if (source[position] == '\\' && position + 1 < n)
                    {
                        position += 2;
                        continue;
                    }

                    if (source[position] == '{')
                    {
                        braceDepth++;
                    }
                    else if (source[position] == '}')
                    {
                        if (braceDepth > 0)
                        {
                            braceDepth--;
                        }
                    }
                    else if (source[position] == '"' && braceDepth == 0)
                    {
                        position++;
                        break;
                    }

                    position++;
                }

                token = new Token(TokenKind.InterpolatedString,
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

            if (c == '#' && IsLineStart(source, position))
            {
                int start = position;

                while (position < n)
                {
                    if (source[position] == '\\' && position + 1 < n &&
                        source[position + 1] == '\n')
                    {
                        position += 2;
                        continue;
                    }

                    if (source[position] == '\\' && position + 2 < n &&
                        source[position + 1] == '\r' && source[position + 2] ==
                        '\n')
                    {
                        position += 3;
                        continue;
                    }

                    if (source[position] == '\n')
                    {
                        break;
                    }

                    position++;
                }

                token = new Token(TokenKind.Preprocessor,
                    source.Substring(start, position - start), start);

                return position - start;
            }

            return 0;
        }

        /// <summary>
        /// Determines whether the <c>#</c> at <paramref name="index"/>
        /// is at the start of a line (preceded only by whitespace or the
        /// beginning of the file).
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="index">The position of the <c>#</c> character.</param>
        /// <returns>True if the <c>#</c> is at line start; otherwise false.</returns>
        private static bool IsLineStart(
            string source,
            int index
        )
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
