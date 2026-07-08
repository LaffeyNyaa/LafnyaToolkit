using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Computes the brace-nesting depth for each line of source text.
    /// Adjusts depth based on namespace declarations (no extra indent for
    /// namespace bodies) and handles closing braces that reduce depth below
    /// the line-start depth.
    /// </summary>
    internal static class IndentationDepthComputer
    {
        /// <summary>
        /// Computes the base brace-nesting depth for each line. The first pass
        /// walks every character in <paramref name="text"/>, incrementing depth
        /// on <c>{</c> and decrementing on <c>}</c> (only in code regions),
        /// and recording the depth at the start of each line.
        ///
        /// Namespace bodies do not increase depth (they match the enclosing
        /// scope's indentation level).
        /// </summary>
        internal static int[] ComputeDepths(List<string> lines, string text,
            bool[] isCode)
        {
            int[] depths = new int[lines.Count];
            int depth = 0;
            int lineIdx = 0;
            bool pendingNamespace = false;

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
                    if (pendingNamespace)
                    {
                        pendingNamespace = false;
                    }
                    else
                    {
                        depth++;
                    }
                }
                else if (isCode[i] && c == '}')
                {
                    depth--;

                    if (depth < 0)
                    {
                        depth = 0;
                    }

                    // Only update depths[lineIdx] when the closing brace
                    // reduces depth below what was recorded at the start of
                    // this line.  This prevents a `}` that merely closes a
                    // `{` _on the same line_ (e.g. inside {{"x", y}}) from
                    // overwriting the line-start depth that was correctly
                    // set by the preceding `\n` handler.

                    if (lineIdx < depths.Length)
                    {
                        int startDepth = depths[lineIdx];

                        if (depth < startDepth)
                        {
                            depths[lineIdx] = depth;
                        }
                    }
                }

                if (isCode[i] && c == 'n' &&
                    (i == 0 || !TextUtils.IsWordChar(text[i - 1])) &&
                    TextUtils.MatchesWord(text, i, "namespace"))
                {
                    pendingNamespace = true;
                }

                // Reset pendingNamespace if we encounter characters that
                // terminate a namespace declaration (; for alias, = for
                // assignment, or non-identifier chars that are not { or :).

                if (pendingNamespace && c == ';')
                {
                    pendingNamespace = false;
                }

                if (pendingNamespace && c == '=')
                {
                    pendingNamespace = false;
                }

                if (pendingNamespace && c == '(')
                {
                    pendingNamespace = false;
                }
            }

            return depths;
        }
    }
}
