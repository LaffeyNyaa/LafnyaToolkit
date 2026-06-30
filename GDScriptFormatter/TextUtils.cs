using System.Collections.Generic;
using System.Text;

namespace GDScriptFormatter
{
    /// <summary>
    /// Shared constants and utility methods used across all GDScript
    /// formatting modules.
    /// </summary>
    internal static class TextUtils
    {
        /// <summary>4 spaces per indentation level.</summary>
        public const int IndentSize = 4;

        /// <summary>Maximum line length.</summary>
        public const int MaxLineLength = 80;

        /// <summary>
        /// Replaces tabs with 4 spaces only at Code-region positions, preserving tabs inside string
        /// literals and comments so that string contents are never modified.
        /// </summary>
        /// <param name="text">The normalized text.</param>
        /// <param name="isCode">The code mask of the text.</param>
        /// <returns>The text with Code-region tabs expanded to 4 spaces.</returns>
        internal static string NormalizeTabs(string text, bool[] isCode)
        {
            if (text.Length == 0)
            {
                return text;
            }

            var sb = new StringBuilder(text.Length + 16);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\t' && i < isCode.Length && isCode[i])
                {
                    sb.Append("    ");
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Ensures a single space between the hash prefix and the comment
        /// content for all comment lines. Lines consisting entirely of
        /// hash characters (e.g. separators) and lines that already have
        /// whitespace after the hashes are left unchanged.
        /// </summary>
        internal static string NormalizeCommentSpaces(string text)
        {
            if (text.Length == 0)
            {
                return text;
            }

            var sb = new StringBuilder(text.Length + 16);
            int i = 0;
            int len = text.Length;

            while (i < len)
            {
                if (text[i] == '#')
                {
                    // Find end of hash prefix on this line.
                    int hashEnd = i;

                    while (hashEnd < len && text[hashEnd] == '#')
                    {
                        hashEnd++;
                    }

                    hashEnd--; // last '#' position
                    // Find end of line.
                    int lineEnd = hashEnd + 1;

                    while (lineEnd < len && text[lineEnd] != '\n')
                    {
                        lineEnd++;
                    }

                    // If there is content after the hashes and the next
                    // character is not whitespace, insert a space.

                    if (hashEnd + 1 < lineEnd)
                    {
                        char next = text[hashEnd + 1];

                        if (next != ' ' && next != '\t')
                        {
                            sb.Append(text, i, hashEnd + 1 - i);
                            sb.Append(' ');
                            sb.Append(text, hashEnd + 1, lineEnd - hashEnd - 1);

                            if (lineEnd < len)
                            {
                                sb.Append('\n');
                            }

                            i = lineEnd + 1;
                            continue;
                        }
                    }

                    // Append the whole line unchanged.
                    sb.Append(text, i, lineEnd - i);

                    if (lineEnd < len)
                    {
                        sb.Append('\n');
                    }

                    i = lineEnd + 1;
                }
                else
                {
                    // Skip to next newline.
                    int lineEnd = i;

                    while (lineEnd < len && text[lineEnd] != '\n')
                    {
                        lineEnd++;
                    }

                    sb.Append(text, i, lineEnd - i);

                    if (lineEnd < len)
                    {
                        sb.Append('\n');
                    }

                    i = lineEnd + 1;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Splits text into lines.
        /// </summary>
        internal static List<string> SplitLines(string text)
        {
            return new List<string>(text.Split('\n'));
        }

        /// <summary>
        /// Ensures the file ends with exactly one newline character.
        /// </summary>
        internal static string EnsureSingleTrailingNewline(string text)
        {
            string trimmed = text.TrimEnd('\n', '\r');
            return trimmed + "\n";
        }

        /// <summary>
        /// Determines whether the text at the given position matches the specified word (must be preceded/followed by non-word characters or boundaries).
        /// </summary>
        internal static bool MatchesWord(string text, int pos, string word)
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
        /// Determines whether a character is a word character (letter, digit, underscore).
        /// </summary>
        internal static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        /// <summary>
        /// Determines whether a string starts with the specified keyword (followed by a non-word character or end of string).
        /// </summary>
        internal static bool StartsWithKeyword(string s, string kw)
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
        /// Determines whether a line is a block-start line (a code line ending with a colon).
        /// </summary>
        internal static bool IsBlockStartLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (EndsWithColon(trimmed))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a line ends with a colon (excluding colons inside strings/comments — the caller
        /// already validates with code mask in Reindent; this is a text heuristic only).
        /// </summary>
        internal static bool EndsWithColon(string s)
        {
            string t = s.TrimEnd();

            if (t.Length == 0)
            {
                return false;
            }

            if (t[t.Length - 1] == ':')
            {
                return true;
            }

            return false;
        }
    }
}
