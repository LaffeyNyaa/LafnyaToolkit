using System;
using System.Collections.Generic;
using System.Text;

using LafnyaToolkit.Core.Tokenization;

namespace LafnyaToolkit.Core.Text
{
    /// <summary>
    /// Shared text utilities used by every formatter. Provides the indent
    /// and line-length constants, character predicates, line/keyword
    /// helpers, and post-processing utilities (trim, collapse, ensure
    /// trailing newline, apply replacements/insertions).
    /// </summary>
    public static class TextUtils
    {
        /// <summary>Indentation uses 4 spaces per level.</summary>
        public const int IndentSize = 4;

        /// <summary>Maximum length of a single formatted line.</summary>
        public const int MaxLineLength = 80;

        /// <summary>
        /// One indent level of whitespace (4 spaces).
        /// </summary>
        public const string IndentString = "    ";

        /// <summary>
        /// Determines whether the character is a valid identifier character
        /// (letter, digit, or underscore).
        /// </summary>
        /// <param name="c">The character to test.</param>
        /// <returns>True if the character is a letter, digit, or underscore.</returns>
        public static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        /// <summary>
        /// Determines whether the character is an ASCII digit.
        /// </summary>
        /// <param name="c">The character to test.</param>
        /// <returns>True if the character is 0-9.</returns>
        public static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        /// <summary>
        /// Checks whether <paramref name="word"/> matches the text at
        /// position <paramref name="pos"/>, ensuring it is not followed by
        /// another word character (i.e. it is a whole word).
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="pos">The character position to check.</param>
        /// <param name="word">The word to match.</param>
        /// <returns>True if the word matches and is not a substring of a longer word.</returns>
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

            if (pos + word.Length < text.Length && IsWordChar(text[pos +
                word.Length]))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether the string <paramref name="s"/> starts with the
        /// keyword <paramref name="kw"/>, ensuring the keyword is not
        /// followed by a word character (i.e. it is a whole word).
        /// </summary>
        /// <param name="s">The string to examine.</param>
        /// <param name="kw">The keyword to look for.</param>
        /// <returns>True if <paramref name="s"/> starts with the keyword as a whole word.</returns>
        public static bool StartsWithKeyword(string s, string kw)
        {
            if (!s.StartsWith(kw, StringComparison.Ordinal))
            {
                return false;
            }

            if (s.Length == kw.Length)
            {
                return true;
            }

            char next = s[kw.Length];
            return !IsWordChar(next);
        }

        /// <summary>
        /// Advances <paramref name="pos"/> past any whitespace characters
        /// (spaces, tabs, newlines, carriage returns).
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="pos">The starting position.</param>
        /// <returns>The position of the first non-whitespace character, or text.Length if none.</returns>
        public static int SkipWhitespace(string text, int pos)
        {
            while (pos < text.Length && (text[pos] == ' ' || text[pos] == '\t'
                || text[pos] == '\n' || text[pos] == '\r'))
            {
                pos++;
            }

            return pos;
        }

        /// <summary>
        /// Splits text by '\n' into a list of lines (without the newline).
        /// </summary>
        /// <param name="text">The text to split.</param>
        /// <returns>A list of lines, each without its trailing newline.</returns>
        public static List<string> SplitLines(string text)
        {
            return new List<string>(text.Split('\n'));
        }

