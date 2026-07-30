using System.Collections.Generic;

using LafnyaToolkit.Core.Text;

namespace CppFormatter
{
    /// <summary>
    /// Merges a lone closing brace that terminates a do-while body
    /// with the following while line, producing K&amp;R style
    /// "} while (cond);". Stateless; the shared instance is exposed
    /// via <see cref="Instance"/>.
    /// </summary>
    internal sealed class DoWhileMerger
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly DoWhileMerger Instance = new DoWhileMerger();
        private DoWhileMerger()
        {
        }

        /// <summary>
        /// Merges a lone closing brace that terminates a do-while body
        /// with the following while line. Only braces in code regions
        /// are considered; braces inside strings or comments are left
        /// untouched.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with merged do-while closing braces.</returns>
        public string MergeDoWhileCloseBrace(string text)
        {
            string[] lines = text.Split('\n');
            var tokens = CppTokenizer.Instance.Tokenize(text);
            bool[] isCode = CppTokenizer.Instance.BuildCodeMask(text, tokens);
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

                if ((trimmed == "}" || trimmed == "};") && i + 1 < lines.Length)
                {
                    int braceOffset = lines[i].IndexOf('}');
                    int bracePos = lineStart + braceOffset;

                    if (bracePos < isCode.Length && isCode[bracePos])
                    {
                        int openBracePos =
                            BraceMerger.Instance.FindMatchingOpenBrace(text,
                            isCode, bracePos);

                        if (openBracePos >= 0 &&
                            BraceMerger.Instance.IsDoKeywordBefore(text, isCode,
                            openBracePos))
                        {
                            int j = i + 1;

                            while (j < lines.Length && lines[j].Trim().Length ==
                                0)
                            {
                                j++;
                            }

                            if (j < lines.Length &&
                                TextUtils.StartsWithKeyword(lines[j].Trim(),
                                "while"))
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
    }
}
