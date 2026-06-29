using System.Collections.Generic;

namespace GDScriptFormatter
{
    /// <summary>
    /// Applies blank-line rules: ensures the correct number of blank lines
    /// around blocks and declarations, collapses excess blank lines, and
    /// trims trailing whitespace.
    /// </summary>
    internal static class BlankLineProcessor
    {
        /// <summary>
        /// Non-blank line entry: records whether there was a blank line above in the original and the indentation level.
        /// </summary>
        private struct NonBlankEntry
        {
            /// <summary>Whether a blank line existed above this line in the original input.</summary>
            public bool HadBlankAbove;

            /// <summary>The line text.</summary>
            public string Line;

            /// <summary>The indentation level.</summary>
            public int Indent;
            public NonBlankEntry(bool hadBlankAbove, string line, int indent)
            {
                HadBlankAbove = hadBlankAbove;
                Line = line;
                Indent = indent;
            }
        }

        /// <summary>
        /// Ensures the correct number of blank lines above and below blocks/declarations, including:
        /// - one blank line above and below code blocks and multi-line statements
        /// - two blank lines above and below func/nested class declarations (only at the same indentation depth)
        /// - one blank line after file-level header lines
        /// - one blank line between different variable groups
        /// - comments attached to the following declaration
        /// </summary>
        internal static List<string> ApplyBlankLineRules(List<string> lines)
        {
            var nonBlank = new List<NonBlankEntry>(lines.Count);
            bool prevWasBlank = false;
            bool isFirst = true;

            foreach (var line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    prevWasBlank = true;
                    continue;
                }

                bool hadBlankAbove = !isFirst && prevWasBlank;
                int indent = IndentationProcessor.LineIndentLevel(line);
                nonBlank.Add(new NonBlankEntry(hadBlankAbove, line, indent));
                prevWasBlank = false;
                isFirst = false;
            }

            var result = new List<string>(nonBlank.Count);
            var resultIndents = new List<int>(nonBlank.Count);

            for (int i = 0; i < nonBlank.Count; i++)
            {
                string line = nonBlank[i].Line;
                string trimmed = line.Trim();
                int lineIndent = nonBlank[i].Indent;
                int wantBlankAbove = 0;

                if (result.Count > 0)
                {
                    string prevTrimmed = result[result.Count - 1].Trim();
                    int prevIndent = resultIndents[resultIndents.Count - 1];

                    wantBlankAbove = ComputeDesiredBlanksAbove(
                        prevTrimmed, trimmed, nonBlank, i,
                        prevIndent, lineIndent);
                }

                int currentBlanksAbove = CountTrailingBlanks(result);

                while (currentBlanksAbove < wantBlankAbove)
                {
                    result.Add(string.Empty);
                    resultIndents.Add(-1);
                    currentBlanksAbove++;
                }

                while (currentBlanksAbove > wantBlankAbove)
                {
                    result.RemoveAt(result.Count - 1);
                    resultIndents.RemoveAt(resultIndents.Count - 1);
                    currentBlanksAbove--;
                }

                result.Add(line);
                resultIndents.Add(lineIndent);
            }

            return result;
        }

