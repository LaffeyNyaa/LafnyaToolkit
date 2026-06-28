using System.Collections.Generic;
using System.Text;

namespace JavaFormatter
{
    /// <summary>
    /// Represents a text insertion at a specific character offset.
    /// </summary>
    internal struct Insertion
    {
        /// <summary>The character offset at which to insert.</summary>
        public int Position;

        /// <summary>The text to insert.</summary>
        public string Text;

        /// <summary>Creates a new insertion.</summary>
        /// <param name="position">The character offset.</param>
        /// <param name="text">The text to insert.</param>
        public Insertion(int position, string text)
        {
            Position = position;
            Text = text;
        }
    }

    /// <summary>
    /// Represents a replacement of a text range with new text.
    /// </summary>
    internal struct Replacement
    {
        /// <summary>The start offset (inclusive).</summary>
        public int Start;

        /// <summary>The end offset (exclusive).</summary>
        public int End;

        /// <summary>The replacement text.</summary>
        public string NewText;

        /// <summary>Creates a new replacement.</summary>
        /// <param name="start">The start offset (inclusive).</param>
        /// <param name="end">The end offset (exclusive).</param>
        /// <param name="newText">The replacement text.</param>
        public Replacement(int start, int end, string newText)
        {
            Start = start;
            End = end;
            NewText = newText;
        }
    }

    /// <summary>
    /// Shared text-processing utilities used across formatter modules.
    /// All structural text operations are token-aware to avoid damaging
    /// comments, string literals and text blocks.
    /// </summary>
    internal static class TextUtils
    {
        /// <summary>Keywords that introduce a block-start line.</summary>
        private static readonly string[] BlockStartKeywords =
        {
            "package", "interface", "synchronized", "finally", "abstract",
            "implements", "extends", "throws", "class", "switch", "catch",
            "enum", "while", "else", "for", "try", "do", "if"
        };

        /// <summary>
        /// Determines whether <paramref name="word"/> occurs at <paramref name="pos"/>
        /// in <paramref name="text"/> as a whole word (not part of a larger identifier).
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="pos">The starting position.</param>
        /// <param name="word">The word to match.</param>
        /// <returns>True if the word matches as a whole word; otherwise false.</returns>
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

            if (pos + word.Length < text.Length && IsWordChar(text[pos + word.Length]))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether a character may be part of an identifier.
        /// </summary>
        /// <param name="c">The character.</param>
        /// <returns>True for letters, digits and underscore; otherwise false.</returns>
        public static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        /// <summary>
        /// Advances <paramref name="pos"/> past any whitespace characters.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="pos">The starting position.</param>
        /// <returns>The position of the next non-whitespace character.</returns>
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
        /// Finds the next open brace in a code region starting from <paramref name="start"/>.
        /// A semicolon encountered first aborts the search.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="start">The starting position.</param>
        /// <returns>The index of the open brace, or -1 if not found.</returns>
        public static int FindOpenBrace(string text, bool[] isCode, int start)
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
        /// Finds the matching close brace for the open brace at <paramref name="openPos"/>.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="openPos">The position of the open brace.</param>
        /// <returns>The index of the matching close brace, or -1 if unbalanced.</returns>
        public static int FindMatchingClose(string text, bool[] isCode, int openPos)
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
        /// Applies a sorted list of non-overlapping replacements to the text.
        /// </summary>
        /// <param name="text">The source text.</param>
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
        /// Splits text into a list of lines on '\n'.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The list of lines.</returns>
        public static List<string> SplitLines(string text)
        {
            return new List<string>(text.Split('\n'));
        }

        /// <summary>
        /// Ensures the text ends with exactly one newline.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with exactly one trailing newline.</returns>
        public static string EnsureSingleTrailingNewline(string text)
        {
            string trimmed = text.TrimEnd('\n', '\r');
            return trimmed + "\n";
        }

        /// <summary>
        /// Determines whether <paramref name="s"/> starts with the keyword
        /// <paramref name="kw"/> followed by a non-identifier character.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <param name="kw">The keyword.</param>
        /// <returns>True if the string starts with the keyword; otherwise false.</returns>
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
        /// Determines whether the trimmed string ends with an open brace.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <returns>True if the string ends with '{'; otherwise false.</returns>
        public static bool EndsWithOpenBrace(string s)
        {
            string t = s.TrimEnd();
            return t.Length > 0 && t[t.Length - 1] == '{';
        }

