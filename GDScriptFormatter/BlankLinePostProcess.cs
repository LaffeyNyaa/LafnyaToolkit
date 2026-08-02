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
        /// brace <c>}</c> or <c>]</c> at the same or lower indent
        /// level, a closing parenthesis <c>)</c> that is
        /// immediately followed by a colon-terminated
        /// end-of-statement line (e.g. <c>):</c> closing an
        /// <c>if (...)</c> block) at the same or shallower indent,
        /// a <c>):</c> style end-of-statement line that closes
        /// a parenthesized expression and starts with a close
        /// bracket, or a <c>)</c> line whose immediately preceding
        /// non-blank line is also a close-bracket line at the
        /// same or deeper indent (so chained <c>)</c>
        /// continuations such as <c>).instantiate()</c> stay
        /// tight). This cleans up trailing blank lines inside
        /// dictionary/array literals and similar constructs,
        /// prevents spurious blank lines between an inner closing
        /// parenthesis and the outer <c>):</c> end-of-statement
        /// colon, and keeps chained close-paren continuations
        /// visually grouped. Closing parentheses that are not
        /// followed by an end-of-statement colon and not part of
        /// a close-bracket chain are left alone because they may
        /// close continuation contexts (e.g. lambda arguments)
        /// where blank lines should be preserved.
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

                bool isCloseBrace = trimmed.Length > 0 &&
                    (trimmed[0] == '}' || trimmed[0] == ']');

                bool isCloseParenBeforeEos = !isCloseBrace &&
                    IsCloseParenBeforeEndOfStatement(lines, i);

                bool isCloseParenEosLine = !isCloseBrace &&
                    !isCloseParenBeforeEos &&
                    IsCloseParenEndOfStatementLine(trimmed);

                bool isCloseParenInChain = !isCloseBrace &&
                    !isCloseParenBeforeEos && !isCloseParenEosLine &&
                    IsCloseParenInCloseChain(lines, i);

                if (isCloseBrace || isCloseParenBeforeEos ||
                    isCloseParenEosLine || isCloseParenInChain)
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
        /// Detects whether a trimmed line is an end-of-statement
        /// line that begins with a closing parenthesis
        /// (e.g. <c>):</c> closing a parenthesized block such as
        /// <c>if (...)</c>). Used to treat such lines like a close
        /// brace for the purpose of suppressing the blank line
        /// between an inner close paren and this end-of-statement
        /// line.
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>True if the line is a <c>):</c>-style end-of-statement line.</returns>
        private static bool IsCloseParenEndOfStatementLine(string trimmed)
        {
            if (trimmed.Length < 2 || trimmed[0] != ')')
            {
                return false;
            }

            return trimmed[trimmed.Length - 1] == ':';
        }

        /// <summary>
        /// Detects whether <paramref name="lines"/> at index
        /// <paramref name="i"/> is a <c>)</c> line whose next
        /// non-blank line ends with a colon and is at the same or
        /// shallower indent (e.g. <c>):</c> closing an
        /// <c>if (...)</c> guard). Used to suppress a trailing
        /// blank line between the close paren and the
        /// end-of-statement colon.
        /// </summary>
        /// <param name="lines">The lines to inspect.</param>
        /// <param name="i">The index of the candidate <c>)</c> line.</param>
        /// <returns>True if a blank line should be removed before this <c>)</c> line.</returns>
        private static bool IsCloseParenBeforeEndOfStatement(List<string>
            lines, int i)
        {
            string trimmed = lines[i].Trim();

            if (trimmed.Length == 0 || trimmed[0] != ')')
            {
                return false;
            }

            int nextIdx = i + 1;

            while (nextIdx < lines.Count &&
                lines[nextIdx].Trim().Length == 0)
            {
                nextIdx++;
            }

            if (nextIdx >= lines.Count)
            {
                return false;
            }

            string nextTrimmed = lines[nextIdx].Trim();

            if (nextTrimmed.Length == 0 ||
                nextTrimmed[nextTrimmed.Length - 1] != ':')
            {
                return false;
            }

            int curIndent =
                IndentationProcessor.Instance.LineIndentLevel(lines[i]);

            int nextIndent =
                IndentationProcessor.Instance.LineIndentLevel(lines[nextIdx]);

            return nextIndent <= curIndent;
        }

        /// <summary>
        /// Detects whether <paramref name="lines"/> at index
        /// <paramref name="i"/> is a <c>)</c> line whose
        /// immediately preceding non-blank line is also a close-
        /// bracket line at the same or deeper indent. Used to
        /// keep chained close-paren continuations such as
        /// <c>).instantiate()</c> followed by the wrapping
        /// <c>)</c> visually grouped.
        /// </summary>
        /// <param name="lines">The lines to inspect.</param>
        /// <param name="i">The index of the candidate <c>)</c> line.</param>
        /// <returns>True if a blank line should be removed before this <c>)</c> line.</returns>
        private static bool IsCloseParenInCloseChain(List<string> lines,
            int i)
        {
            string trimmed = lines[i].Trim();

            if (trimmed.Length == 0 || trimmed[0] != ')')
            {
                return false;
            }

            int prevIdx = i - 1;

            while (prevIdx >= 0 && lines[prevIdx].Trim().Length == 0)
            {
                prevIdx--;
            }

            if (prevIdx < 0)
            {
                return false;
            }

            string prevTrimmed = lines[prevIdx].Trim();

            if (prevTrimmed.Length == 0)
            {
                return false;
            }

            char prevFirst = prevTrimmed[0];

            if (prevFirst != ')' && prevFirst != '}' && prevFirst != ']')
            {
                return false;
            }

            int curIndent =
                IndentationProcessor.Instance.LineIndentLevel(lines[i]);

            int prevIndent =
                IndentationProcessor.Instance.LineIndentLevel(lines[prevIdx]);

            return prevIndent >= curIndent;
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
                                !DeclarationClassifier.Instance.IsFuncOrClassDecl(nextTrimmed)

                                &&
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
