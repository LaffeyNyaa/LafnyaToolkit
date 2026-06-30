using System.Collections.Generic;

namespace GDScriptFormatter
{
    /// <summary>
    /// Core implementation that applies all GDScript formatting rules.
    /// </summary>
    internal static class Formatter
    {
        /// <summary>
        /// Applies all formatting rules to a source string and returns the result. Line endings are
        /// normalized first, then tabs are normalized only in Code regions, then enums are expanded,
        /// and finally the tokenization is reused for re-indentation and line-length splitting.
        /// </summary>
        /// <param name="source">The original source string.</param>
        /// <returns>The formatted source string.</returns>
        public static string Format(string source)
        {
            if (source == null || source.Length == 0)
            {
                return source ?? string.Empty;
            }

            string text = source.Replace("\r\n", "\n").Replace("\r", "\n");
            text = MoveFileDocComments(text);
            text = TextUtils.NormalizeCommentSpaces(text);

            var tabTokens = Tokenizer.Tokenize(text);
            bool[] tabMask = Tokenizer.BuildCodeMask(text, tabTokens);
            text = TextUtils.NormalizeTabs(text, tabMask);

            text = EnumFormatter.ExpandEnums(text);

            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);

            var lines = TextUtils.SplitLines(text);
            lines = IndentationProcessor.Reindent(lines, text, tokens, isCode);
            // Compute continuation flags from the post-Reindent (pre-split)
            // line structure so that LineLengthProcessor can detect
            // continuation lines and avoid cascading indents when splitting
            // them (a continuation line split at parent+4 must keep its
            // segments at parent+4, not parent+8).
            string textForLimit = string.Join("\n", lines);
            var tokensForLimit = Tokenizer.Tokenize(textForLimit);

            bool[] isCodeForLimit = Tokenizer.BuildCodeMask(textForLimit,
                tokensForLimit);

            int[] lineStartsForLimit =
                IndentationProcessor.ComputeLineStarts(lines);

            var lineInfoForLimit = IndentationProcessor.ComputeLineInfo(lines,
                textForLimit,
                isCodeForLimit, lineStartsForLimit);

            var preSplitContinues = new bool[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                // Line i "continues to next" when line i+1 is detected as a
                // continuation (unclosed bracket from line i, or line i ends
                // with a continuation backslash).
                preSplitContinues[i] = i + 1 < lines.Count &&
                    lineInfoForLimit[i + 1].IsContinuation;
            }

            // Split long lines BEFORE applying blank-line rules so that the
            // preSplitContinues flags (computed above) stay aligned with the
            // line list. Running BlankLineProcessor first would insert blank
            // lines and shift indices, causing LineLengthProcessor to read
            // the wrong continuation flag for each line.
            lines = LineLengthProcessor.ApplyLineLengthLimit(lines,
                preSplitContinues);

            // Re-indent after line-length splitting so that the indentation
            // of newly introduced continuation lines and synthetic
            // parentheses matches the Reindent algorithm. Without this,
            // the first formatting pass produces different indentation than
            // the second pass (where Reindent processes the post-split
            // output), causing the formatter to be non-idempotent.
            {
                var postSplitTextForReindent = string.Join("\n", lines);

                var reindentTokens = Tokenizer.Tokenize(
                    postSplitTextForReindent);

                bool[] reindentIsCode = Tokenizer.BuildCodeMask(
                    postSplitTextForReindent, reindentTokens);

                lines = IndentationProcessor.Reindent(
                    lines, postSplitTextForReindent, reindentTokens,
                    reindentIsCode);
            }

            // Recompute continuation flags after line-length splitting so
            // that BlankLineProcessor can suppress blank lines between
            // continuation lines.
            var postSplitText = string.Join("\n", lines);
            var postSplitTokens = Tokenizer.Tokenize(postSplitText);

            bool[] postSplitIsCode = Tokenizer.BuildCodeMask(postSplitText,
                postSplitTokens);

            int[] postSplitLineStarts =
                IndentationProcessor.ComputeLineStarts(lines);

            var postSplitLineInfo =
                IndentationProcessor.ComputeLineInfo(lines, postSplitText,
                postSplitIsCode, postSplitLineStarts);

            var postSplitContinues = new bool[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                postSplitContinues[i] =
                    postSplitLineInfo[i].IsContinuation;
            }

            lines = BlankLineProcessor.ApplyBlankLineRules(lines,
                postSplitContinues);

            lines = BlankLineProcessor.CollapseBlankLines(lines);
            lines = BlankLineProcessor.TrimTrailingWhitespace(lines);
            string result = string.Join("\n", lines);
            result = TextUtils.EnsureSingleTrailingNewline(result);
            return result;
        }

        /// <summary>
        /// Moves leading ## doc-comment blocks from the top of the file to
        /// immediately after the last file header line (class_name, extends,
        /// @tool, @icon, @static_unload). Returns the text unchanged if no
        /// leading ## comments or no file headers are found.
        /// </summary>
        /// <param name="text">The line-ending-normalized text.</param>
        /// <returns>The text with leading ## doc comments moved after the
        /// last file header, or the original text if no move is needed.
        /// </returns>
        private static string MoveFileDocComments(string text)
        {
            var lines = TextUtils.SplitLines(text);

            var moveIndices = new SortedSet<int>();
            int lastDocLineIdx = -1;
            int scanEnd = lines.Count;

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();

                if (trimmed.StartsWith("##"))
                {
                    if (lastDocLineIdx >= 0)
                    {
                        for (int b = lastDocLineIdx + 1; b < i; b++)
                        {
                            moveIndices.Add(b);
                        }
                    }

                    moveIndices.Add(i);
                    lastDocLineIdx = i;
                }
                else if (trimmed.Length == 0)
                {
                }
                else
                {
                    scanEnd = i;
                    break;
                }
            }

            if (lastDocLineIdx < 0)
            {
                return text;
            }

            int lastFileHeaderIdx = -1;

            for (int j = scanEnd; j < lines.Count; j++)
            {
                string trimmed = lines[j].Trim();

                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (TextUtils.IsFileHeaderLine(trimmed) &&
                    !trimmed.StartsWith("##"))
                {
                    lastFileHeaderIdx = j;
                }
                else
                {
                    break;
                }
            }

            if (lastFileHeaderIdx < 0)
            {
                return text;
            }

            var newLines = new List<string>(lines.Count);

            for (int k = 0; k < lines.Count; k++)
            {
                if (moveIndices.Contains(k))
                {
                    continue;
                }

                newLines.Add(lines[k]);

                if (k == lastFileHeaderIdx)
                {
                    foreach (int idx in moveIndices)
                    {
                        newLines.Add(lines[idx]);
                    }
                }
            }

            return string.Join("\n", newLines);
        }
    }
}