        /// <summary>
        /// Replaces any tab character in code regions with 4 spaces. The
        /// isCode mask is used to preserve tabs inside strings and comments
        /// (which are not code regions).
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code-region mask.</param>
        /// <returns>The text with tabs in code regions replaced by 4 spaces.</returns>
        public static string NormalizeTabsInCode(string text, bool[] isCode)
        {
            if (text.IndexOf('\t') < 0)
            {
                return text;
            }

            var sb = new StringBuilder(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\t' && (isCode == null || isCode[i]))
                {
                    sb.Append(IndentString);
                }
                else
                {
                    sb.Append(text[i]);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Normalizes all line endings to '\n'. Carriage returns are removed
        /// unconditionally; '\r' before '\n' is collapsed into a single '\n'.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with all line endings normalized to '\n'.</returns>
        public static string NormalizeLineEndings(string text)
        {
            if (text.IndexOf('\r') < 0)
            {
                return text;
            }

            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        /// <summary>
        /// Trims trailing whitespace from every line in the text. Lines whose
        /// last character lies inside a multi-line string or multi-line
        /// comment (as determined by <paramref name="lineEndsInsideToken"/>)
        /// are left untouched to preserve raw string content.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="lineEndsInsideToken">Per-line flag from the tokenizer; true means skip that line.</param>
        /// <returns>The text with trailing whitespace removed from each line.</returns>
        public static string TrimTrailingWhitespace(string text,
            bool[] lineEndsInsideToken)
        {
            var lines = SplitLines(text);
            var sb = new StringBuilder(text.Length);

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed;

                if (lineEndsInsideToken != null && i <
                    lineEndsInsideToken.Length && lineEndsInsideToken[i])
                {
                    trimmed = lines[i];
                }
                else
                {
                    trimmed = lines[i].TrimEnd();
                }

                sb.Append(trimmed);

                if (i < lines.Count - 1)
                {
                    sb.Append('\n');
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Collapses runs of 3 or more consecutive blank lines down to a
        /// single blank line.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with at most 2 consecutive newlines (i.e. one blank line) anywhere.</returns>
        public static string CollapseBlankLines(string text)
        {
            if (text.Length < 3)
            {
                return text;
            }

            var sb = new StringBuilder(text.Length);
            int newlineRun = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\n')
                {
                    newlineRun++;

                    if (newlineRun <= 2)
                    {
                        sb.Append('\n');
                    }
                }
                else
                {
                    newlineRun = 0;
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Ensures the file ends with exactly one trailing newline.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with at most one trailing newline character.</returns>
        public static string EnsureSingleTrailingNewline(string text)
        {
            string trimmed = text.TrimEnd('\n', '\r');
            return trimmed + "\n";
        }

        /// <summary>
        /// Applies a list of <see cref="Replacement"/> records to the source
        /// text. Replacements are sorted by start position and applied in
        /// sequence. Overlapping or out-of-order replacements are silently
        /// skipped.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="replacements">The list of replacements to apply.</param>
        /// <returns>The transformed text.</returns>
        public static string ApplyReplacements(string text, List<Replacement>
            replacements)
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
        /// Applies a list of <see cref="Insertion"/> records to the source
        /// text. Insertions are sorted by position in descending order and
        /// applied so that earlier positions in the source remain valid
        /// during the operation.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="insertions">The list of insertions to apply.</param>
        /// <returns>The text with all insertions applied.</returns>
        public static string ApplyInsertions(string text, List<Insertion>
            insertions)
        {
            if (insertions.Count == 0)
            {
                return text;
            }

            insertions.Sort((a, b) => b.Position.CompareTo(a.Position));
            var sb = new StringBuilder(text);

            foreach (var ins in insertions)
            {
                if (ins.Position < 0 || ins.Position > sb.Length)
                {
                    continue;
                }

                sb.Insert(ins.Position, ins.Text);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Determines whether a trimmed line is a comment line (starts with
        /// //, /*, or *).
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a comment.</returns>
        public static bool IsCommentLine(string trimmed)
        {
            return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("/*", StringComparison.Ordinal)
            || trimmed.StartsWith("*", StringComparison.Ordinal);
        }

        /// <summary>
        /// Counts the occurrences of a specific character in a string.
        /// </summary>
        /// <param name="s">The string to scan.</param>
        /// <param name="c">The character to count.</param>
        /// <returns>The number of times <paramref name="c"/> appears in <paramref name="s"/>.</returns>
        public static int CountChar(string s, char c)
        {
            int count = 0;

            foreach (char ch in s)
            {
                if (ch == c)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Determines whether the string ends with an open brace (<c>{</c>)
        /// after trimming trailing whitespace.
        /// </summary>
        /// <param name="s">The string to examine.</param>
        /// <returns>True if the trimmed string ends with <c>{</c>.</returns>
        public static bool EndsWithOpenBrace(string s)
        {
            string t = s.TrimEnd();
            return t.Length > 0 && t[t.Length - 1] == '{';
        }

        /// <summary>
        /// Determines whether a string is a pure identifier: starts with a
        /// letter or underscore and contains only letters, digits, or
        /// underscores.
        /// </summary>
        /// <param name="s">The string to test.</param>
        /// <returns>True if the string is a pure identifier.</returns>
        public static bool IsPureIdentifier(string s)
        {
            if (s.Length == 0)
            {
                return false;
            }

            if (!char.IsLetter(s[0]) && s[0] != '_')
            {
                return false;
            }

            foreach (char c in s)
            {
                if (!IsWordChar(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
