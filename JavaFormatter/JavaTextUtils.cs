using System.Collections.Generic;
using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace JavaFormatter
{
    /// <summary>
    /// Java-specific text helpers that complement
    /// <see cref="LafnyaToolkit.Core.Text.TextUtils"/>. Provides
    /// brace-pair navigation (respecting code regions) and line-end
    /// normalization utilities used by the Java formatter pipeline.
    /// Stateless; the single shared instance is exposed via
    /// <see cref="Instance"/>.
    /// </summary>
    internal sealed class JavaTextUtils
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly JavaTextUtils Instance = new JavaTextUtils();

        private JavaTextUtils()
        {
        }

        /// <summary>
        /// Finds the first open brace (<c>{</c>) at or after
        /// <paramref name="start"/> that lies in a code region.
        /// Stops early if a semicolon is encountered before any brace,
        /// returning -1.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">Boolean mask indicating code regions.</param>
        /// <param name="start">The character position to start searching from.</param>
        /// <returns>The position of the open brace, or -1 if not found.</returns>
        public int FindOpenBrace(string text, bool[] isCode, int start)
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
        /// Finds the matching closing brace for the open brace at
        /// <paramref name="openPos"/>, respecting brace nesting and
        /// only considering code regions.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">Boolean mask indicating code regions.</param>
        /// <param name="openPos">The position of the open brace.</param>
        /// <returns>The position of the matching close brace, or -1 if not found.</returns>
        public int FindMatchingClose(string text, bool[] isCode, int openPos)
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
        /// Replaces tabs with four spaces only inside Code tokens,
        /// preserving tabs inside string literals, char literals,
        /// text blocks, and comments.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with code-region tabs expanded to four spaces.</returns>
        public string NormalizeTabs(string text)
        {
            var tokens = JavaTokenizer.Instance.Tokenize(text);
            var sb = new System.Text.StringBuilder(text.Length);

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
        /// Splits text into a list of lines on '\n'.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The list of lines.</returns>
        public List<string> SplitLines(string text)
        {
            return new List<string>(text.Split('\n'));
        }

        /// <summary>
        /// Computes the starting text position of each line.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <returns>An array where element i is the starting position of
        /// line i in the reconstructed text.</returns>
        public int[] ComputeLineStarts(List<string> lines)
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
        /// Ensures the text ends with exactly one newline.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with exactly one trailing newline.</returns>
        public string EnsureSingleTrailingNewline(string text)
        {
            string trimmed = text.TrimEnd('\n', '\r');
            return trimmed + "\n";
        }

        /// <summary>
        /// Moves a code-region open brace that occupies its own line to
        /// the end of the previous non-empty line (K&amp;R style).
        /// Braces inside comments, strings, and text blocks are left
        /// untouched. The operation is idempotent.
        /// </summary>
        /// <param name="text">The source text (newline-normalized to '\n').</param>
        /// <returns>The text with solo open braces joined to the previous line.</returns>
        public string EnsureOpenBraceOnSameLine(string text)
        {
            var tokens = JavaTokenizer.Instance.Tokenize(text);
            bool[] isCode = JavaTokenizer.Instance.BuildCodeMask(text, tokens);
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
                        bracePosInText < isCode.Length &&
                        isCode[bracePosInText];

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

        /// <summary>
        /// Determines whether the trimmed string ends with an open brace.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <returns>True if the string ends with '{'; otherwise false.</returns>
        public bool EndsWithOpenBrace(string s)
        {
            string t = s.TrimEnd();
            return t.Length > 0 && t[t.Length - 1] == '{';
        }

        /// <summary>
        /// Determines whether <paramref name="s"/> starts with the
        /// keyword <paramref name="kw"/> followed by a non-identifier
        /// character.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <param name="kw">The keyword.</param>
        /// <returns>True if the string starts with the keyword;
        /// otherwise false.</returns>
        public bool StartsWithKeyword(string s, string kw)
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
            return !TextUtils.IsWordChar(next);
        }
    }
}
