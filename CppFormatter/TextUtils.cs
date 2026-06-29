using System.Collections.Generic;
using System.Text;

namespace CppFormatter
{
    /// <summary>
    /// Shared constants, data structures, and utility methods used across
    /// all C++ formatting modules.
    /// </summary>
    internal static class TextUtils
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

        internal static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        internal static int SkipWhitespace(string text, int pos)
        {
            while (pos < text.Length && (text[pos] == ' ' || text[pos] == '\t'
            || text[pos] == '\n' || text[pos] == '\r'))
            {
                pos++;
            }

            return pos;
        }

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
        /// Determines whether a line is a block end line: exactly } or };.
        /// </summary>
        internal static bool IsBlockEndLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed == "}")
            {
                return true;
            }

            if (trimmed == "};")
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a line is an #include directive.
        /// </summary>
        internal static bool IsIncludeDirective(string trimmed)
        {
            return trimmed.StartsWith("#include");
        }

        /// <summary>
        /// Merges a { that sits on its own line back onto the previous line
        /// (K&amp;R style). Only merges when { is alone on its line and lies in a
        /// code region; braces inside string literals or comments are left
        /// untouched.
        /// </summary>
        internal static string MoveOpenBraceToPreviousLine(string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            string[] lines = text.Split('\n');
            var result = new List<string>(lines.Length);
            int pos = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                bool merged = false;

                if (trimmed == "{" && i > 0 && result.Count > 0)
                {
                    int bracePos = pos + lines[i].IndexOf('{');
                    bool isCodeBrace = bracePos < isCode.Length &&
                        isCode[bracePos];

                    if (isCodeBrace)
                    {
                        string prev = result[result.Count - 1].TrimEnd();

                        if (prev.Length > 0)
                        {
                            result[result.Count - 1] = prev + " {";
                            merged = true;
                        }
                    }
                }

                if (!merged)
                {
                    result.Add(lines[i]);
                }

                if (i < lines.Length - 1)
                {
                    pos += lines[i].Length + 1;
                }
            }

            return string.Join("\n", result);
        }

        /// <summary>
        /// Merges a lone closing brace that terminates a do-while body with the
        /// following while line, producing K&amp;R style "} while (cond);". Only
        /// braces in code regions are considered; braces inside strings or
        /// comments are left untouched.
        /// </summary>
        internal static string MergeDoWhileCloseBrace(string text)
        {
            string[] lines = text.Split('\n');
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            var result = new List<string>(lines.Length);
            var merged = new bool[lines.Length];
            int pos = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                int lineStart = pos;

                if (i < lines.Length - 1)
                {
                    pos += lines[i].Length + 1;
                }

                if (merged[i])
                {
                    continue;
                }

                string trimmed = lines[i].Trim();

                if ((trimmed == "}" || trimmed == "};") &&
                    i + 1 < lines.Length)
                {
                    int braceOffset = lines[i].IndexOf('}');
                    int bracePos = lineStart + braceOffset;

                    if (bracePos < isCode.Length && isCode[bracePos])
                    {
                        int openBracePos = FindMatchingOpenBrace(text, isCode,
                            bracePos);

                        if (openBracePos >= 0 &&
                            IsDoKeywordBefore(text, isCode, openBracePos))
                        {
                            int j = i + 1;

                            while (j < lines.Length &&
                                lines[j].Trim().Length == 0)
                            {
                                j++;
                            }

                            if (j < lines.Length &&
                                StartsWithKeyword(lines[j].Trim(), "while"))
                            {
                                result.Add(lines[i].TrimEnd() + " " +
                                    lines[j].Trim());
                                merged[j] = true;
                                continue;
                            }
                        }
                    }
                }

                result.Add(lines[i]);
            }

            return string.Join("\n", result);
        }

        /// <summary>
        /// Finds the matching open brace for a close brace at closePos by
        /// scanning backward through code regions only. Returns -1 if no
        /// match is found.
        /// </summary>
        internal static int FindMatchingOpenBrace(string text, bool[] isCode,
            int closePos)
        {
            int depth = 1;
            int i = closePos - 1;

            while (i >= 0)
            {
                if (isCode[i])
                {
                    if (text[i] == '}')
                    {
                        depth++;
                    }
                    else if (text[i] == '{')
                    {
                        depth--;

                        if (depth == 0)
                        {
                            return i;
                        }
                    }
                }

                i--;
            }

            return -1;
        }

        /// <summary>
        /// Determines whether the keyword "do" immediately precedes the open
        /// brace at openBracePos, ignoring any whitespace between them.
        /// </summary>
        internal static bool IsDoKeywordBefore(string text, bool[] isCode,
            int openBracePos)
        {
            int i = openBracePos - 1;

            while (i >= 0 && (text[i] == ' ' || text[i] == '\t' ||
                text[i] == '\n' || text[i] == '\r'))
            {
                i--;
            }

            if (i < 1)
            {
                return false;
            }

            int doStart = i - 1;

            if (doStart >= isCode.Length || !isCode[doStart])
            {
                return false;
            }

            return MatchesWord(text, doStart, "do");
        }
    }
}