        /// <summary>
        /// Determines whether the trimmed line is a block start line: a non-empty,
        /// non-brace-only, non-annotation line that starts with a block keyword.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a block start; otherwise false.</returns>
        public static bool IsBlockStartLine(string trimmed)
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

            if (trimmed.StartsWith("@"))
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
        /// Determines whether the trimmed line is a block end line: exactly "}" or "};".
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a block end; otherwise false.</returns>
        public static bool IsBlockEndLine(string trimmed)
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
        /// Determines whether the trimmed line is an import directive.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is an import directive; otherwise false.</returns>
        public static bool IsImportDirective(string trimmed)
        {
            if (trimmed.StartsWith("import "))
            {
                return true;
            }

            if (trimmed.StartsWith("import\t"))
            {
                return true;
            }

            return trimmed == "import";
        }

        /// <summary>
        /// Determines whether the trimmed line is a do-while tail: starts with the
        /// "while" keyword and ends with ");".
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a do-while tail; otherwise false.</returns>
        public static bool IsDoWhileTail(string trimmed)
        {
            if (!StartsWithKeyword(trimmed, "while"))
            {
                return false;
            }

            return trimmed.EndsWith(");");
        }

        /// <summary>
        /// Determines whether the trimmed line is a block continuation keyword:
        /// catch, finally, or else.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a block continuation; otherwise false.</returns>
        public static bool IsBlockContinuation(string trimmed)
        {
            return StartsWithKeyword(trimmed, "catch") ||
                StartsWithKeyword(trimmed, "finally") ||
                StartsWithKeyword(trimmed, "else");
        }

        /// <summary>
        /// Replaces tabs with four spaces only inside Code tokens, preserving
        /// tabs inside string literals, char literals, text blocks and comments.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with code-region tabs expanded to four spaces.</returns>
        public static string NormalizeTabs(string text)
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
        /// Moves a code-region open brace that occupies its own line to the end of
        /// the previous non-empty line (K&amp;R style). Braces inside comments,
        /// strings and text blocks are left untouched. The operation is idempotent.
        /// </summary>
        /// <param name="text">The source text (newline-normalized to '\n').</param>
        /// <returns>The text with solo open braces joined to the previous line.</returns>
        public static string EnsureOpenBraceOnSameLine(string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            string[] lines = text.Split('\n');

            var lineStarts = new int[lines.Length];
            int pos = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                lineStarts[i] = pos;
                pos += lines[i].Length + 1;
            }

            var result = new List<string>(lines.Length);
            int lastNonEmptyResultIdx = -1;
            string lastNonEmptyLine = null;
            bool lastNonEmptyEndsInCode = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();
                bool merged = false;

                if (trimmed == "{")
                {
                    int braceIdxInLine = line.IndexOf('{');
                    int bracePosInText = lineStarts[i] + braceIdxInLine;
                    bool braceInCode = bracePosInText >= 0 &&
                        bracePosInText < isCode.Length && isCode[bracePosInText];

                    if (braceInCode && lastNonEmptyLine != null &&
                        lastNonEmptyEndsInCode)
                    {
                        string prevTrimmedEnd = lastNonEmptyLine.TrimEnd();

                        if (prevTrimmedEnd.Length > 0)
                        {
                            string mergedLine = prevTrimmedEnd + " {";
                            result[lastNonEmptyResultIdx] = mergedLine;
                            lastNonEmptyLine = mergedLine;
                            lastNonEmptyEndsInCode = true;
                            merged = true;
                        }
                    }
                }

                if (merged)
                {
                    continue;
                }

                result.Add(line);

                if (trimmed.Length == 0)
                {
                    continue;
                }

                lastNonEmptyResultIdx = result.Count - 1;
                lastNonEmptyLine = line;
                string trimmedEnd = line.TrimEnd();

                if (trimmedEnd.Length > 0)
                {
                    int lastIdxInLine = trimmedEnd.Length - 1;
                    int lastPosInText = lineStarts[i] + lastIdxInLine;
                    lastNonEmptyEndsInCode = lastPosInText >= 0 &&
                        lastPosInText < isCode.Length && isCode[lastPosInText];
                }

                else
                {
                    lastNonEmptyEndsInCode = false;
                }
            }

            return string.Join("\n", result);
        }
    }
}
