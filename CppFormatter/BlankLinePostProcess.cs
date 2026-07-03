using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Post-processing methods for blank-line operations.
    /// </summary>
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Collapses 3 or more consecutive blank lines into 1. Lines entirely
        /// inside a multi-line string or comment token are preserved verbatim
        /// and never participate in blank-line collapsing.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full source text corresponding to
        /// <paramref name="lines"/>.</param>
        /// <returns>The processed line list.</returns>
        internal static List<string> CollapseBlankLines(List<string> lines,
            string text)
        {
            var tokens = Tokenizer.Tokenize(text);

            bool[] protectedLines = Tokenizer.ComputeProtectedLines(text,
                tokens, lines.Count);

            var result = new List<string>(lines.Count);
            int blankRun = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                if (i < protectedLines.Length && protectedLines[i])
                {
                    result.Add(lines[i]);
                    blankRun = 0;
                    continue;
                }

                if (lines[i].Trim().Length == 0)
                {
                    blankRun++;

                    if (blankRun <= 1)
                    {
                        result.Add(string.Empty);
                    }
                }
                else
                {
                    blankRun = 0;
                    result.Add(lines[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Trims trailing whitespace from each line. Lines whose last character
        /// lies inside a multi-line string or comment token are preserved
        /// verbatim to avoid damaging raw string contents.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full source text corresponding to
        /// <paramref name="lines"/>.</param>
        /// <returns>The processed line list.</returns>
        internal static List<string> TrimTrailingWhitespace(List<string> lines,
            string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

            bool[] endsInside = Tokenizer.ComputeLineEndsInsideToken(text,
                tokens, lineStarts, lines);

            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                if (i < endsInside.Length && endsInside[i])
                {
                    result.Add(lines[i]);
                }
                else
                {
                    result.Add(lines[i].TrimEnd());
                }
            }

            return result;
        }
    }
}
