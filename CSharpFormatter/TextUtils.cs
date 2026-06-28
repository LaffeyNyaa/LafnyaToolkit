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
        /// Determines whether a trimmed line is a using directive.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>true if the line is a using directive.</returns>
        public static bool IsUsingDirective(string trimmed)
        {
            if (trimmed.StartsWith("using "))
            {
                return true;
            }

            if (trimmed.StartsWith("using\t"))
            {
                return true;
            }

            return trimmed == "using";
        }

        /// <summary>
        /// Determines whether a trimmed line is a block-start line (starts
        /// with a declaration or control-flow keyword). This is a text-only
        /// check; callers should also verify the line is in a code region.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>true if the line starts a block.</returns>
        public static bool IsBlockStartLine(string trimmed)
        {
            if (trimmed.Length == 0 || trimmed == "{")
            {
                return false;
            }

            if (trimmed.EndsWith(";"))
            {
                return false;
            }

            if (StartsWithKeyword(trimmed, "using") &&
                !trimmed.Contains("("))
            {
                return false;
            }

            string[] keywords =
                {
                "namespace", "interface", "unchecked", "finally",
                    "foreach", "checked", "struct", "switch", "catch",
                    "class", "while", "unsafe", "using", "enum", "else",
                    "for", "try", "do", "if", "lock", "fixed"
                };

            foreach (var kw in keywords)
            {
                if (StartsWithKeyword(trimmed, kw))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a trimmed line is a block-end line: exactly
        /// <c>}</c> or <c>};</c>.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>true if the line is a block end.</returns>
        public static bool IsBlockEndLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            return trimmed == "}" || trimmed == "};";
        }

        /// <summary>
        /// Determines whether a trimmed line is a switch case/default label
        /// line. This is a text-only check; callers should also verify the
        /// line is in a code region.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>true if the line is a case label.</returns>
        public static bool IsCaseLabelLine(string trimmed)
        {
            if (trimmed.Length == 0 || !trimmed.EndsWith(":"))
            {
                return false;
            }

            return StartsWithKeyword(trimmed, "case") ||
                StartsWithKeyword(trimmed, "default");
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
        /// Computes a per-line flag indicating whether the first
        /// non-whitespace character of each line falls within a code
        /// region. Used by token-aware blank-line and case-scope rules.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="isCode">The code mask of the full text.</param>
        /// <returns>A boolean array; true means the line's first
        /// non-whitespace character is in a code region.</returns>
        public static bool[] ComputeIsCodeLine(List<string> lines,
            bool[] isCode)
        {
            var isCodeLine = new bool[lines.Count];
            int[] lineStarts = ComputeLineStarts(lines);

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                int firstNonWs = 0;

                while (firstNonWs < line.Length &&
                    (line[firstNonWs] == ' ' || line[firstNonWs] == '\t'))
                {
                    firstNonWs++;
                }

                if (firstNonWs < line.Length)
                {
                    int textPos = lineStarts[i] + firstNonWs;
                    isCodeLine[i] = textPos < isCode.Length &&
                        isCode[textPos];
                }
            }

            return isCodeLine;
        }

        /// <summary>
        /// Finds the index of the last non-whitespace code-region character in
        /// the line. Scans backward from the end of <paramref name="line"/>,
        /// skipping positions whose corresponding <paramref name="isCode"/>
        /// entry is false and skipping space/tab characters. Correctly handles
        /// trailing comments (e.g., <c>code, // comment</c>).
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The starting offset of this line in
        /// <paramref name="text"/>.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask of <paramref name="text"/>.</param>
        /// <returns>The index in <paramref name="line"/> of the last code-region
        /// non-whitespace character, or -1 if none exists.</returns>
        public static int LastCodeCharIndex(string line, int lineStart,
            string text, bool[] isCode)
        {
            for (int i = line.Length - 1; i >= 0; i--)
            {
                int textPos = lineStart + i;
                if (textPos < 0 || textPos >= isCode.Length ||
                    !isCode[textPos])
                {
                    continue;
                }

                char c = line[i];
                if (c == ' ' || c == '\t')
                {
                    continue;
                }

                return i;
            }

            return -1;
        }

        /// <summary>
        /// Determines whether the line ends with a statement terminator
        /// (<c>;</c> or <c>}</c>) within a code region.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The starting offset of this line in
        /// <paramref name="text"/>.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>true if the last code-region character is <c>;</c> or
        /// <c>}</c>; otherwise false.</returns>
        public static bool EndsStatement(string line, int lineStart,
            string text, bool[] isCode)
        {
            int idx = LastCodeCharIndex(line, lineStart, text, isCode);
            if (idx < 0)
            {
                return false;
            }

            char last = line[idx];
            return last == ';' || last == '}';
        }

        /// <summary>
        /// Determines whether the specified line ends with a continuation
        /// indicator within a code region. Recognized operators: <c>,</c>,
        /// <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>, <c>%</c>, <c>(</c>,
        /// <c>=</c>, <c>?</c>, <c>&lt;</c>, <c>&gt;</c> (covers <c>=&gt;</c>),
        /// <c>&amp;&amp;</c>, <c>||</c>. Compound assignment operators
        /// (<c>==</c>, <c>!=</c>, <c>&lt;=</c>, <c>&gt;=</c>, <c>+=</c>,
        /// <c>-=</c>) end with <c>=</c> and are thus covered.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The starting offset of this line in
        /// <paramref name="text"/>.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>true if the line ends with a continuation indicator;
        /// otherwise false.</returns>
        public static bool IsContinuationIndicator(string line, int lineStart,
            string text, bool[] isCode)
        {
            int lastCodeIdx = LastCodeCharIndex(line, lineStart, text, isCode);
            if (lastCodeIdx < 0)
            {
                return false;
            }

            char last = line[lastCodeIdx];

            if (last == ',' || last == '+' || last == '-' || last == '*' ||
                last == '/' || last == '%' || last == '(' || last == '=' ||
                last == '?' || last == '<' || last == '>')
            {
                return true;
            }

            if (lastCodeIdx < 1)
            {
                return false;
            }

            int prevTextPos = lineStart + lastCodeIdx - 1;
            if (prevTextPos < 0 || prevTextPos >= isCode.Length ||
                !isCode[prevTextPos])
            {
                return false;
            }

            string last2 = line.Substring(lastCodeIdx - 1, 2);
            return last2 == "&&" || last2 == "||";
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
