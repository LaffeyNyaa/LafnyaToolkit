using System;
using System.Collections.Generic;

using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace CppFormatter
{
    /// <summary>
    /// Tokenizes a C++ source character stream into a token sequence,
    /// preserving the original text and trivia. Recognizes single-line
    /// and multi-line comments, ordinary strings, raw strings (including
    /// the L/u/U/u8 prefixed variants), character literals, and
    /// preprocessor directives. Provides additional C++-specific
    /// helpers (line-protection masks, line-ends-inside-token masks)
    /// used by downstream processors.
    /// </summary>
    internal sealed class CppTokenizer : TokenizerBase
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly CppTokenizer Instance = new CppTokenizer();
        private CppTokenizer()
        {
        }

        /// <summary>
        /// Scans the source starting at <paramref name="position"/> and
        /// returns either a recognized non-code token (string, comment,
        /// preprocessor) or zero to indicate ordinary code. The full set
        /// of recognized tokens includes:
        /// <list type="bullet">
        ///   <item><description>single-line comment //... to end of line</description></item>
        ///   <item><description>multi-line comment /* ... */</description></item>
        ///   <item><description>raw string literal R"delim(...)delim" (with optional L/u/U/u8 prefix)</description></item>
        ///   <item><description>ordinary string literal "..." (with optional L/u/U/u8 prefix)</description></item>
        ///   <item><description>character literal '...'</description></item>
        ///   <item><description>preprocessor directive #... whole line (with backslash continuation)</description></item>
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

            int rawPrefixLen = TryMatchRawStringPrefix(source, position);

            if (rawPrefixLen >= 0 && !IsPrevIdentChar(source, position))
            {
                int start = position;
                position += rawPrefixLen + 2;
                int delimStart = position;

                while (position < n && source[position] != '(')
                {
                    position++;
                }

                if (position >= n)
                {
                    token = new Token(TokenKind.VerbatimString,
                        source.Substring(start, position - start), start);

                    return position - start;
                }

                string delim = source.Substring(delimStart, position -
                    delimStart);

                position++;
                string terminator = ")" + delim + "\"";

                int endIdx = source.IndexOf(terminator, position,
                    StringComparison.Ordinal);

                if (endIdx < 0)
                {
                    position = n;
                }
                else
                {
                    position = endIdx + terminator.Length;
                }

                token = new Token(TokenKind.VerbatimString,
                    source.Substring(start, position - start), start);

                return position - start;
            }

            int strPrefixLen = TryMatchStringPrefix(source, position);

            if (strPrefixLen >= 0 && (strPrefixLen == 0 ||
                !IsPrevIdentChar(source, position)))
            {
                int start = position;
                position += strPrefixLen + 1;

                while (position < n)
                {
                    if (source[position] == '\\')
                    {
                        position += 2;
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
                        position += 2;
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
        /// Computes which lines are entirely inside a multi-line
        /// VerbatimString or MultiLineComment token. A line is protected
        /// iff there exists such a token whose start is strictly before
        /// the line start and whose end is strictly after the line end.
        /// </summary>
        /// <param name="text">The full text.</param>
        /// <param name="tokens">The token list.</param>
        /// <param name="lineCount">The number of lines.</param>
        /// <returns>A boolean array; true means the line is entirely inside a multi-line token.</returns>
        public bool[] ComputeProtectedLines(string text, List<Token> tokens,
            int lineCount)
        {
            var protectedLines = new bool[lineCount];

            if (lineCount == 0)
            {
                return protectedLines;
            }

            var lines = text.Split('\n');
            int[] lineStarts = ComputeLineStarts(lines);
            int n = lineCount < lines.Length ? lineCount : lines.Length;
            int pos = 0;

            foreach (var t in tokens)
            {
                int tokenStart = pos;
                int tokenEnd = tokenStart + t.Text.Length;
                pos = tokenEnd;

                if (t.Kind != TokenKind.VerbatimString && t.Kind !=
                    TokenKind.MultiLineComment)
                {
                    continue;
                }

                for (int i = 0; i < n; i++)
                {
                    int lineStart = lineStarts[i];
                    int lineEnd = lineStart + lines[i].Length;

                    if (lineStart > tokenStart && lineEnd < tokenEnd)
                    {
                        protectedLines[i] = true;
                    }
                }
            }

            return protectedLines;
        }

        /// <summary>
        /// Computes which lines have their last character position inside
        /// a multi-line VerbatimString or MultiLineComment token. Used by
        /// TrimTrailingWhitespace: when a line's last character is inside a
        /// multi-line token, trailing whitespace must not be trimmed to
        /// avoid breaking raw string contents.
        /// </summary>
        /// <param name="text">The full text.</param>
        /// <param name="tokens">The token list.</param>
        /// <param name="lineStarts">The line start positions.</param>
        /// <param name="lines">The list of lines.</param>
        /// <returns>A boolean array; true means the line's end position is inside a multi-line token.</returns>
        public bool[] ComputeLineEndsInsideToken(string text, List<Token>
            tokens, int[] lineStarts, IList<string> lines)
        {
            int lineCount = lines.Count;
            var result = new bool[lineCount];

            if (lineCount == 0)
            {
                return result;
            }

            int pos = 0;

            foreach (var t in tokens)
            {
                int tokenStart = pos;
                int tokenEnd = tokenStart + t.Text.Length;
                pos = tokenEnd;

                if (t.Kind != TokenKind.VerbatimString && t.Kind !=
                    TokenKind.MultiLineComment)
                {
                    continue;
                }

                for (int i = 0; i < lineCount; i++)
                {
                    if (lines[i].Length == 0)
                    {
                        continue;
                    }

                    int lineEnd = lineStarts[i] + lines[i].Length - 1;

                    if (lineEnd >= tokenStart && lineEnd < tokenEnd)
                    {
                        result[i] = true;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Determines whether the '#' at position <paramref name="index"/>
        /// is at the start of a line (preceded only by whitespace or the
        /// beginning of the file).
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="index">The position of the current '#'.</param>
        /// <returns>True if the '#' is at line start; otherwise false.</returns>
        private static bool IsLineStart(string source, int index)
        {
            int lastNewline = index > 0 ? source.LastIndexOf('\n', index -
                1) : -1;

            for (int j = lastNewline + 1; j < index; j++)
            {
                char ch = source[j];

                if (ch != ' ' && ch != '\t' && ch != '\r')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether a given character is a C++ identifier
        /// character (letter, digit, or underscore).
        /// </summary>
        /// <param name="c">The character to test.</param>
        /// <returns>True if the character is an identifier character.</returns>
        private static bool IsIdentChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >=
                '0' && c <= '9') || c == '_';
        }

        /// <summary>
        /// Determines whether the character before position
        /// <paramref name="i"/> is a C++ identifier character.
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="i">The current position.</param>
        /// <returns>True if the previous character is an identifier character.</returns>
        private static bool IsPrevIdentChar(string source, int i)
        {
            if (i == 0)
            {
                return false;
            }

            return IsIdentChar(source[i - 1]);
        }

        /// <summary>
        /// Attempts to match the prefix of a raw string literal at position
        /// <paramref name="i"/>. Recognizes R"..."/LR"..."/uR"..."/UR"..."/u8R"...".
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="i">The current position.</param>
        /// <returns>The prefix length (excluding R and "), or -1 if not a raw string.</returns>
        private static int TryMatchRawStringPrefix(string source, int i)
        {
            int n = source.Length;

            if (i + 1 < n && source[i] == 'R' && source[i + 1] == '"')
            {
                return 0;
            }

            if (i + 2 < n && (source[i] == 'L' || source[i] == 'u' ||
                source[i] == 'U') && source[i + 1] == 'R' && source[i + 2] ==
                '"')
            {
                return 1;
            }

            if (i + 3 < n && source[i] == 'u' && source[i + 1] == '8' &&
                source[i + 2] == 'R' && source[i + 3] == '"')
            {
                return 2;
            }

            return -1;
        }

        /// <summary>
        /// Attempts to match the prefix of an ordinary string literal at
        /// position <paramref name="i"/>. Recognizes "..."/L"..."/u"..."/U"..."/u8"...".
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="i">The current position.</param>
        /// <returns>The prefix length (excluding "), or -1 if not a string.</returns>
        private static int TryMatchStringPrefix(string source, int i)
        {
            int n = source.Length;

            if (i < n && source[i] == '"')
            {
                return 0;
            }

            if (i + 1 < n && (source[i] == 'L' || source[i] == 'u' ||
                source[i] == 'U') && source[i + 1] == '"')
            {
                return 1;
            }

            if (i + 2 < n && source[i] == 'u' && source[i + 1] == '8' &&
                source[i + 2] == '"')
            {
                return 2;
            }

            return -1;
        }
    }
}
