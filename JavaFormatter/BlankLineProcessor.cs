using System.Collections.Generic;

namespace JavaFormatter
{
    /// <summary>
    /// Applies blank-line spacing rules and trims trailing whitespace.
    /// All keyword/brace detection is token-aware so that comment and string
    /// content is never mistaken for structural code.
    /// </summary>
    internal static class BlankLineProcessor
    {
        /// <summary>
        /// Ensures exactly one blank line above blocks, multi-line statements, and
        /// declarations (with exceptions for the beginning/end of file). Annotation
        /// lines (starting with @) do not get blank lines inserted above them.
        /// Consecutive import lines also do not get blank lines inserted between them
        /// unless they were already separated by a blank line.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <returns>The lines with blank-line rules applied.</returns>
        public static List<string> ApplyBlankLineRules(List<string> lines)
        {
            string text = string.Join("\n", lines);
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);

            var lineStarts = new int[lines.Count];
            int pos = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                lineStarts[i] = pos;
                pos += lines[i].Length + 1;
            }

            var nonBlank = new List<NonBlankEntry>(lines.Count);
            bool prevWasBlank = false;
            bool isFirst = true;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                if (line.Trim().Length == 0)
                {
                    prevWasBlank = true;
                    continue;
                }

                bool hadBlankAbove = !isFirst && prevWasBlank;
                nonBlank.Add(new NonBlankEntry(hadBlankAbove, line, i));
                prevWasBlank = false;
                isFirst = false;
            }

            var result = new List<string>(nonBlank.Count);

            for (int i = 0; i < nonBlank.Count; i++)
            {
                var entry = nonBlank[i];
                string line = entry.Line;
                int lineStart = lineStarts[entry.OriginalIndex];
                string trimmed = line.Trim();

                bool lineStartsInCode = FirstNonWsInCode(line, lineStart,
                    isCode);

                bool isBlockStart = lineStartsInCode &&
                    LineClassifier.IsBlockStartLine(trimmed);

                bool currentIsImport = lineStartsInCode &&
                    LineClassifier.IsImportDirective(trimmed);

                bool currentIsDoWhileTail = lineStartsInCode &&
                    LineClassifier.IsDoWhileTail(trimmed);

                bool currentIsBlockCont = lineStartsInCode &&
                    LineClassifier.IsBlockContinuation(trimmed);

                bool currentIsAnnotation = lineStartsInCode &&
                    trimmed.StartsWith("@");

                bool currentStartsWithCloseBrace = lineStartsInCode &&
                    trimmed.StartsWith("}");

                bool wantBlankAbove = false;

                if (i > 0)
                {
                    var prevEntry = nonBlank[i - 1];
                    string prevLine = prevEntry.Line;
                    int prevLineStart = lineStarts[prevEntry.OriginalIndex];
                    string prevTrimmed = prevLine.Trim();

                    bool prevStartsInCode = FirstNonWsInCode(prevLine,
                        prevLineStart, isCode);

                    bool prevEndsInCode = LastNonWsInCode(prevLine,
                        prevLineStart, isCode);

                    bool prevIsOpenBraceOnly = prevStartsInCode &&
                        prevTrimmed == "{";

                    bool prevEndsWithOpenBrace = prevEndsInCode &&
                        TextUtils.EndsWithOpenBrace(prevTrimmed);

                    bool prevIsBlockEnd = prevStartsInCode &&
                        LineClassifier.IsBlockEndLine(prevTrimmed);

                    bool prevIsImport = prevStartsInCode &&
                        LineClassifier.IsImportDirective(prevTrimmed);

                    bool prevIsPackage = prevStartsInCode &&
                        prevTrimmed.StartsWith("package ");

                    if (isBlockStart && prevTrimmed.Length > 0 &&
                        !prevIsOpenBraceOnly && !prevEndsWithOpenBrace)
                    {
                        wantBlankAbove = true;
                    }

                    if (!wantBlankAbove && prevIsBlockEnd &&
                        trimmed.Length > 0 && !currentStartsWithCloseBrace)
                    {
                        wantBlankAbove = true;
                    }

                    if (!wantBlankAbove && currentIsImport && prevIsImport &&
                        entry.HadBlankAbove)
                    {
                        wantBlankAbove = true;
                    }

                    if (!wantBlankAbove && currentIsImport && prevIsPackage)
                    {
                        wantBlankAbove = true;
                    }

                    bool currentIsDocCommentStart =
                        trimmed.StartsWith("/**");

                    if (!wantBlankAbove && currentIsDocCommentStart)
                    {
                        bool prevIsRegularComment =
                            prevTrimmed.StartsWith("//") ||
                            (prevTrimmed.StartsWith("/*") &&
                            !prevTrimmed.StartsWith("/**")) ||
                            (prevTrimmed.StartsWith("*") &&
                            !prevTrimmed.EndsWith("*/"));

                        bool prevIsBlockOpenBrace =
                            prevTrimmed == "{" ||
                            TextUtils.EndsWithOpenBrace(prevTrimmed);

                        if (prevTrimmed.Length > 0 &&
                            !prevIsRegularComment &&
                            !prevIsBlockOpenBrace)
                        {
                            wantBlankAbove = true;
                        }
                    }

                    if (!wantBlankAbove && entry.HadBlankAbove)
                    {
                        bool currentIsPlainStmt = IsPlainSingleLineStatement(
                            trimmed, lineStartsInCode);

                        bool prevIsPlainStmt = IsPlainSingleLineStatement(
                            prevTrimmed, prevStartsInCode);

                        if (currentIsPlainStmt && prevIsPlainStmt)
                        {
                            wantBlankAbove = true;
                        }
                    }
                }

                if (wantBlankAbove && currentIsAnnotation)
                {
                    wantBlankAbove = false;
                }

                if (wantBlankAbove && currentIsDoWhileTail)
                {
                    wantBlankAbove = false;
                }

                if (wantBlankAbove && currentIsBlockCont)
                {
                    wantBlankAbove = false;
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
        /// Collapses 3 or more consecutive blank lines into 1.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <returns>The lines with blank runs collapsed.</returns>
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
        /// Removes trailing whitespace from each line.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <returns>The lines with trailing whitespace removed.</returns>
        public static List<string> TrimTrailingWhitespace(List<string> lines)
        {
            var result = new List<string>(lines.Count);

            foreach (var line in lines)
            {
                result.Add(line.TrimEnd());
            }

            return result;
        }

        /// <summary>
        /// Determines whether the first non-whitespace character of the line is in
        /// a code region.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The line's start offset in the source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>True if the first non-whitespace character is code; otherwise false.</returns>
        private static bool FirstNonWsInCode(string line, int lineStart,
            bool[] isCode)
        {
            int i = 0;

            while (i < line.Length && char.IsWhiteSpace(line[i]))
            {
                i++;
            }

            if (i >= line.Length)
            {
                return false;
            }

            int p = lineStart + i;
            return p >= 0 && p < isCode.Length && isCode[p];
        }

        /// <summary>
        /// Determines whether the last non-whitespace character of the line is in
        /// a code region.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The line's start offset in the source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>True if the last non-whitespace character is code; otherwise false.</returns>
        private static bool LastNonWsInCode(string line, int lineStart,
            bool[] isCode)
        {
            int i = line.Length - 1;

            while (i >= 0 && char.IsWhiteSpace(line[i]))
            {
                i--;
            }

            if (i < 0)
            {
                return false;
            }

            int p = lineStart + i;
            return p >= 0 && p < isCode.Length && isCode[p];
        }

        /// <summary>
        /// Determines whether the trimmed line is a comment line: a line
        /// comment, a block comment start, or a block comment continuation.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a comment line; otherwise false.</returns>
        private static bool IsCommentLine(string trimmed)
        {
            return trimmed.StartsWith("//") || trimmed.StartsWith("/*") ||
                trimmed.StartsWith("*");
        }

        /// <summary>
        /// Determines whether the line is a plain single-line statement:
        /// code that ends with a semicolon and is not a block boundary,
        /// do-while tail, or comment.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <param name="startsInCode">Whether the line's first non-whitespace
        /// character is in a code region.</param>
        /// <returns>True if the line is a plain single-line statement;
        /// otherwise false.</returns>
        private static bool IsPlainSingleLineStatement(string trimmed,
            bool startsInCode)
        {
            if (!startsInCode)
            {
                return false;
            }

            if (trimmed.Length == 0 || !trimmed.EndsWith(";"))
            {
                return false;
            }

            if (LineClassifier.IsBlockEndLine(trimmed))
            {
                return false;
            }

            if (LineClassifier.IsBlockStartLine(trimmed))
            {
                return false;
            }

            if (LineClassifier.IsDoWhileTail(trimmed))
            {
                return false;
            }

            if (IsCommentLine(trimmed))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Records a non-blank line along with whether a blank line preceded it in
        /// the original input and its original line index.
        /// </summary>
        private struct NonBlankEntry
        {
            /// <summary>Whether a blank line preceded this line in the input.</summary>
            public bool HadBlankAbove;

            /// <summary>The line text.</summary>
            public string Line;

            /// <summary>The original index of the line in the input list.</summary>
            public int OriginalIndex;

            /// <summary>Creates a new entry.</summary>
            /// <param name="hadBlankAbove">Whether a blank line preceded it.</param>
            /// <param name="line">The line text.</param>
            /// <param name="originalIndex">The original line index.</param>
            public NonBlankEntry(bool hadBlankAbove, string line,
                int originalIndex)
            {
                HadBlankAbove = hadBlankAbove;
                Line = line;
                OriginalIndex = originalIndex;
            }
        }
    }
}
