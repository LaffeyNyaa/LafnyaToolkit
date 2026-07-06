using System;
using System.Collections.Generic;

namespace GDScriptFormatter
{
    /// <summary>
    /// Core implementation that applies all GDScript formatting rules.
    /// </summary>
    public static class Formatter
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
            text = DocCommentMover.MoveFileDocComments(text);
            text = TextUtils.NormalizeCommentSpaces(text);

            bool[] tabMask = ComputeTokensAndMask(text, out var tabTokens);
            text = TextUtils.NormalizeTabs(text, tabMask);
            text = MemberReorderer.ReorderMembers(text);

            text = EnumFormatter.ExpandEnums(text);

            bool[] isCode = ComputeTokensAndMask(text, out var tokens);

            var lines = TextUtils.SplitLines(text);
            lines = IndentationProcessor.Reindent(lines, text, tokens, isCode);
            // Compute continuation flags from the post-Reindent (pre-split)
            // line structure so that LineLengthProcessor can detect
            // continuation lines and avoid cascading indents when splitting
            // them (a continuation line split at parent+4 must keep its
            // segments at parent+4, not parent+8).
            string textForLimit = string.Join("\n", lines);

            bool[] isCodeForLimit = ComputeTokensAndMask(textForLimit,
                out var tokensForLimit);

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

            // Collapse unnecessary wrapping parentheses that were introduced
            // by an earlier formatter pass (e.g. wrapping a method call chain
            // in (...) and splitting each segment onto its own line). This
            // restores the expression to a single long line that the
            // line-length splitter below can then format properly.
            CollapseWrappedExpressions(lines);
            // Recompute continuation flags after CollapseWrappedExpressions,
            // which may have changed the line count/structure so that the
            // pre-existing flags are no longer aligned.
            {
                var postCollapseText = string.Join("\n", lines);

                bool[] postCollapseIsCode =
                    ComputeTokensAndMask(postCollapseText,
                    out var postCollapseTokens);

                int[] postCollapseLineStarts =
                    IndentationProcessor.ComputeLineStarts(lines);

                var postCollapseLineInfo =
                    IndentationProcessor.ComputeLineInfo(lines,
                    postCollapseText, postCollapseIsCode,
                    postCollapseLineStarts);

                var newPreSplitContinues = new bool[lines.Count];

                for (int i = 0; i < lines.Count; i++)
                {
                    newPreSplitContinues[i] = i + 1 < lines.Count &&
                        postCollapseLineInfo[i + 1].IsContinuation;
                }

                preSplitContinues = newPreSplitContinues;
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

                bool[] reindentIsCode =
                    ComputeTokensAndMask(postSplitTextForReindent,
                    out var reindentTokens);

                lines = IndentationProcessor.Reindent(
                    lines, postSplitTextForReindent, reindentTokens,
                    reindentIsCode);
            }

            // Recompute continuation flags after line-length splitting so
            // that BlankLineProcessor can suppress blank lines between
            // continuation lines.
            var postSplitText = string.Join("\n", lines);

            bool[] postSplitIsCode = ComputeTokensAndMask(postSplitText,
                out var postSplitTokens);

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
        /// Detects and collapses patterns where the entire expression is wrapped
        /// in unnecessary parentheses that span multiple lines. This commonly
        /// occurs when a previous formatter pass wraps a method call chain like:
        /// <code>
        ///     (
        ///         sorted_layers
        ///
        ///         . append(
        ///
        ///             {
        ///             ...
        ///             }
        ///         )
        ///     )
        /// </code>
        /// and collapses it back into a single line so that the subsequent
        /// line-length splitter can handle it correctly.
        /// </summary>
        private static void CollapseWrappedExpressions(List<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                string trimmedOpen = lines[i].Trim();

                if (trimmedOpen != "(")
                {
                    continue;
                }

                int indentOpen = lines[i].Length -
                    lines[i].TrimStart().Length;

                int closeIdx = FindMatchingCloseParen(lines, i);

                if (closeIdx < 0 || closeIdx - i <= 1)
                {
                    continue;
                }

                string trimmedClose = lines[closeIdx].Trim();

                if (trimmedClose != ")")
                {
                    continue;
                }

                int indentClose = lines[closeIdx].Length -
                    lines[closeIdx].TrimStart().Length;

                if (indentOpen != indentClose)
                {
                    continue;
                }

                if (HasSiblingContentAtSameIndent(lines, i, closeIdx,
                    indentOpen))
                {
                    continue;
                }

                // Collect non-blank content lines inside the wrapping parens.
                var contentParts = new List<string>();

                for (int j = i + 1; j < closeIdx; j++)
                {
                    string trimmed = lines[j].Trim();

                    if (trimmed.Length == 0)
                    {
                        continue;
                    }

                    contentParts.Add(trimmed);
                }

                if (contentParts.Count == 0)
                {
                    continue;
                }

                // Join the content into a single expression line.
                string joined = string.Join(" ", contentParts);
                // Clean up spacing artifacts introduced by the multi-line
                // wrapping.  Naive replacements are safe here because the
                // content is a single expression — no string literals in
                // this codebase contain these exact character sequences.
                joined = joined.Replace(" .", ".");
                joined = joined.Replace(". ", ".");
                joined = joined.Replace("( ", "(");
                joined = joined.Replace(" )", ")");
                joined = joined.Replace(" {", "{");
                joined = joined.Replace(" }", "}");

                string indent = lines[i].Substring(0, indentOpen);
                string newLine = indent + joined;
                // Replace the multi-line block with the collapsed single line.
                lines.RemoveRange(i, closeIdx - i + 1);
                lines.Insert(i, newLine);
                // Re-check the new line for further collapsing.
                i--;
            }
        }

        private static int FindMatchingCloseParen(List<string> lines,
            int openIdx)
        {
            int depth = 0;

            for (int j = openIdx; j < lines.Count; j++)
            {
                string l = lines[j];

                for (int k = 0; k < l.Length; k++)
                {
                    if (l[k] == '(')
                    {
                        depth++;
                    }
                    else if (l[k] == ')')
                    {
                        depth--;

                        if (depth == 0)
                        {
                            return j;
                        }
                    }
                }
            }

            return -1;
        }

        private static bool HasSiblingContentAtSameIndent(List<string> lines,
            int openIdx, int closeIdx, int indentOpen)
        {
            for (int j = openIdx + 1; j < closeIdx; j++)
            {
                string innerTrimmed = lines[j].Trim();

                if (innerTrimmed.Length == 0)
                {
                    continue;
                }

                int innerIndent = lines[j].Length -
                    lines[j].TrimStart().Length;

                if (innerIndent <= indentOpen)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool[] ComputeTokensAndMask(string text, out List<Token>
            tokens)
        {
            tokens = Tokenizer.Tokenize(text);
            return Tokenizer.BuildCodeMask(text, tokens);
        }
    }
}
