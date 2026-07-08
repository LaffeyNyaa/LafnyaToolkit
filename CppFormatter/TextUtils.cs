using System;
using System.Collections.Generic;
using System.Text;

namespace CppFormatter
{
    /// <summary>
    /// Shared constants, data structures, and utility methods used across
    /// all C++ formatting modules.
    /// </summary>
    internal static partial class TextUtils
    {
        /// <summary>Indentation uses 4 spaces per level.</summary>
        public const int IndentSize = 4;

        /// <summary>Maximum length of a single line.</summary>
        public const int MaxLineLength = 80;

        /// <summary>Keywords that introduce a brace-delimited block.</summary>
        private static readonly string[] BlockStartKeywords =
            { "namespace", "struct", "switch", "catch", "class", "while",
            "union", "enum", "else", "for", "try", "do", "if" };

        /// <summary>
        /// Checks whether <paramref name="word"/> matches the text at position <paramref name="pos"/>,
        /// ensuring it is not followed by another word character (i.e. it is a whole word).
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="pos">The character position to check.</param>
        /// <param name="word">The word to match.</param>
        /// <returns>True if the word matches and is not a substring of a longer word.</returns>
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

            if (pos + word.Length < text.Length && IsWordChar(text[pos +
                word.Length]))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the character is a valid C++ identifier character
        /// (letter, digit, or underscore).
        /// </summary>
        /// <param name="c">The character to test.</param>
        /// <returns>True if the character is a letter, digit, or underscore.</returns>
        internal static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        /// <summary>
        /// Advances <paramref name="pos"/> past any whitespace characters
        /// (spaces, tabs, newlines, carriage returns).
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="pos">The starting position.</param>
        /// <returns>The position of the first non-whitespace character, or text.Length if none.</returns>
        internal static int SkipWhitespace(string text, int pos)
        {
            while (pos < text.Length && (text[pos] == ' ' || text[pos] == '\t'
                || text[pos] == '\n' || text[pos] == '\r'))
            {
                pos++;
            }

            return pos;
        }

