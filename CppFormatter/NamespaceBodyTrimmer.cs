using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Removes blank lines immediately after the opening { and immediately
    /// before the closing } of a namespace body.
    /// </summary>
    internal static class NamespaceBodyTrimmer
    {
        /// <summary>
        /// Removes blank lines immediately after the opening { and immediately
        /// before the closing } of a namespace body.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="tokens">The token list.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>The processed line list.</returns>
        internal static List<string> TrimNamespaceBodyBlankLines(
            List<string> lines, string text, List<Token> tokens,
            bool[] isCode)
        {
            var result = new List<string>(lines.Count);

            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

            var nsBlocks = new List<KeyValuePair<int, int>>();
            int braceDepth = 0;
            var braceStack = new Stack<int>();
            bool pendingNamespace = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = text[i];

                if (c == 'n' && (i == 0 || !TextUtils.IsWordChar(text[i -
                    1])) &&
                    TextUtils.MatchesWord(text, i, "namespace"))
                {
                    pendingNamespace = true;
                }

                if (c == '{')
                {
                    if (pendingNamespace)
                    {
                        nsBlocks.Add(new KeyValuePair<int, int>(i, -1));
                        braceStack.Push(nsBlocks.Count - 1);
                    }
                    else
                    {
                        braceStack.Push(-1);
                    }

                    pendingNamespace = false;
                    braceDepth++;
                }
                else if (c == '}')
                {
                    braceDepth--;

                    if (braceStack.Count > 0)
                    {
                        int idx = braceStack.Pop();

                        if (idx >= 0 && idx < nsBlocks.Count)
                        {
                            nsBlocks[idx] = new KeyValuePair<int, int>(
                                nsBlocks[idx].Key, i);
                        }
                    }

                    pendingNamespace = false;
                }
                else if (c == ';')
                {
                    pendingNamespace = false;
                }
            }

            var removeSet = new HashSet<int>();

            foreach (var block in nsBlocks)
            {
                if (block.Value < 0)
                {
                    continue;
                }

                int openBracePos = block.Key;
                int closeBracePos = block.Value;

                for (int li = 0; li < lines.Count; li++)
                {
                    int ls = lineStarts[li];

                    if (ls <= openBracePos || ls >= closeBracePos)
                    {
                        continue;
                    }

                    if (lines[li].Trim().Length != 0)
                    {
                        continue;
                    }

                    int nextNonBlank = li + 1;

                    while (nextNonBlank < lines.Count &&
                        lines[nextNonBlank].Trim().Length == 0)
                    {
                        nextNonBlank++;
                    }

                    bool isAfterOpenBrace = li > 0 &&
                        lineStarts[li - 1] + lines[li - 1].Length <=
                        openBracePos + 1;

                    if (isAfterOpenBrace)
                    {
                        removeSet.Add(li);
                    }

                    bool isBeforeCloseBrace = nextNonBlank <
                        lines.Count &&
                        lineStarts[nextNonBlank] >= closeBracePos;

                    if (isBeforeCloseBrace)
                    {
                        removeSet.Add(li);
                    }
                }
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (!removeSet.Contains(i))
                {
                    result.Add(lines[i]);
                }
            }

            return result;
        }
    }
}