        /// <summary>
        /// Computes how many blank lines should appear above the current line.
        /// </summary>
        private static int ComputeDesiredBlanksAbove(string prevTrimmed,
            string curTrimmed, List<NonBlankEntry> nonBlank, int curIdx,
            int prevIndent, int curIndent)
        {
            if (curTrimmed.Length == 0)
            {
                return 0;
            }

            if (prevTrimmed.Length == 0)
            {
                return 0;
            }

            if (IsAttachedComment(prevTrimmed, curTrimmed, nonBlank, curIdx))
            {
                return 0;
            }

            bool sameIndent = prevIndent == curIndent;
            bool deeperThanPrev = curIndent > prevIndent;
            int want = 0;

            if (sameIndent && TextUtils.IsFuncOrClassDecl(curTrimmed))
            {
                want = 2;
            }

            else if (sameIndent && TextUtils.IsFuncOrClassDecl(prevTrimmed) &&
                !TextUtils.IsFuncOrClassDecl(curTrimmed))
            {
                want = 2;
            }

            else if (TextUtils.IsBlockStartLine(curTrimmed) &&
                !TextUtils.IsSameGroup(
                prevTrimmed, curTrimmed) && sameIndent)
            {
                want = 1;
            }

            else if (TextUtils.IsBlockStartLine(curTrimmed) &&
                !deeperThanPrev &&
                prevTrimmed.Length > 0 && prevTrimmed != ":" &&
                !TextUtils.EndsWithColon(prevTrimmed))
            {
                want = 1;
            }

            if (want == 0 && sameIndent &&
                TextUtils.IsTopLevelMember(prevTrimmed) &&
                TextUtils.IsTopLevelMember(curTrimmed) &&
                !TextUtils.IsSameGroup(prevTrimmed, curTrimmed))
            {
                want = 1;
            }

            if (want == 0 && TextUtils.IsFileHeaderLine(prevTrimmed) &&
                !TextUtils.IsFileHeaderLine(curTrimmed) && !deeperThanPrev)
            {
                want = 1;
            }

            if (want == 0 && curTrimmed.StartsWith("##"))
            {
                bool prevIsDocComment = prevTrimmed.StartsWith("##");

                bool prevIsRegularComment = prevTrimmed.StartsWith("#") &&
                    !prevIsDocComment;

                bool prevIsBlockOpenBrace = prevTrimmed == "{" ||
                    prevTrimmed.EndsWith("{");

                bool prevIsFileHeader = TextUtils.IsFileHeaderLine(prevTrimmed);

                if (prevTrimmed.Length > 0 && !prevIsDocComment &&
                    !prevIsRegularComment && !prevIsBlockOpenBrace &&
                    !prevIsFileHeader)
                {
                    want = 1;
                }
            }

            // Preserve author-inserted blank lines between adjacent
            // single-line statements at the same indent. Only PRESERVES an
            // existing blank (HadBlankAbove); never adds one. Prevents the
            // "align downward" logic from stripping the author's blank.

            if (want == 0 && nonBlank[curIdx].HadBlankAbove &&
                prevIndent == curIndent &&
                IsPlainSingleLineStatement(prevTrimmed) &&
                IsPlainSingleLineStatement(curTrimmed))
            {
                want = 1;
            }

            return want;
        }

        /// <summary>
        /// Determines whether a trimmed line is a plain single-line GDScript
        /// statement: non-empty, not a comment, not a block-start (lines ending
        /// with ":"), not a file header, and not an annotation line ("@...").
        /// Note: var/const/signal/enum declarations are treated as plain
        /// single-line statements so that author-inserted blank lines between
        /// them are preserved (consistent with C# field initializers).
        /// func/class declarations end with ":" and are excluded as block-starts;
        /// class_name/extends are excluded as file headers.
        /// </summary>
        private static bool IsPlainSingleLineStatement(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed.StartsWith("#"))
            {
                return false;
            }

            if (TextUtils.IsBlockStartLine(trimmed))
            {
                return false;
            }

            if (TextUtils.IsFileHeaderLine(trimmed))
            {
                return false;
            }

            if (trimmed.StartsWith("@"))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether a preceding comment line is attached to the current declaration.
        /// Doc comment lines (starting with ##) are always force-attached to a following declaration
        /// regardless of whether a blank line originally separated them. Single-# comments are
        /// attached only when no blank line originally separated them.
        /// </summary>
        private static bool IsAttachedComment(string prevTrimmed,
            string curTrimmed, List<NonBlankEntry> nonBlank, int curIdx)
        {
            if (!prevTrimmed.StartsWith("#"))
            {
                return false;
            }

            if (!TextUtils.IsDeclarationLine(curTrimmed))
            {
                return false;
            }

            if (prevTrimmed.StartsWith("##"))
            {
                return true;
            }

            if (!nonBlank[curIdx].HadBlankAbove)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Counts the number of consecutive blank lines at the end of result.
        /// </summary>
        private static int CountTrailingBlanks(List<string> result)
        {
            int count = 0;

            for (int j = result.Count - 1; j >= 0; j--)
            {
                if (result[j].Trim().Length == 0)
                {
                    count++;
                }

                else
                {
                    break;
                }
            }

            return count;
        }

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

                        string trimmed = line.Trim();

                        bool nearFuncClass =
                            TextUtils.IsFuncOrClassDecl(trimmed);

                        if (!nearFuncClass && result.Count > 0)
                        {
                            string prevTrim = result[result.Count - 1].Trim();

                            if (TextUtils.IsFuncOrClassDecl(prevTrim))
                            {
                                nearFuncClass = true;
                            }
                        }

                        result.Add(string.Empty);

                        if (nearFuncClass)
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
    }
}
