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

            // Post-processing: remove blank lines immediately before closing braces
            result = RemoveBlanksBeforeClosingBraces(result);
            // Post-processing: add blank lines after closing braces when followed by a statement at same indent
            result = AddBlankAfterClosingBraces(result);

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
                    sameIndent, nonBlank, curIdx);
            }

            if (want == 0)
            {
                want = ApplySetterGetterBlockRule(prevTrimmed, curTrimmed,
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

            // elif/else suppression: never insert blank lines before elif
            // or else — they are continuations of the preceding if/elif
            // block and should remain adjacent.

            if (IsElifOrElseBlock(curTrimmed))
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
        /// Also handles standalone annotation lines by resolving their member
        /// group from the following declaration line.
        /// When both lines belong to the same group but one is a bare declaration
        /// and the other is an annotated declaration (via standalone annotation),
        /// a blank line is also inserted to visually separate the two blocks.
        /// </summary>
        private static int ApplyTopLevelMemberBlankRule(string prevTrimmed,
            string curTrimmed, bool sameIndent, List<NonBlankEntry> nonBlank,
            int curIdx)
        {
            if (!sameIndent)
            {
                return 0;
            }

            MemberGroup prevGroup = (MemberGroup)(-1);
            MemberGroup curGroup = (MemberGroup)(-1);

            if (IsTopLevelMember(prevTrimmed))
            {
                prevGroup = ClassifyMember(prevTrimmed);
            }
            else if (IsStandaloneAnnotation(prevTrimmed))
            {
                prevGroup = ResolveAnnotationGroup(prevTrimmed, nonBlank,
                    curIdx - 1);
            }

            if (IsTopLevelMember(curTrimmed))
            {
                curGroup = ClassifyMember(curTrimmed);
            }
            else if (IsStandaloneAnnotation(curTrimmed))
            {
                curGroup = ResolveAnnotationGroup(curTrimmed, nonBlank, curIdx);
            }

            if (prevGroup != (MemberGroup)(-1) && curGroup !=
                (MemberGroup)(-1) && prevGroup != curGroup)
            {
                return 1;
            }

            // Same group: add blank if transitioning between bare declaration
            // and annotated declaration (one has a standalone annotation,
            // the other doesn't).

            if (prevGroup == curGroup && prevGroup != (MemberGroup)(-1))
            {
                bool prevIsBare = IsTopLevelMember(prevTrimmed) &&
                    !IsStandaloneAnnotation(prevTrimmed);

                bool curIsBare = IsTopLevelMember(curTrimmed) &&
                    !IsStandaloneAnnotation(curTrimmed);

                bool prevIsAnnotated = IsStandaloneAnnotation(prevTrimmed);
                bool curIsAnnotated = IsStandaloneAnnotation(curTrimmed);

                if ((prevIsBare && curIsAnnotated) ||
                    (prevIsAnnotated && curIsBare))
                {
                    return 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// For a standalone annotation line, resolves the member group by looking ahead
        /// in the nonBlank list to find the next declaration line. This lets annotation
        /// lines inherit the group of their following declaration (e.g., @warning_ignore
        /// + signal → signal group).
        /// </summary>
        private static MemberGroup ResolveAnnotationGroup(string trimmed, List<
            NonBlankEntry> nonBlank, int curIdx)
        {
            if (!IsStandaloneAnnotation(trimmed))
            {
                return (MemberGroup)(-1);
            }

            // Look ahead for the next declaration line

            for (int i = curIdx + 1; i < nonBlank.Count; i++)
            {
                string nextTrimmed = nonBlank[i].Line.Trim();

                if (IsDeclarationLine(nextTrimmed))
                {
                    return ClassifyMember(nextTrimmed);
                }

                // Stop if we encounter another non-annotation, non-blank line

                if (!nextTrimmed.StartsWith("@"))
                {
                    break;
                }
            }

            return (MemberGroup)(-1);
        }

        /// <summary>
        /// Returns 1 blank line when a var declaration ends with a colon (indicating
        /// a setter/getter block), even if it belongs to the same member group as the
        /// previous line. This ensures that properties with setters/getters are always
        /// visually separated from adjacent members.
        /// </summary>
        private static int ApplySetterGetterBlockRule(string prevTrimmed,
            string curTrimmed, bool sameIndent)
        {
            if (!sameIndent)
            {
                return 0;
            }

            // Current line is a block-start var declaration (has setter/getter)

            if (IsBlockStartVar(curTrimmed))
            {
                return 1;
            }

            // Previous line is a block-start var declaration

            if (IsBlockStartVar(prevTrimmed))
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Determines whether a trimmed line is a variable declaration that starts a
        /// setter/getter block (ends with a colon).
        /// </summary>
        private static bool IsBlockStartVar(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            // Must be a var/export declaration that ends with ':'

            if (!TextUtils.EndsWithColon(trimmed))
            {
                return false;
            }

            // Check if it's a var declaration (possibly with @export/@onready prefix)
            MemberGroup memberType = ClassifyMember(trimmed);

            if (memberType == MemberGroup.Export || memberType ==
                MemberGroup.RegularVar || memberType == MemberGroup.Onready ||
                memberType == MemberGroup.Private)
            {
                return true;
            }

            // Also handle explicit "var" keyword with colon (e.g. "var x:" as type annotation)

            if (TextUtils.StartsWithKeyword(trimmed, "var"))
            {
                return true;
            }

            return false;
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
        /// entering a deeper block or the continuation is a block header), when
        /// a following line is a continuation, or when the current line ends with
        /// an opening brace and the next line is indented deeper (brace-terminated
        /// multi-line construct).
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

            // Brace-terminated multi-line: when the current line ends with
            // an opening brace '{' (e.g., a dictionary literal) and the next
            // non-blank line is indented deeper, insert a blank line above.
            // This handles cases where the brace is consumed by
            // IndentationProcessor (marked as BraceTerminated) so the
            // continuation mechanism does not apply.

            if (curIdx + 1 < nonBlank.Count &&
                !nonBlank[curIdx].IsContinuation &&
                nonBlank[curIdx].Line.Trim().EndsWith("{") &&
                nonBlank[curIdx + 1].Indent > curIndent &&
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
            if (IsStandaloneAnnotation(prevTrimmed) &&
                IsDeclarationLine(curTrimmed))
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Determines whether a trimmed line is an elif or else block start.
        /// These are continuations of the preceding if/elif block and should
        /// not have blank lines inserted before them.
        /// </summary>
        private static bool IsElifOrElseBlock(string trimmed)
        {
            return TextUtils.StartsWithKeyword(trimmed, "elif") ||
                TextUtils.StartsWithKeyword(trimmed, "else");
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
        /// Determines whether a trimmed line is a standalone annotation line:
        /// starts with @ but does NOT contain a declaration keyword (var, func, signal,
        /// const, enum, class, static) on the same line.
        /// For example, "@warning_ignore("unused_signal")" is standalone,
        /// "@export_storage var x := 0" is NOT standalone (it has "var").
        /// </summary>
        private static bool IsStandaloneAnnotation(string trimmed)
        {
            if (!trimmed.StartsWith("@"))
            {
                return false;
            }

            // Find the first space after the annotation prefix
            int spaceIdx = trimmed.IndexOf(' ');

            if (spaceIdx < 0)
            {
                return true; // Just @something without any keyword
            }

            string rest = trimmed.Substring(spaceIdx + 1).TrimStart();
            // If after the @ annotation there's a declaration keyword, it's combined
            return !TextUtils.StartsWithKeyword(rest, "var") &&
                !TextUtils.StartsWithKeyword(rest, "func") &&
                !TextUtils.StartsWithKeyword(rest, "signal") &&
                !TextUtils.StartsWithKeyword(rest, "const") &&
                !TextUtils.StartsWithKeyword(rest, "enum") &&
                !TextUtils.StartsWithKeyword(rest, "class") &&
                !TextUtils.StartsWithKeyword(rest, "static");
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
                // original, it may mark the start of a new doc comment
                // block. Only treat it as non-file-level when the
                // blank separates two ## blocks; if it separates a
                // file header from this ##, the doc comment is still
                // file-level.

                if (nonBlank[j].HadBlankAbove)
                {
                    if (j > 0 &&
                        nonBlank[j - 1].Line.Trim().StartsWith("##"))
                    {
                        return false;
                    }

                    // Blank above was from a file header — this
                    // doc comment is still file-level.
                    continue;
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
