using System.Collections.Generic;
using System.Text;

namespace CppFormatter
{
    /// <summary>
    /// Splits a C++ source character stream into a token sequence, preserving original text and trivia.
    /// </summary>
    internal static class Tokenizer
    {
        /// <summary>
        /// Tokenizes the source and returns a list of tokens.
        /// </summary>
        /// <param name="source">The original source string.</param>
        /// <returns>A list of tokens in order of appearance.</returns>
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

                int rawPrefixLen = TryMatchRawStringPrefix(source, i);

                if (rawPrefixLen >= 0 && !IsPrevIdentChar(source, i))
                {
                    FlushCode(tokens, code);
                    int start = i;
                    i += rawPrefixLen + 2;

                    int delimStart = i;

                    while (i < n && source[i] != '(')
                    {
                        i++;
                    }

                    if (i >= n)
                    {
                        tokens.Add(new Token { Kind = TokenKind.VerbatimString,
                            Text = source.Substring(start, i - start) });

                        continue;
                    }

                    string delim = source.Substring(delimStart, i - delimStart);
                    i++;
                    string terminator = ")" + delim + "\"";

                    int endIdx = source.IndexOf(terminator, i,
                        System.StringComparison.Ordinal);

                    if (endIdx < 0)
                    {
                        i = n;
                    }
                    else
                    {
                        i = endIdx + terminator.Length;
                    }

                    tokens.Add(new Token { Kind = TokenKind.VerbatimString,
                        Text = source.Substring(start, i - start) });

                    continue;
                }

                int strPrefixLen = TryMatchStringPrefix(source, i);

                if (strPrefixLen >= 0 && (strPrefixLen == 0 ||
                    !IsPrevIdentChar(source, i)))
                {
                    FlushCode(tokens, code);
                    int start = i;
                    i += strPrefixLen + 1;

                    while (i < n)
                    {
                        if (source[i] == '\\')
                        {
                            i += 2;
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
                            i += 2;
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

                        if (source[i] == '\\' && i + 2 < n &&
                            source[i + 1] == '\r' && source[i + 2] == '\n')
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
        /// Constructs a boolean array marking whether each character position belongs to a Code token,
        /// based on the given text and token list.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="tokens">The token list (should be the tokenization result of text).</param>
        /// <returns>A boolean array; true means the position is a Code token character.</returns>
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
        /// Computes the starting character position of each line in the
        /// concatenated full text. Lines are separated by a single '\n'.
        /// </summary>
        /// <param name="lines">The list of lines.</param>
        /// <returns>An array where result[i] is the start position of
        /// lines[i].</returns>
        public static int[] ComputeLineStarts(IList<string> lines)
        {
            var starts = new int[lines.Count];
            int pos = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                starts[i] = pos;
                pos += lines[i].Length;

                if (i < lines.Count - 1)
                {
                    pos++;
                }
            }

            return starts;
        }

        /// <summary>
        /// Computes which lines are entirely inside a multi-line
        /// VerbatimString or MultiLineComment token. A line is protected iff
        /// there exists a VerbatimString or MultiLineComment token whose start
        /// is strictly before the line start and whose end is strictly after
        /// the line end.
        /// </summary>
        /// <param name="text">The full text.</param>
        /// <param name="tokens">The token list.</param>
        /// <param name="lineCount">The number of lines.</param>
        /// <returns>A boolean array; true means the line is entirely inside a
        /// multi-line token and should not be modified by formatting
        /// rules.</returns>
        public static bool[] ComputeProtectedLines(string text,
            List<Token> tokens, int lineCount)
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

                if (t.Kind != TokenKind.VerbatimString &&
                    t.Kind != TokenKind.MultiLineComment)
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
        /// Computes which lines have their last character position inside a
        /// multi-line VerbatimString or MultiLineComment token. Used by
        /// TrimTrailingWhitespace: if a line's last character is inside a
        /// multi-line token, trailing whitespace should not be trimmed to
        /// avoid breaking raw string contents.
        /// </summary>
        /// <param name="text">The full text.</param>
        /// <param name="tokens">The token list.</param>
        /// <param name="lineStarts">The line start positions computed by
        /// ComputeLineStarts.</param>
        /// <param name="lines">The list of lines.</param>
        /// <returns>A boolean array; true means the line's end position is
        /// inside a multi-line token.</returns>
        public static bool[] ComputeLineEndsInsideToken(string text,
            List<Token> tokens, int[] lineStarts, IList<string> lines)
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

                if (t.Kind != TokenKind.VerbatimString &&
                    t.Kind != TokenKind.MultiLineComment)
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
        /// Outputs accumulated Code characters as a Code token and clears the accumulator.
        /// </summary>
        /// <param name="tokens">The token list.</param>
        /// <param name="code">The StringBuilder accumulating Code characters.</param>
        private static void FlushCode(List<Token> tokens, StringBuilder code)
        {
            if (code.Length > 0)
            {
                tokens.Add(new Token { Kind = TokenKind.Code,
                    Text = code.ToString() });

                code.Clear();
            }
        }

        /// <summary>
        /// Determines whether the '#' at position index is at the start of a line
        /// (preceded only by whitespace or the beginning of the file).
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="index">The position of the current '#'.</param>
        /// <returns>true if the '#' is at line start; otherwise false.</returns>
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

        /// <summary>
        /// Determines whether a given character is an identifier character (letter, digit, or underscore).
        /// </summary>
        /// <param name="c">The character to test.</param>
        /// <returns>true if the character is an identifier character; otherwise false.</returns>
        private static bool IsIdentChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') || c == '_';
        }

        /// <summary>
        /// Determines whether the character before position i is an identifier character.
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="i">The current position.</param>
        /// <returns>true if the previous character is an identifier character; otherwise false.</returns>
        private static bool IsPrevIdentChar(string source, int i)
        {
            if (i == 0)
            {
                return false;
            }

            return IsIdentChar(source[i - 1]);
        }

        /// <summary>
        /// Attempts to match the prefix of a raw string literal at position i,
        /// returning the prefix length (excluding R and ").
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="i">The current position.</param>
        /// <returns>The prefix length (0, 1, or 2); -1 if not the start of a raw string literal.</returns>
        private static int TryMatchRawStringPrefix(string source, int i)
        {
            int n = source.Length;

            if (i + 1 < n && source[i] == 'R' && source[i + 1] == '"')
            {
                return 0;
            }

            if (i + 2 < n && (source[i] == 'L' || source[i] == 'u' ||
                source[i] == 'U') &&
                source[i + 1] == 'R' && source[i + 2] == '"')
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
        /// Attempts to match the prefix of an ordinary string literal at position i,
        /// returning the prefix length (excluding ").
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="i">The current position.</param>
        /// <returns>The prefix length (0, 1, or 2); -1 if not the start of a string literal.</returns>
        private static int TryMatchStringPrefix(string source, int i)
        {
            int n = source.Length;

            if (i < n && source[i] == '"')
            {
                return 0;
            }

            if (i + 1 < n && (source[i] == 'L' || source[i] == 'u' ||
                source[i] == 'U') &&
                source[i + 1] == '"')
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
