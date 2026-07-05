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
                    // A line that starts with a closing bracket returns
                    // to the parent indent level rather than continuing
                    // at the continuation indent — but only when closing
                    // the outermost bracket (EndBracketDepth == 0).
                    // When inside nested brackets (EndBracketDepth > 0),
                    // keep the continuation indent so that synthetically
                    // introduced wrapping brackets (e.g. from = (...) line
                    // splitting) do not lose their indentation on a second
                    // formatting pass. Using EndBracketDepth instead of
                    // StartBracketDepth ensures stability across formatting
                    // passes because EndBracketDepth reflects the bracket
                    // depth after the line is fully processed, which is
                    // independent of synthetic parentheses added on a prior
                    // formatting pass.

                    if (content.Length == 0 ||
                        (content[0] != ')' && content[0] != ']' &&
                        content[0] != '}') ||
                        lineInfo[i].EndBracketDepth > 0)
                    {
                        // Indent continuation lines based on bracket nesting depth.
                        // Non-closing content uses StartBracketDepth (depth at line
                        // start), so content inside nested brackets (e.g., elements
                        // inside % [...] within print(...)) gets progressively deeper
                        // indentation. Closing brackets that don't close all brackets
                        // (EndBracketDepth > 0) use EndBracketDepth so they align
                        // with same-level content rather than jumping deeper.
                        int inc = 1;

                        if (content.Length > 0)
                        {
                            if (content[0] == ')' || content[0] == ']' ||
                                content[0] == '}')
                            {
                                inc = lineInfo[i].EndBracketDepth;
                            }
                            else
                            {
                                inc = lineInfo[i].StartBracketDepth;
                            }

                            if (inc < 1)
                            {
                                inc = 1;
                            }
                        }

                        baseDepth += inc;
                    }

                    // Additional fix for continuation lines that close the
                    // outermost bracket: block-stack entries pushed by
                    // colon-terminated lines inside the continuation context
                    // (e.g., func(): inside callback parens) inflate
                    // depths[i]. The closing bracket should return to the
                    // indentation level of the line that opened the bracket,
                    // which is the last non-continuation line's depth.

                    if (content.Length > 0 &&
                        (content[0] == ')' || content[0] == ']') &&
                        lineInfo[i].EndBracketDepth == 0 &&
                        depths[i] > 0)
                    {
                        for (int j = i - 1; j >= 0; j--)
                        {
                            if (!lineInfo[j].IsContinuation)
                            {
                                baseDepth = depths[j];
                                break;
                            }
                        }
                    }
                }

                result.Add(new string(' ',
                    baseDepth * TextUtils.IndentSize) + content);
            }

            return result;
        }
    }
}
