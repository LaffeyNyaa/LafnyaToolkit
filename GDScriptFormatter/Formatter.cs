using System.Collections.Generic;

using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace GDScriptFormatter
{
    /// <summary>
    /// Core implementation that applies all GDScript formatting rules.
    /// </summary>
    public sealed class Formatter
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly Formatter Instance = new Formatter();
        private Formatter()
        {
        }

        /// <summary>
        /// Applies all formatting rules to a source string and returns the result. Line endings are
        /// normalized first, then tabs are normalized only in Code regions, then enums are expanded,
        /// and finally the tokenization is reused for re-indentation and line-length splitting.
        /// </summary>
        /// <param name="source">The original source string.</param>
        /// <returns>The formatted source string.</returns>
        public string Format(string source)
        {
            if (source == null || source.Length == 0)
            {
                return source ?? string.Empty;
            }

            string text = source.Replace("\r\n", "\n").Replace("\r", "\n");
            text = DocCommentMover.Instance.MoveFileDocComments(text);
            text = GDScriptTextUtils.Instance.NormalizeCommentSpaces(text);

            bool[] tabMask = ComputeTokensAndMask(text, out var tabTokens);
            text = GDScriptTextUtils.Instance.NormalizeTabs(text, tabMask);
            text = MemberReorderer.Instance.ReorderMembers(text);

            text = EnumFormatter.Instance.ExpandEnums(text);

            bool[] isCode = ComputeTokensAndMask(text, out var tokens);

            var lines = TextUtils.SplitLines(text);

            lines = IndentationProcessor.Instance.Reindent(lines, text, tokens,
                isCode);

            string textForLimit = string.Join("\n", lines);

            bool[] isCodeForLimit = ComputeTokensAndMask(textForLimit,
                out var tokensForLimit);

            int[] lineStartsForLimit =
                IndentationProcessor.Instance.ComputeLineStarts(lines);

            var lineInfoForLimit =
                IndentationProcessor.Instance.ComputeLineInfo(lines,
                textForLimit,
                isCodeForLimit, lineStartsForLimit);

            var preSplitContinues = new bool[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                preSplitContinues[i] = i + 1 < lines.Count &&
                    lineInfoForLimit[i + 1].IsContinuation;
            }

            CollapseWrappedExpressions(lines);
            {
                var postCollapseText = string.Join("\n", lines);

                bool[] postCollapseIsCode =
                    ComputeTokensAndMask(postCollapseText,
                    out var postCollapseTokens);

                int[] postCollapseLineStarts =
                    IndentationProcessor.Instance.ComputeLineStarts(lines);

                var postCollapseLineInfo =
                    IndentationProcessor.Instance.ComputeLineInfo(lines,
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

            lines = LineLengthProcessor.Instance.ApplyLineLengthLimit(lines,
                preSplitContinues);

            {
                var postSplitTextForReindent = string.Join("\n", lines);

                bool[] reindentIsCode =
                    ComputeTokensAndMask(postSplitTextForReindent,
                    out var reindentTokens);

                lines = IndentationProcessor.Instance.Reindent(
                    lines, postSplitTextForReindent, reindentTokens,
                    reindentIsCode);
            }

            var postSplitText = string.Join("\n", lines);

            bool[] postSplitIsCode = ComputeTokensAndMask(postSplitText,
                out var postSplitTokens);

            int[] postSplitLineStarts =
                IndentationProcessor.Instance.ComputeLineStarts(lines);

            var postSplitLineInfo =
                IndentationProcessor.Instance.ComputeLineInfo(lines,
                postSplitText,
                postSplitIsCode, postSplitLineStarts);

            var postSplitContinues = new bool[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                postSplitContinues[i] =
                    postSplitLineInfo[i].IsContinuation;
            }

            lines = BlankLineProcessor.Instance.ApplyBlankLineRules(lines,
                postSplitContinues);

            lines = BlankLineProcessor.Instance.CollapseBlankLines(lines);
            lines = BlankLineProcessor.Instance.TrimTrailingWhitespace(lines);
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

                string joined = string.Join(" ", contentParts);
                joined = joined.Replace(" .", ".");
                joined = joined.Replace(". ", ".");
                joined = joined.Replace("( ", "(");
                joined = joined.Replace(" )", ")");
                joined = joined.Replace(" {", "{");
                joined = joined.Replace(" }", "}");

                string indent = lines[i].Substring(0, indentOpen);
                string newLine = indent + joined;
                lines.RemoveRange(i, closeIdx - i + 1);
                lines.Insert(i, newLine);
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
            tokens = GDScriptTokenizer.Instance.Tokenize(text);
            return GDScriptTokenizer.Instance.BuildCodeMask(text, tokens);
        }
    }
}
