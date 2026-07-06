using System.Collections.Generic;

namespace GDScriptFormatter
{
    /// <summary>
    /// Recomputes indentation for each line based on colon/brace block depth,
    /// bracket continuation, and triple-quoted string preservation.
    /// </summary>
    internal static partial class IndentationProcessor
    {
        /// <summary>
        /// Colon-based indentation recalculation: infers block depth stack from original indentation,
        /// colon-terminated code lines (not inside brackets) open a new block, bracket depth &gt; 0 or
        /// previous line ending with \ indicates a continuation line (indented one extra level). Lines inside
        /// triple-quoted strings preserve their original indentation. Reuses the caller-provided tokens
        /// and code mask instead of re-tokenizing.
        /// </summary>
        /// <param name="lines">The input lines.</param>
        /// <param name="text">The full text corresponding to the lines.</param>
        /// <param name="tokens">The tokenization of text (reused).</param>
        /// <param name="isCode">The code mask of text (reused).</param>
        /// <returns>The re-indented lines.</returns>
        internal static List<string> Reindent(List<string> lines, string text,
            List<Token> tokens, bool[] isCode)
        {
            bool[] preserveIndent = ComputePreserveIndent(lines, tokens);
            var lineStarts = ComputeLineStarts(lines);
            var lineInfo = ComputeLineInfo(lines, text, isCode, lineStarts);

            int[] depths = ComputeDepthsFromStack(lines, lineInfo,
                preserveIndent);

            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                if (preserveIndent[i])
                {
                    result.Add(lines[i]);
                    continue;
                }

                string content = lines[i].TrimStart();

                if (content.Length == 0)
                {
                    result.Add(string.Empty);
                    continue;
                }

                int baseDepth = depths[i];

                if (lineInfo[i].IsContinuation)
                {
                    if (content[0] == ')' || content[0] == ']' ||
                        content[0] == '}')
                    {
                        baseDepth = GetClosingBracketBaseDepth(lineInfo, depths,
                            i);
                    }
                    else
                    {
                        baseDepth += GetContinuationIncrement(lineInfo[i],
                            content);
                    }
                }

                result.Add(new string(' ',
                    baseDepth * TextUtils.IndentSize) + content);
            }

            return result;
        }

        private static int GetClosingBracketBaseDepth(LineAnalysis[] lineInfo,
            int[] depths, int i)
        {
            int targetOpenDepth =
                lineInfo[i].StartBracketDepth - 1;

            for (int j = i - 1; j >= 0; j--)
            {
                if (lineInfo[j].StartBracketDepth ==
                    targetOpenDepth &&
                    lineInfo[j].EndBracketDepth >
                    targetOpenDepth)
                {
                    return depths[j] +
                        lineInfo[j].StartBracketDepth;
                }
            }

            for (int j = i - 1; j >= 0; j--)
            {
                if (!lineInfo[j].IsContinuation)
                {
                    return depths[j];
                }
            }

            return depths[i];
        }

        private static int GetContinuationIncrement(LineAnalysis lineInfo,
            string content)
        {
            int inc = 1;

            if (content.Length > 0)
            {
                inc = lineInfo.StartBracketDepth;

                if (inc < 1)
                {
                    inc = 1;
                }
            }

            return inc;
        }
    }
}
