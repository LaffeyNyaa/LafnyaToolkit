using System.Collections.Generic;

namespace JavaFormatter
{
    /// <summary>
    /// Recomputes leading whitespace for each line based on nesting depth and
    /// continuation context. Lines inside text blocks and multi-line comments
    /// preserve their original indentation.
    /// </summary>
    internal static class IndentationProcessor
    {
        /// <summary>
        /// Recomputes leading whitespace for each line based on nesting depth.
        /// Lines entirely inside a TextBlock or MultiLineComment token (non-first line)
        /// preserve their original leading whitespace.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <param name="text">The full source text corresponding to <paramref name="lines"/>.</param>
        /// <returns>The re-indented lines.</returns>
        public static List<string> Reindent(List<string> lines, string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            int[] depths = new int[lines.Count];
            bool[] preserveIndent = ComputePreserveIndent(lines, tokens);
            bool[] inEnumBlock = ComputeInEnumBlock(lines, text, isCode);
            int depth = 0;
            int lineIdx = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\n')
                {
                    lineIdx++;

                    if (lineIdx < depths.Length)
                    {
                        depths[lineIdx] = depth;
                    }

                    continue;
                }

                if (isCode[i] && c == '{')
                {
                    depth++;
                }

                else if (isCode[i] && c == '}')
                {
                    depth--;

                    if (depth < 0)
                    {
                        depth = 0;
                    }

                    if (lineIdx < depths.Length)
                    {
                        depths[lineIdx] = depth;
                    }
                }
            }

            var result = new List<string>(lines.Count);
            var lineStarts = new int[lines.Count];
            int p = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                lineStarts[i] = p;
                p += lines[i].Length;

                if (i < lines.Count - 1)
                {
                    p++;
                }
            }

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

                if (i > 0 && !inEnumBlock[i] &&
                    TextUtils.IsContinuationIndicator(lines[i - 1],
                    lineStarts[i - 1], text, isCode))
                {
                    baseDepth++;
                }

                result.Add(new string(' ', baseDepth * Formatter.IndentSize) +
                    content);
            }

            return result;
        }

        /// <summary>
        /// Computes whether each line should preserve its original leading whitespace:
        /// true when the line starts inside a TextBlock or MultiLineComment token
        /// (non-first line).
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <param name="tokens">The token list for the corresponding text.</param>
        /// <returns>A boolean array indicating preserve-indent flags per line.</returns>
        public static bool[] ComputePreserveIndent(List<string> lines,
            List<Token> tokens)
        {
            var preserveIndent = new bool[lines.Count];
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

            int tokenPos = 0;

            foreach (var token in tokens)
            {
                int tokenStart = tokenPos;
                int tokenEnd = tokenPos + token.Text.Length;

                if (token.Kind == TokenKind.TextBlock ||
                    token.Kind == TokenKind.MultiLineComment)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lineStarts[i] > tokenStart &&
                            lineStarts[i] < tokenEnd)
                        {
                            preserveIndent[i] = true;
                        }
                    }
                }

                tokenPos = tokenEnd;
            }

            return preserveIndent;
        }

        /// <summary>
        /// Computes whether each line is inside an enum block (between enum's { and }).
        /// Enum member lines ending with , should not be treated as continuation indicators.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask for <paramref name="text"/>.</param>
        /// <returns>A boolean array indicating in-enum-block flags per line.</returns>
        private static bool[] ComputeInEnumBlock(List<string> lines,
            string text, bool[] isCode)
        {
            var inEnumBlock = new bool[lines.Count];
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

            var enumRanges = new List<KeyValuePair<int, int>>();
            int depth = 0;
            int enumDepth = -1;
            int enumStart = -1;
            bool pendingEnum = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = text[i];

                if (c == 'e' && (i == 0 || !TextUtils.IsWordChar(text[i - 1])) &&
                    TextUtils.MatchesWord(text, i, "enum"))
                {
                    pendingEnum = true;
                }

                if (c == '{')
                {
                    if (pendingEnum)
                    {
                        enumStart = i;
                        enumDepth = depth + 1;
                        pendingEnum = false;
                    }

                    depth++;
                }

                else if (c == '}')
                {
                    depth--;

                    if (depth < 0)
                    {
                        depth = 0;
                    }

                    if (enumDepth >= 0 && depth < enumDepth)
                    {
                        enumRanges.Add(new KeyValuePair<int, int>(enumStart,
                            i));
                        enumStart = -1;
                        enumDepth = -1;
                    }
                }

                else if (c == ';')
                {
                    pendingEnum = false;
                }
            }

            foreach (var range in enumRanges)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lineStarts[i] > range.Key &&
                        lineStarts[i] < range.Value)
                    {
                        inEnumBlock[i] = true;
                    }
                }
            }

            return inEnumBlock;
        }
    }
}
