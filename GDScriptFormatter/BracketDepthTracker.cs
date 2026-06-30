namespace GDScriptFormatter
{
    /// <summary>
    /// Static utility class for tracking bracket/parenthesis/brace depth across lines.
    /// Provides two variants of UpdateDepth — one that scans every character in the line
    /// (used by LineLengthProcessor where the code mask is not available per-line in
    /// UpdateBraceDepth), and one that respects the isCode mask (for contexts where
    /// brackets inside string literals or comments should be ignored).
    /// </summary>
    internal static class BracketDepthTracker
    {
        /// <summary>
        /// Updates the running bracket depth by scanning every character in the line.
        /// Does NOT check the isCode mask — use this overload when the caller has
        /// already verified that the line contains only code characters (or when
        /// the code mask is not available, as in LineLengthProcessor.UpdateBraceDepth).
        /// </summary>
        /// <param name="line">The line to scan.</param>
        /// <param name="currentDepth">The incoming bracket depth from previous lines.</param>
        /// <returns>The bracket depth after processing the line.</returns>
        internal static int UpdateDepth(string line, int currentDepth)
        {
            int depth = currentDepth;

            foreach (char c in line)
            {
                if (c == '(' || c == '[' || c == '{')
                {
                    depth++;
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                }
            }

            return depth;
        }

        /// <summary>
        /// Updates the running bracket depth by scanning only Code-region characters
        /// in the line (skipping characters inside string literals and comments).
        /// </summary>
        /// <param name="line">The line to scan.</param>
        /// <param name="isCode">The code mask of the full text.</param>
        /// <param name="lineStart">The starting offset of this line in the isCode array.</param>
        /// <param name="currentDepth">The incoming bracket depth from previous lines.</param>
        /// <returns>The bracket depth after processing the line.</returns>
        internal static int UpdateDepth(string line, bool[] isCode,
            int lineStart, int currentDepth)
        {
            int depth = currentDepth;
            int end = lineStart + line.Length;

            if (end > isCode.Length)
            {
                end = isCode.Length;
            }

            for (int i = lineStart; i < end; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = line[i - lineStart];

                if (c == '(' || c == '[' || c == '{')
                {
                    depth++;
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                }
            }

            return depth;
        }

        /// <summary>
        /// Computes the bracket depth starting from <paramref name="startIdx"/>
        /// in the given line, scanning only Code-region characters. Used by
        /// LineLengthProcessor.SplitLongLine to compute the bracketDepth for
        /// determining whether the line has unclosed brackets.
        /// </summary>
        /// <param name="line">The line to scan.</param>
        /// <param name="isCode">The code mask of the line (or full text).</param>
        /// <param name="startIdx">The starting character index within the line.</param>
        /// <returns>The bracket depth at the end of the line.</returns>
        internal static int FindBracketDepth(string line, bool[] isCode,
            int startIdx)
        {
            int depth = 0;

            for (int i = startIdx; i < line.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = line[i];

                if (c == '(' || c == '[' || c == '{')
                {
                    depth++;
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                }
            }

            return depth;
        }
    }
}
