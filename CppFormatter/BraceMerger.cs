using System.Collections.Generic;

using LafnyaToolkit.Core.Text;

namespace CppFormatter
{
    /// <summary>
    /// Merges a lone open brace onto the previous line (K&amp;R style)
    /// and provides brace-matching utilities used by do-while merging.
    /// Stateless; the single shared instance is exposed via
    /// <see cref="Instance"/>.
    /// </summary>
    internal sealed class BraceMerger
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly BraceMerger Instance = new BraceMerger();
        private BraceMerger()
        {
        }

        /// <summary>
        /// Merges a <c>{</c> that sits on its own line back onto the
        /// previous line (K&amp;R style). Only merges when <c>{</c> is
        /// alone on its line and lies in a code region; braces inside
        /// string literals or comments are left untouched.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with lone open braces merged onto the previous line.</returns>
        public string MoveOpenBraceToPreviousLine(string text)
        {
            var tokens = CppTokenizer.Instance.Tokenize(text);
            bool[] isCode = CppTokenizer.Instance.BuildCodeMask(text, tokens);
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
        /// Finds the matching open brace for a close brace at
        /// <paramref name="closePos"/> by scanning backward through
        /// code regions only. Returns -1 if no match is found.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">Boolean mask indicating code regions.</param>
        /// <param name="closePos">The position of the close brace.</param>
        /// <returns>The position of the matching open brace, or -1.</returns>
        public int FindMatchingOpenBrace(string text, bool[] isCode,
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
        /// Determines whether the keyword "do" immediately precedes the
        /// open brace at <paramref name="openBracePos"/>, ignoring any
        /// whitespace between them.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">Boolean mask indicating code regions.</param>
        /// <param name="openBracePos">The position of the open brace.</param>
        /// <returns>True if "do" immediately precedes the open brace.</returns>
        public bool IsDoKeywordBefore(string text, bool[] isCode,
            int openBracePos)
        {
            int i = openBracePos - 1;

            while (i >= 0 && (text[i] == ' ' || text[i] == '\t' || text[i] ==
                '\n' || text[i] == '\r'))
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

            return TextUtils.MatchesWord(text, doStart, "do");
        }
    }
}
