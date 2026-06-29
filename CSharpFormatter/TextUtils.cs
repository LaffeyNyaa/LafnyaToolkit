using System.Collections.Generic;
using System.Text;

namespace CSharpFormatter
{
    /// <summary>
    /// Shared constants, data structures, and utility methods used across
    /// all formatting modules.
    /// </summary>
    internal static class TextUtils
    {
        /// <summary>Number of spaces per indentation level.</summary>
        public const int IndentSize = 4;
        /// <summary>Maximum allowed line length.</summary>
        public const int MaxLineLength = 80;
        /// <summary>
        /// Represents a text insertion point used by brace enforcement.
        /// </summary>
        public struct Insertion
        {
            /// <summary>The character position at which to insert.</summary>
            public int Position;
            /// <summary>The text to insert.</summary>
            public string Text;
            /// <summary>
            /// Creates a new insertion record.
            /// </summary>
            /// <param name="position">The character position.</param>
            /// <param name="text">The text to insert.</param>
            public Insertion(int position, string text)
            {
                Position = position;
                Text = text;
            }
        }

        /// <summary>
        /// Represents a text replacement range used by enum and property
        /// formatting.
        /// </summary>
        public struct Replacement
        {
            /// <summary>The start position (inclusive).</summary>
            public int Start;
            /// <summary>The end position (exclusive).</summary>
            public int End;
            /// <summary>The replacement text.</summary>
            public string NewText;
            /// <summary>
            /// Creates a new replacement record.
            /// </summary>
            /// <param name="start">The start position.</param>
            /// <param name="end">The end position.</param>
            /// <param name="newText">The replacement text.</param>
            public Replacement(int start, int end, string newText)
            {
                Start = start;
                End = end;
                NewText = newText;
            }
        }

        /// <summary>
        /// Determines whether the text at <paramref name="pos"/> matches
        /// the given word with word-boundary checks on both sides.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="pos">The starting position.</param>
        /// <param name="word">The word to match.</param>
        /// <returns>true if the word matches at the given position;
        /// otherwise false.</returns>
        public static bool MatchesWord(string text, int pos, string word)
        {
            if (pos + word.Length > text.Length)
            {
                return false;
            }

            for (int i = 0; i < word.Length; i++)
            {
                if (text[pos + i] != word[i])
                {
                    return false;
                }
            }

            if (pos + word.Length < text.Length &&
                IsWordChar(text[pos + word.Length]))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether a character can be part of an identifier
        /// (letter, digit, or underscore).
        /// </summary>
        /// <param name="c">The character.</param>
        /// <returns>true if the character is a word character.</returns>
        public static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        /// <summary>
        /// Skips whitespace characters starting from <paramref name="pos"/>.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="pos">The starting position.</param>
        /// <returns>The position of the first non-whitespace character at
        /// or after <paramref name="pos"/>.</returns>
        public static int SkipWhitespace(string text, int pos)
        {
            while (pos < text.Length &&
                (text[pos] == ' ' || text[pos] == '\t' ||
                text[pos] == '\n' || text[pos] == '\r'))
            {
                pos++;
            }

            return pos;
        }

        /// <summary>
        /// Finds the next open brace <c>{</c> in a code region, stopping
        /// at a semicolon.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="start">The starting position.</param>
        /// <returns>The position of the brace, or -1 if not found.</returns>
        public static int FindOpenBrace(string text, bool[] isCode,
            int start)
        {
            int i = start;
            while (i < text.Length)
            {
                if (isCode[i] && text[i] == '{')
                {
                    return i;
                }

                if (text[i] == ';')
                {
                    return -1;
                }

                i++;
            }

            return -1;
        }

        /// <summary>
        /// Finds the close brace <c>}</c> that matches the open brace at
        /// <paramref name="openPos"/>.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="openPos">The position of the open brace.</param>
        /// <returns>The position of the matching close brace, or
        /// -1.</returns>
        public static int FindMatchingClose(string text, bool[] isCode,
            int openPos)
        {
            int depth = 1;
            int i = openPos + 1;
            while (i < text.Length)
            {
                if (isCode[i])
                {
                    if (text[i] == '{')
                    {
                        depth++;
                    }

                    else if (text[i] == '}')
                    {
                        depth--;

                        if (depth == 0)
                        {
                            return i;
                        }
                    }
                }

                i++;
            }

            return -1;
        }

        /// <summary>
        /// Applies a sorted list of replacements to the text.
        /// </summary>
        /// <param name="text">The original text.</param>
        /// <param name="replacements">The replacements to apply.</param>
        /// <returns>The text with replacements applied.</returns>
        public static string ApplyReplacements(string text,
            List<Replacement> replacements)
        {
            if (replacements.Count == 0)
            {
                return text;
            }

            replacements.Sort((a, b) => a.Start.CompareTo(b.Start));
            var sb = new StringBuilder(text.Length);
            int pos = 0;
            foreach (var r in replacements)
            {
                if (r.Start < pos)
                {
                    continue;
                }

                sb.Append(text, pos, r.Start - pos);
                sb.Append(r.NewText);
                pos = r.End;
            }

            sb.Append(text, pos, text.Length - pos);
            return sb.ToString();
        }

        /// <summary>
        /// Splits text into lines on <c>\n</c>.
        /// </summary>
        /// <param name="text">The text to split.</param>
        /// <returns>A list of lines.</returns>
        public static List<string> SplitLines(string text)
        {
            return new List<string>(text.Split('\n'));
        }

        /// <summary>
        /// Ensures the text ends with exactly one newline character.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The text with a single trailing newline.</returns>
        public static string EnsureSingleTrailingNewline(string text)
        {
            string trimmed = text.TrimEnd('\n', '\r');
            return trimmed + "\n";
        }

        /// <summary>
        /// Determines whether <paramref name="s"/> starts with the keyword
        /// <paramref name="kw"/> at a word boundary.
        /// </summary>
        /// <param name="s">The string to inspect.</param>
        /// <param name="kw">The keyword.</param>
        /// <returns>true if the string starts with the keyword;
        /// otherwise false.</returns>
        public static bool StartsWithKeyword(string s, string kw)
        {
            if (!s.StartsWith(kw))
            {
                return false;
            }

            if (s.Length == kw.Length)
            {
                return true;
            }

            char next = s[kw.Length];
            return !char.IsLetterOrDigit(next) && next != '_';
        }

        /// <summary>
        /// Determines whether a trimmed line ends with an open brace.
        /// </summary>
        /// <param name="s">The string to inspect.</param>
        /// <returns>true if the string ends with <c>{</c>.</returns>
        public static bool EndsWithOpenBrace(string s)
        {
            string t = s.TrimEnd();
            return t.Length > 0 && t[t.Length - 1] == '{';
        }

        /// <summary>
        /// Computes the starting text position of each line.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <returns>An array where element i is the starting position of
        /// line i in the reconstructed text.</returns>
        public static int[] ComputeLineStarts(List<string> lines)
        {
            var lineStarts = new int[lines.Count];
            int pos = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                lineStarts[i] = pos;
                pos += lines[i].Length;

                if (i < lines.Count - 1)
                {
                    pos++;
                }
            }

            return lineStarts;
        }

