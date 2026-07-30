using System.Collections.Generic;

namespace GDScriptFormatter
{
    /// <summary>
    /// Post-processing passes applied to the line list after the main
    /// blank-line rules: collapse excessive blank runs, trim trailing
    /// whitespace, remove blank lines immediately before closing
    /// braces, and add blank lines after closing braces when the
    /// following line is at the same indent.
    /// </summary>
    public sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Collapses runs of 3 or more consecutive blank lines into 2
        /// (func/class context) or 1.
        /// </summary>
        /// <param name="lines">The lines to process.</param>
        /// <returns>The lines with blank runs collapsed.</returns>
        public List<string> CollapseBlankLines(List<string> lines)
        {
            var result = new List<string>(lines.Count);
            int blankRun = 0;

            foreach (var line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    blankRun++;

                    if (blankRun <= 2)
                    {
                        result.Add(string.Empty);
                    }
                }
                else
                {
                    if (blankRun > 2)
                    {
                        while (result.Count > 0 &&
                            result[result.Count - 1].Trim().Length == 0)
                        {
                            result.RemoveAt(result.Count - 1);
                        }

                        result.Add(string.Empty);

                        if (ShouldKeepTwoBlanks(line, result))
                        {
                            result.Add(string.Empty);
                        }
                    }

                    blankRun = 0;
                    result.Add(line);
                }
            }

            return result;
        }

        /// <summary>
        /// Determines whether to keep two blank lines (instead of one)
        /// when collapsing excessive blank lines above a func/class
        /// declaration or below one.
        /// </summary>
        /// <param name="currentLine">The current non-blank line.</param>
        /// <param name="result">The result list built so far.</param>
        /// <returns>True if two blank lines should be preserved.</returns>
        private static bool ShouldKeepTwoBlanks(string currentLine, List<string>
            result)
        {
            string trimmed = currentLine.Trim();

            if (DeclarationClassifier.Instance.IsFuncOrClassDecl(trimmed))
            {
                return true;
            }

            if (result.Count > 0)
            {
                string prevTrim = result[result.Count - 1].Trim();

                if (DeclarationClassifier.Instance.IsFuncOrClassDecl(prevTrim))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Trims trailing whitespace from each line.
        /// </summary>
        /// <param name="lines">The lines to process.</param>
        /// <returns>The lines with trailing whitespace removed.</returns>
        public List<string> TrimTrailingWhitespace(List<string> lines)
        {
            var result = new List<string>(lines.Count);

            foreach (var line in lines)
            {
                result.Add(line.TrimEnd());
            }

            return result;
        }

        /// <summary>
        /// Removes blank lines that immediately precede a closing
        /// brace '}' or ']' at the same or lower indent level. This
        /// cleans up trailing blank lines inside dictionary/array
        /// literals and similar constructs. Closing parentheses are
        /// excluded because they may close continuation contexts
        /// (e.g., lambda arguments) where blank lines should be
        /// preserved.
        /// </summary>
        /// <param name="lines">The lines to process.</param>
        /// <returns>The lines with trailing blank lines before closing braces removed.</returns>
        private static List<string> RemoveBlanksBeforeClosingBraces(List<string>
            lines)
        {
            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();

                if (trimmed.Length > 0 && (trimmed[0] == '}' || trimmed[0] ==
                    ']'))
                {
                    while (result.Count > 0 && result[result.Count -
                        1].Trim().Length == 0)
                    {
                        result.RemoveAt(result.Count - 1);
                    }
                }

                result.Add(lines[i]);
            }

            return result;
        }

        /// <summary>
        /// Adds a blank line after a closing brace '}' when the next
        /// non-blank line is at the same indent level and is not
        /// another closing brace. This ensures that block-assignments
        /// (e.g. dict literals) are visually separated from the next
        /// statement.
        /// </summary>
        /// <param name="lines">The lines to process.</param>
        /// <returns>The lines with blank lines added after closing braces.</returns>
        private static List<string> AddBlankAfterClosingBraces(List<string>
            lines)
        {
            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                result.Add(lines[i]);

                string trimmed = lines[i].Trim();

                if (trimmed.Length > 0 && trimmed[0] == '}' && i + 1 <
                    lines.Count)
                {
                    int nextIdx = i + 1;

                    int closeBraceIndent =
                        IndentationProcessor.Instance.LineIndentLevel(lines[i]);

                    while (nextIdx < lines.Count &&
                        lines[nextIdx].Trim().Length == 0)
                    {
                        nextIdx++;
                    }

                    if (nextIdx < lines.Count)
                    {
                        string nextTrimmed = lines[nextIdx].Trim();

                        int nextIndent =
                            IndentationProcessor.Instance.LineIndentLevel(lines[nextIdx]);

                        bool nextIsCloseBrace = nextTrimmed.Length > 0 &&
                            (nextTrimmed[0] == '}' || nextTrimmed[0] == ')' ||
                            nextTrimmed[0] == ']');

                        bool hasBlank = i + 1 < lines.Count && lines[i +
                            1].Trim().Length == 0;

                        if (!hasBlank && !nextIsCloseBrace &&
                            closeBraceIndent <= nextIndent)
                        {
                            if (closeBraceIndent > 0 || (nextTrimmed.Length >
                                0 &&
                                !DeclarationClassifier.Instance.IsFuncOrClassDecl(nextTrimmed) &&
                                !DeclarationClassifier.Instance.IsFileHeaderLine(nextTrimmed)))
                            {
                                result.Add(string.Empty);
                            }
                        }
                    }
                }
            }

            return result;
        }
    }
}