        /// <summary>
        /// Finds the first open brace (<c>{</c>) at or after <paramref name="start"/>
        /// that lies in a code region. Stops early if a semicolon (<c>;</c>) is
        /// encountered before any brace, returning -1.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">Boolean mask indicating code regions.</param>
        /// <param name="start">The character position to start searching from.</param>
        /// <returns>The position of the open brace, or -1 if not found.</returns>
        internal static int FindOpenBrace(string text, bool[] isCode, int start)
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
        /// Finds the matching closing brace for the open brace at <paramref name="openPos"/>,
        /// respecting brace nesting and only considering code regions.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">Boolean mask indicating code regions.</param>
        /// <param name="openPos">The position of the open brace.</param>
        /// <returns>The position of the matching close brace, or -1 if not found.</returns>
        internal static int FindMatchingClose(string text, bool[] isCode,
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
        /// Applies a list of <see cref="Replacement"/> records to the source
        /// text. Replacements are sorted by start position and applied in
        /// sequence. Overlapping or out-of-order replacements are silently
        /// skipped.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="replacements">The list of replacements to apply.</param>
        /// <returns>The transformed text.</returns>
        internal static string ApplyReplacements(string text,
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
        /// Splits text by lines.
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
        /// Checks whether the string <paramref name="s"/> starts with the
        /// keyword <paramref name="kw"/>, ensuring the keyword is not followed
        /// by a word character (i.e. it is a whole word).
        /// </summary>
        /// <param name="s">The string to examine.</param>
        /// <param name="kw">The keyword to look for.</param>
        /// <returns>True if <paramref name="s"/> starts with the keyword as a whole word.</returns>
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
        /// Determines whether the string ends with an open brace (<c>{</c>)
        /// after trimming trailing whitespace.
        /// </summary>
        /// <param name="s">The string to examine.</param>
        /// <returns>True if the trimmed string ends with <c>{</c>.</returns>
        internal static bool EndsWithOpenBrace(string s)
        {
            string t = s.TrimEnd();
            return t.Length > 0 && t[t.Length - 1] == '{';
        }

        /// <summary>
        /// Determines whether a line starts with a block-start keyword.
        /// C++ keyword set: catch/class/do/else/enum/for/if/namespace/struct/
        /// switch/try/union/while.
        /// </summary>
        internal static bool IsBlockStartLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed == "{")
            {
                return false;
            }

            if (trimmed.EndsWith(";"))
            {
                return false;
            }

            foreach (var kw in BlockStartKeywords)
            {
                if (StartsWithKeyword(trimmed, kw))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a line is a block-end line: }, };, or }
        /// followed by comments/closing delimiters. Lines like
        /// "} else {", "} else if (...)", "} catch (...)" are NOT
        /// block-end lines (they continue into a new block).
        /// </summary>
        internal static bool IsBlockEndLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed[0] != '}')
            {
                return false;
            }

            // Check what follows after the leading }
            string afterBrace = trimmed.Substring(1).TrimStart();
            // Pure block-end: just "}", "};", or "} // comment"

            if (afterBrace.Length == 0)
            {
                return true;
            }

            if (afterBrace == ";")
            {
                return true;
            }

            if (afterBrace.StartsWith("//") ||
                afterBrace.StartsWith("/*"))
            {
                return true;
            }

            // "} else {", "} else if (...)", "} catch (...)" —
            // these continue into a new block, not a real block-end.

            if (StartsWithKeyword(afterBrace, "else") ||
                StartsWithKeyword(afterBrace, "catch"))
            {
                return false;
            }

            // Everything else: "});", "} while (cond);",
            // "}  // comment" (handled above), etc. — block end.
            return true;
        }

        /// <summary>
        /// Determines whether a trimmed line is an access specifier:
        /// public:, protected:, or private:.
        /// </summary>
        internal static bool IsAccessSpecifier(string trimmed)
        {
            return trimmed == "public:" ||
                trimmed == "protected:" ||
                trimmed == "private:";
        }

        /// <summary>
        /// Determines whether a trimmed line is a comment.
        /// </summary>
        internal static bool IsCommentLine(string trimmed)
        {
            return trimmed.StartsWith("//") || trimmed.StartsWith("/*") ||
                trimmed.StartsWith("*");
        }

        /// <summary>
        /// Determines whether a line is an #include directive.
        /// </summary>
        internal static bool IsIncludeDirective(string trimmed)
        {
            return trimmed.StartsWith("#include");
        }

        /// <summary>
        /// Determines whether a string is a pure C++ identifier: starts with
        /// a letter or underscore and contains only letters, digits, or underscores.
        /// </summary>
        internal static bool IsPureIdentifier(string s)
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
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Counts the occurrences of a specific character in a string.
        /// </summary>
        internal static int CountChar(string s, char c)
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
        /// Determines whether a string looks like a member initializer
        /// (identifier followed by parentheses or braces for initialization).
        /// </summary>
        internal static bool LooksLikeMemberInitializer(string s)
        {
            if (s.Length == 0)
            {
                return false;
            }

            // Find first '(' or '{' that follows an identifier
            int parenPos = s.IndexOf('(');
            int bracePos = s.IndexOf('{');
            int initPos = -1;

            if (parenPos >= 0 && bracePos >= 0)
            {
                initPos = Math.Min(parenPos, bracePos);
            }
            else if (parenPos >= 0)
            {
                initPos = parenPos;
            }
            else if (bracePos >= 0)
            {
                initPos = bracePos;
            }

            if (initPos <= 0)
            {
                return false;
            }

            // Check if the part before '(' or '{' is an identifier
            string beforeInit = s.Substring(0, initPos);

            return IsPureIdentifier(beforeInit) ||
                (beforeInit.EndsWith("_") && beforeInit.Length > 1 &&
                IsPureIdentifier(beforeInit.Substring(0, beforeInit.Length -
                1)));
        }

        /// <summary>
        /// Represents a text insertion point used by brace enforcement.
        /// </summary>
        internal struct Insertion
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
        /// Represents a text replacement range used by enum formatting.
        /// </summary>
        internal struct Replacement
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
    }
}