        /// <summary>
        /// Replaces tab characters with four spaces, but only inside Code
        /// tokens. Tabs inside string literals, verbatim strings, and
        /// comments are preserved.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with code-region tabs expanded.</returns>
        public static string ReplaceTabsInCode(string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            var sb = new StringBuilder(text.Length);

            foreach (var token in tokens)
            {
                if (token.Kind == TokenKind.Code)
                {
                    sb.Append(token.Text.Replace("\t", "    "));
                }

                else
                {
                    sb.Append(token.Text);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Moves a trailing open brace from the end of a line to its own
        /// line (Allman style). Only braces in code regions are moved;
        /// braces inside comments or strings are left untouched.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with code-region trailing braces moved to
        /// their own lines.</returns>
        public static string MoveOpenBraceToOwnLine(string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            string[] lines = text.Split('\n');
            var result = new List<string>(lines.Length + 16);
            int lineStart = 0;

            foreach (var line in lines)
            {
                string trimmedEnd = line.TrimEnd();

                if (trimmedEnd.Length > 1 &&
                    trimmedEnd[trimmedEnd.Length - 1] == '{')
                {
                    int bracePos = lineStart + trimmedEnd.Length - 1;

                    if (bracePos < isCode.Length && isCode[bracePos])
                    {
                        string beforeBrace = trimmedEnd.Substring(0,
                            trimmedEnd.Length - 1).TrimEnd();
                        if (beforeBrace.Length > 0)
                        {
                            result.Add(beforeBrace);
                            result.Add("{");
                        }

                        else
                        {
                            result.Add(line);
                        }
                    }

                    else
                    {
                        result.Add(line);
                    }
                }

                else
                {
                    result.Add(line);
                }

                lineStart += line.Length + 1;
            }

            return string.Join("\n", result);
        }
    }
}
