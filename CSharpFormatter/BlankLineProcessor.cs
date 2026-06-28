using System.Collections.Generic;

namespace CSharpFormatter
{
    /// <summary>
    /// Applies blank-line rules: ensures exactly one blank line around
    /// blocks and declarations, collapses excess blank lines, and trims
    /// trailing whitespace.
    /// </summary>
    internal static class BlankLineProcessor
    {
        /// <summary>
        /// Records a non-blank line together with its original index in
        /// the input list and whether a blank line preceded it. Used by
        /// <see cref="ApplyBlankLineRules"/> to correctly index into the
        /// <paramref name="isCodeLine"/> array after blank lines have been
        /// collapsed.
        /// </summary>
        private struct NonBlankEntry
        {
            /// <summary>The original index of this line in the input
            /// list.</summary>
            public int OriginalIndex;
            /// <summary>Whether a blank line immediately preceded this
            /// line in the input.</summary>
            public bool HadBlankAbove;
            /// <summary>The line text.</summary>
            public string Line;

            public NonBlankEntry(int originalIndex, bool hadBlankAbove,
                string line)
            {
                OriginalIndex = originalIndex;
                HadBlankAbove = hadBlankAbove;
                Line = line;
            }
        }

        /// <summary>
        /// Ensures exactly one blank line above and below blocks/
        /// declarations (applying the start/end exceptions). Uses
        /// <paramref name="isCodeLine"/> to ensure only code-region
        /// keywords trigger blank-line insertion.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="isCodeLine">Per-line flag indicating whether the
        /// line's first non-whitespace character is in a code region.
        /// </param>
        /// <returns>The processed line list.</returns>
        public static List<string> ApplyBlankLineRules(List<string> lines,
            bool[] isCodeLine)
        {
            var nonBlank = new List<NonBlankEntry>(lines.Count);
            bool prevWasBlank = false;
            bool isFirst = true;

            for (int idx = 0; idx < lines.Count; idx++)
            {
                string line = lines[idx];
                if (line.Trim().Length == 0)
                {
                    prevWasBlank = true;
                    continue;
                }

                bool hadBlankAbove = !isFirst && prevWasBlank;
                nonBlank.Add(new NonBlankEntry(idx, hadBlankAbove, line));
                prevWasBlank = false;
                isFirst = false;
            }

            var result = new List<string>(nonBlank.Count);
            for (int i = 0; i < nonBlank.Count; i++)
            {
                NonBlankEntry entry = nonBlank[i];
                string line = entry.Line;
                string trimmed = line.Trim();
                int origIdx = entry.OriginalIndex;
                bool lineIsCode = origIdx < isCodeLine.Length &&
                    isCodeLine[origIdx];
                bool isBlockStart = lineIsCode &&
                    TextUtils.IsBlockStartLine(trimmed);
                bool wantBlankAbove = false;

                if (result.Count > 0)
                {
                    string prevTrimmed = result[result.Count - 1].Trim();
                    if (isBlockStart && prevTrimmed.Length > 0 &&
                        prevTrimmed != "{" &&
                        !TextUtils.EndsWithOpenBrace(prevTrimmed))
                    {
                        wantBlankAbove = true;
                    }

                    if (!wantBlankAbove &&
                        TextUtils.IsBlockEndLine(prevTrimmed) &&
                        trimmed.Length > 0 && trimmed != "}" &&
                        !trimmed.StartsWith("}"))
                    {
                        wantBlankAbove = true;
                    }

                    if (!wantBlankAbove &&
                        TextUtils.IsUsingDirective(trimmed) &&
                        TextUtils.IsUsingDirective(prevTrimmed) &&
                        entry.HadBlankAbove)
                    {
                        wantBlankAbove = true;
                    }
                }

                if (wantBlankAbove)
                {
                    result.Add(string.Empty);
                }

                result.Add(line);
            }

            return result;
        }

        /// <summary>
        /// Collapses 2 or more consecutive blank lines into 1.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <returns>The processed line list.</returns>
        public static List<string> CollapseBlankLines(List<string> lines)
        {
            var result = new List<string>(lines.Count);
            int blankRun = 0;

            foreach (var line in lines)
            {
                if (line.Trim().Length == 0)
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
                    result.Add(line);
                }
            }

            return result;
        }

        /// <summary>
        /// Strips trailing whitespace from each line.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <returns>The processed line list.</returns>
        public static List<string> TrimTrailingWhitespace(
            List<string> lines)
        {
            var result = new List<string>(lines.Count);

            foreach (var line in lines)
            {
                result.Add(line.TrimEnd());
            }

            return result;
        }
    }
}
