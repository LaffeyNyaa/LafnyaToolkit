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

            /// <summary>Whether this line is a continuation of the previous line.</summary>
            public bool IsContinuation;

            public NonBlankEntry(bool hadBlankAbove, string line, int indent,
                bool isContinuation)
            {
                HadBlankAbove = hadBlankAbove;
                Line = line;
                Indent = indent;
                IsContinuation = isContinuation;
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
        internal static List<string> ApplyBlankLineRules(List<string> lines,
            bool[] isContinuation)
        {
            var nonBlank = new List<NonBlankEntry>(lines.Count);
            bool prevWasBlank = false;
            bool isFirst = true;
            int lineIdx = 0;

            foreach (var line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    prevWasBlank = true;
                    lineIdx++;
                    continue;
                }

                bool hadBlankAbove = !isFirst && prevWasBlank;
                int indent = IndentationProcessor.LineIndentLevel(line);

                bool cont = isContinuation != null &&
                    lineIdx < isContinuation.Length &&
                    isContinuation[lineIdx];

                nonBlank.Add(new NonBlankEntry(hadBlankAbove, line, indent,
                    cont));

                prevWasBlank = false;
                isFirst = false;
                lineIdx++;
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

            // If the current line is a continuation of the previous line
            // (unclosed bracket or backslash), never insert blank lines
            // between them.

            if (nonBlank[curIdx].IsContinuation)
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

            if (TextUtils.IsFuncOrClassDecl(curTrimmed))
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
                    want = IsDocCommentAttachedToFuncOrClass(
                        nonBlank, curIdx) ? 2 : 1;
                }
            }

            // When the current line is at a shallower indent than the
            // previous non-blank line, we just exited one or more code
            // blocks. Insert one blank line to satisfy the "one blank line
            // below code blocks" rule.

            if (want == 0 && curIndent < prevIndent)
            {
                want = 1;
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

            // Multi-line statement below: if the immediate previous
            // non-blank line was a continuation and the current line is
            // not, add a blank line (unless entering a deeper block,
            // e.g. a multi-line if-condition followed by its body).
            // Also skip when the continuation line itself is a block
            // start (ends with colon) — that is an if/for/while header
            // whose body should stay attached.

            if (want == 0 && curIdx > 0 &&
                nonBlank[curIdx - 1].IsContinuation &&
                !nonBlank[curIdx].IsContinuation &&
                curIndent <= prevIndent &&
                !TextUtils.IsBlockStartLine(
                nonBlank[curIdx - 1].Line.Trim()))
            {
                want = 1;
            }

            // Multi-line statement above: if the next non-blank line is a
            // continuation and the current line is not, add a blank line
            // before the multi-line statement begins. Only applies when
            // the current line is a peer of the previous line (same
            // indent) — not when entering a new block.

            if (want == 0 && curIdx + 1 < nonBlank.Count &&
                nonBlank[curIdx + 1].IsContinuation &&
                !nonBlank[curIdx].IsContinuation &&
                prevIndent == curIndent)
            {
                want = 1;
            }

            return want;
        }

        /// <summary>
        /// Determines whether a trimmed line is a plain single-line GDScript
        /// statement: non-empty, not a comment, not a block-start, not an
        /// annotation, not a func/class declaration, and not a file header.
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

            if (trimmed.StartsWith("@"))
            {
                return false;
            }

            if (TextUtils.IsFuncOrClassDecl(trimmed))
            {
                return false;
            }

            if (TextUtils.IsFileHeaderLine(trimmed))
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
                // Do not force-attach file-level doc comments.
                // A doc comment is file-level when its nearest preceding
                // non-doc-comment line is a file header (class_name,
                // extends, @tool, @icon, @static_unload).

                if (IsFileLevelDocComment(nonBlank, curIdx))
                {
                    return false;
                }

                return true;
            }

            if (!nonBlank[curIdx].HadBlankAbove)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the current doc-comment block (ending at curIdx-1)
        /// is a file-level doc comment. A doc comment is file-level when the
        /// nearest preceding non-doc-comment line is a file header.
        /// </summary>
        private static bool IsFileLevelDocComment(
            List<NonBlankEntry> nonBlank, int curIdx)
        {
            // Scan backwards from the line just before curIdx (which is
            // the last line of the doc-comment block) to find the first
            // non-doc-comment line.

            for (int j = curIdx - 1; j >= 0; j--)
            {
                string trimmed = nonBlank[j].Line.Trim();

                if (!trimmed.StartsWith("##"))
                {
                    return TextUtils.IsFileHeaderLine(trimmed);
                }
            }

            // Entire file up to curIdx consists of doc comments
            // (no file header found). Treat as file-level.
            return true;
        }

        /// <summary>
        /// Determines whether the doc-comment block starting at startIdx is attached
        /// to a func or class declaration (looking ahead past consecutive ## lines).
        /// </summary>
        private static bool IsDocCommentAttachedToFuncOrClass(
            List<NonBlankEntry> nonBlank, int startIdx)
        {
            for (int i = startIdx + 1; i < nonBlank.Count; i++)
            {
                string trimmed = nonBlank[i].Line.Trim();

                if (!trimmed.StartsWith("##"))
                {
                    return TextUtils.IsFuncOrClassDecl(trimmed);
                }
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
