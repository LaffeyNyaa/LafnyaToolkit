using System.Collections.Generic;

using static GDScriptFormatter.DeclarationClassifier;

namespace GDScriptFormatter
{
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Collapses runs of 3 or more consecutive blank lines into 2 (func/class context) or 1.
        /// </summary>
        internal static List<string> CollapseBlankLines(List<string> lines)
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
        /// Determines whether to keep two blank lines (instead of one) when
        /// collapsing excessive blank lines above a func/class declaration or
        /// below one.
        /// </summary>
        private static bool ShouldKeepTwoBlanks(string currentLine, List<string>
            result)
        {
            string trimmed = currentLine.Trim();

            if (IsFuncOrClassDecl(trimmed))
            {
                return true;
            }

            if (result.Count > 0)
            {
                string prevTrim = result[result.Count - 1].Trim();

                if (IsFuncOrClassDecl(prevTrim))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Trims trailing whitespace from each line.
        /// </summary>
        internal static List<string> TrimTrailingWhitespace(List<string> lines)
        {
            var result = new List<string>(lines.Count);

            foreach (var line in lines)
            {
                result.Add(line.TrimEnd());
            }

            return result;
        }

        /// <summary>
        /// Removes blank lines that immediately precede a closing brace '}',
        /// ')' or ']' at the same or lower indent level. This cleans up
        /// trailing blank lines inside dictionary literals and similar constructs.
        /// </summary>
        private static List<string> RemoveBlanksBeforeClosingBraces(List<string>
            lines)
        {
            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();
                // Check if this line is a closing brace/bracket

                if (trimmed.Length > 0 && (trimmed[0] == '}' || trimmed[0] ==
                    ')' || trimmed[0] == ']'))
                {
                    // Remove any blank lines just before this closing brace

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
        /// Adds a blank line after a closing brace '}' when the next non-blank
        /// line is at the same indent level and is not another closing brace.
        /// This ensures that block-assignments (e.g. dict literals) are visually
        /// separated from the next statement.
        /// </summary>
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
                    // Look ahead: if the next non-blank line is at the same indent
                    // and is not a closing brace, add a blank line
                    int nextIdx = i + 1;

                    int closeBraceIndent =
                        IndentationProcessor.LineIndentLevel(lines[i]);

                    // Skip existing blank lines

                    while (nextIdx < lines.Count &&
                        lines[nextIdx].Trim().Length == 0)
                    {
                        nextIdx++;
                    }

                    if (nextIdx < lines.Count)
                    {
                        string nextTrimmed = lines[nextIdx].Trim();

                        int nextIndent =
                            IndentationProcessor.LineIndentLevel(lines[nextIdx]);

                        bool nextIsCloseBrace = nextTrimmed.Length > 0 &&
                            (nextTrimmed[0] == '}' || nextTrimmed[0] == ')' ||
                            nextTrimmed[0] == ']');

                        // Only add blank if:
                        // - Next line is at same or shallower indent (not inside the brace block)
                        // - Next line is not itself a closing brace
                        // - There isn't already a blank line
                        bool hasBlank = i + 1 < lines.Count && lines[i +
                            1].Trim().Length == 0;

                        if (!hasBlank && !nextIsCloseBrace &&
                            closeBraceIndent <= nextIndent)
                        {
                            // Check if this is a top-level closing brace (enum/class body) — skip those

                            if (closeBraceIndent > 0 || (nextTrimmed.Length >
                                0 &&
                                !DeclarationClassifier.IsFuncOrClassDecl(nextTrimmed) &&
                                !DeclarationClassifier.IsFileHeaderLine(nextTrimmed)))
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
