using System.Collections.Generic;

using static GDScriptFormatter.DeclarationClassifier;
using static GDScriptFormatter.MemberClassifier;

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
            int currentBlanksAbove = 0;

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
                currentBlanksAbove = 0;
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
            // Guard clauses

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

            int want = ApplyFuncClassBlankRule(prevTrimmed, curTrimmed,
                sameIndent);

            if (want == 0)
            {
                want = ApplyBlockStartBlankRule(prevTrimmed, curTrimmed,
                    sameIndent, deeperThanPrev);
            }

            if (want == 0)
            {
                want = ApplyTopLevelMemberBlankRule(prevTrimmed, curTrimmed,
                    sameIndent);
            }

            if (want == 0)
            {
                want = ApplyFileHeaderBlankRule(prevTrimmed, curTrimmed,
                    deeperThanPrev);
            }

            if (want == 0)
            {
                want = ApplyDocCommentBlankRule(prevTrimmed, curTrimmed,
                    nonBlank, curIdx);
            }

            if (want == 0)
            {
                want = ApplyDedentBlankRule(curIndent, prevIndent);
            }

            if (want == 0)
            {
                want = ApplyPreserveAuthorBlankRule(nonBlank[curIdx],
                    prevTrimmed, curTrimmed);
            }

            if (want == 0)
            {
                want = ApplyMultiLineStatementBlankRule(nonBlank, curIdx,
                    curIndent, prevIndent);
            }

            // Annotation suppression override (checked last so it can
            // override previous rules)

            if (ApplyAnnotationSuppressRule(prevTrimmed, curTrimmed) != 0)
            {
                return 0;
            }

            return want;
        }

        /// <summary>
        /// Returns 2 blank lines when the current line is a func/class declaration,
        /// or when the previous line was a func/class declaration at the same indent level.
        /// </summary>
        private static int ApplyFuncClassBlankRule(string prevTrimmed,
            string curTrimmed, bool sameIndent)
        {
            if (IsFuncOrClassDecl(curTrimmed))
            {
                return 2;
            }

            if (sameIndent && IsFuncOrClassDecl(prevTrimmed) &&
                !IsFuncOrClassDecl(curTrimmed))
            {
                return 2;
            }

            return 0;
        }

        /// <summary>
        /// Returns 1 blank line when the current line starts a block and is not
        /// in the same group as the previous line, or when entering a block from
        /// a non-block line.
        /// </summary>
        private static int ApplyBlockStartBlankRule(string prevTrimmed,
            string curTrimmed, bool sameIndent, bool deeperThanPrev)
        {
            if (TextUtils.IsBlockStartLine(curTrimmed) &&
                !IsSameGroup(prevTrimmed, curTrimmed) && sameIndent)
            {
                return 1;
            }

            if (TextUtils.IsBlockStartLine(curTrimmed) &&
                !deeperThanPrev &&
                prevTrimmed.Length > 0 && prevTrimmed != ":" &&
                !TextUtils.EndsWithColon(prevTrimmed))
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Returns 1 blank line between different groups of top-level members
        /// (signals, enums, consts, vars, etc.) at the same indent level.
        /// </summary>
        private static int ApplyTopLevelMemberBlankRule(string prevTrimmed,
            string curTrimmed, bool sameIndent)
        {
            if (sameIndent &&
                IsTopLevelMember(prevTrimmed) &&
                IsTopLevelMember(curTrimmed) &&
                !IsSameGroup(prevTrimmed, curTrimmed))
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Returns 1 blank line after a file-level header line when the
        /// current line is not itself a header and is not entering a deeper block.
        /// </summary>
        private static int ApplyFileHeaderBlankRule(string prevTrimmed,
            string curTrimmed, bool deeperThanPrev)
        {
            if (IsFileHeaderLine(prevTrimmed) &&
                !IsFileHeaderLine(curTrimmed) && !deeperThanPrev)
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Returns 1 (or 2 if the doc comment is attached to a func/class) blank line
        /// before a doc comment block. No blank line is added when the previous line
        /// is already a comment, an opening brace, or a file header.
        /// </summary>
        private static int ApplyDocCommentBlankRule(string prevTrimmed,
            string curTrimmed, List<NonBlankEntry> nonBlank, int curIdx)
        {
            if (!curTrimmed.StartsWith("##"))
            {
                return 0;
            }

            bool prevIsDocComment = prevTrimmed.StartsWith("##");

            bool prevIsRegularComment = prevTrimmed.StartsWith("#") &&
                !prevIsDocComment;

            bool prevIsBlockOpenBrace = prevTrimmed == "{" ||
                prevTrimmed.EndsWith("{");

            bool prevIsFileHeader = IsFileHeaderLine(prevTrimmed);

            if (prevTrimmed.Length > 0 && !prevIsDocComment &&
                !prevIsRegularComment && !prevIsBlockOpenBrace &&
                !prevIsFileHeader)
            {
                return IsDocCommentAttachedToFuncOrClass(
                    nonBlank, curIdx) ? 2 : 1;
            }

            // If the previous line is a ## doc comment but the current ##
            // line had a blank line above it in the original, they belong to
            // separate doc comment blocks. Insert the appropriate spacing.

            if (prevIsDocComment && nonBlank[curIdx].HadBlankAbove)
            {
                return IsDocCommentAttachedToFuncOrClass(
                    nonBlank, curIdx) ? 2 : 1;
            }

            return 0;
        }

        /// <summary>
        /// Returns 1 blank line when the current line is at a shallower indent
        /// than the previous line (i.e. we just exited a code block).
        /// </summary>
        private static int ApplyDedentBlankRule(int curIndent, int prevIndent)
        {
            if (curIndent < prevIndent)
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Preserves author-inserted blank lines between adjacent plain
        /// single-line statements at the same indent. Only preserves an
        /// existing blank (HadBlankAbove); never adds one.
        /// </summary>
        private static int ApplyPreserveAuthorBlankRule(NonBlankEntry curEntry,
            string prevTrimmed, string curTrimmed)
        {
            if (curEntry.HadBlankAbove &&
                IsPlainSingleLineStatement(prevTrimmed) &&
                IsPlainSingleLineStatement(curTrimmed))
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Returns 1 blank line around multi-line statements: when the previous
        /// non-blank line was a continuation and the current line is not (unless
        /// entering a deeper block or the continuation is a block header), or
        /// when a following line is a continuation.
        /// </summary>
        private static int ApplyMultiLineStatementBlankRule(List<NonBlankEntry>
            nonBlank, int curIdx, int curIndent, int prevIndent)
        {
            // Multi-line statement below: if the immediate previous
            // non-blank line was a continuation and the current line is
            // not, add a blank line (unless entering a deeper block,
            // e.g. a multi-line if-condition followed by its body).
            // Also skip when the continuation line itself is a block
            // start (ends with colon) — that is an if/for/while header
            // whose body should stay attached.

            if (curIdx > 0 &&
                nonBlank[curIdx - 1].IsContinuation &&
                !nonBlank[curIdx].IsContinuation &&
                curIndent <= prevIndent &&
                !TextUtils.IsBlockStartLine(
                nonBlank[curIdx - 1].Line.Trim()))
            {
                return 1;
            }

            // Multi-line statement above: if the next non-blank line is a
            // continuation and the current line is not, add a blank line
            // before the multi-line statement begins. Only applies when
            // the current line is a peer of the previous line (same
            // indent) — not when entering a new block.

            if (curIdx + 1 < nonBlank.Count &&
                nonBlank[curIdx + 1].IsContinuation &&
                !nonBlank[curIdx].IsContinuation &&
                prevIndent == curIndent)
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// When an annotation line (starting with @) immediately precedes a
        /// declaration line (func, class, var, signal, const, enum, etc.),
        /// returns non-zero to suppress blank lines between them. The
        /// annotation belongs to the declaration and should be directly
        /// adjacent.
        /// </summary>
        private static int ApplyAnnotationSuppressRule(string prevTrimmed,
            string curTrimmed)
        {
            if (prevTrimmed.StartsWith("@") &&
                IsDeclarationLine(curTrimmed))
            {
                return 1;
            }

            return 0;
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

            if (IsFuncOrClassDecl(trimmed))
            {
                return false;
            }

            if (IsFileHeaderLine(trimmed))
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

            if (!IsDeclarationLine(curTrimmed))
            {
                return false;
            }

            // Doc comments (##) are force-attached unless they're file-level.

            if (prevTrimmed.StartsWith("##"))
            {
                return !IsFileLevelDocComment(nonBlank, curIdx);
            }

            // Single-# comments are attached only when no blank line
            // originally separated them.
            return !nonBlank[curIdx].HadBlankAbove;
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
                    return IsFileHeaderLine(trimmed);
                }

                // If this ## line had a blank line above it in the
                // original, it marks the start of a new doc comment
                // block. Since it is separated from any file headers
                // by another ## block, this block is NOT file-level
                // (it is a member-level doc comment).

                if (nonBlank[j].HadBlankAbove)
                {
                    return false;
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
                    return IsFuncOrClassDecl(trimmed);
                }
            }

            return false;
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
    }
}
