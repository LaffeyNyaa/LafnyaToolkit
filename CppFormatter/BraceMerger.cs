using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Merges lone open braces onto the previous line (K&amp;R style)
    /// and provides brace-matching utilities used by do-while merging.
    /// </summary>
    internal static class BraceMerger
    {
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

            return TextUtils.MatchesWord(text, doStart, "do");
        }
    }
}
