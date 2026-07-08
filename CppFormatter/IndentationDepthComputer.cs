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
        ///
        /// A second pass scans preprocessor conditional directives
        /// (<c>#if</c>/<c>#ifdef</c>/<c>#ifndef</c>/<c>#elif</c>/<c>#else</c>/<c>#endif</c>)
        /// and adjusts depths so that code inside a conditional block receives
        /// an extra indentation level.  Directive lines themselves retain the
        /// enclosing scope depth.
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

            // Second pass: adjust depths for preprocessor conditional
            // directives (#if/#ifdef/#ifndef/#elif/#else/#endif).
            // Directive lines keep the enclosing scope depth; code lines
            // inside the conditional get an extra indent per nesting level.
            int preprocDepth = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].TrimStart();
                string keyword = GetPreprocessorKeyword(trimmed);

                if (keyword == "if" || keyword == "ifdef" ||
                    keyword == "ifndef")
                {
                    depths[i] += preprocDepth;
                    preprocDepth++;
                }
                else if (keyword == "elif" || keyword == "else")
                {
                    if (preprocDepth > 0)
                    {
                        depths[i] += (preprocDepth - 1);
                    }
                }
                else if (keyword == "endif")
                {
                    if (preprocDepth > 0)
                    {
                        depths[i] += (preprocDepth - 1);
                        preprocDepth--;
                    }
                }
                else
                {
                    depths[i] += preprocDepth;
                }
            }

            return depths;
        }

        /// <summary>
        /// Extracts the preprocessor directive keyword from a trimmed line.
        /// Returns <c>null</c> if the line does not start with <c>#</c>
        /// followed by a letter-based keyword.
        /// </summary>
        private static string GetPreprocessorKeyword(string trimmedLine)
        {
            if (trimmedLine.Length == 0 || trimmedLine[0] != '#')
            {
                return null;
            }

            string afterHash = trimmedLine.Substring(1).TrimStart();

            if (afterHash.Length == 0)
            {
                return null;
            }

            int kwEnd = 0;

            while (kwEnd < afterHash.Length &&
                char.IsLetter(afterHash[kwEnd]))
            {
                kwEnd++;
            }

            if (kwEnd == 0)
            {
                return null;
            }

            return afterHash.Substring(0, kwEnd);
        }
    }
}
