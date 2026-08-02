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
                IndentationProcessor.Instance.ComputeLineInfo(
                    lines,
                    textForLimit,
                    isCodeForLimit,
                    lineStartsForLimit
                );

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
                    IndentationProcessor.Instance.ComputeLineInfo(
                        lines,
                        postCollapseText,
                        postCollapseIsCode,
                        postCollapseLineStarts
                    );

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

            {
                var postSplitTextForReindent2 = string.Join("\n", lines);

                bool[] reindentIsCode2 =
                    ComputeTokensAndMask(postSplitTextForReindent2,
                        out var reindentTokens2);

                lines = IndentationProcessor.Instance.Reindent(
                    lines, postSplitTextForReindent2, reindentTokens2,
                    reindentIsCode2);
            }

            lines = GlueChainsToPreviousLine(lines);
            lines = SplitOpeningParenFromChain(lines);
            lines = SplitClosingParenRun(lines);

            var postSplitText = string.Join("\n", lines);

            bool[] postSplitIsCode = ComputeTokensAndMask(postSplitText,
                out var postSplitTokens);

            int[] postSplitLineStarts =
                IndentationProcessor.Instance.ComputeLineStarts(lines);

            var postSplitLineInfo =
                IndentationProcessor.Instance.ComputeLineInfo(
                    lines,
                    postSplitText,
                    postSplitIsCode,
                    postSplitLineStarts
                );

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
        /// Splits a leading <c>(</c> from a long opening chain
        /// expression on the same line. When a non-empty line starts
        /// with <c>(</c> and the rest of the line is a chain
        /// expression (e.g. <c>(await Foo(</c>), and the line length
        /// is at least 60 characters (i.e. it is likely part of a
        /// nested multi-line call), the line is split into an
        /// indented opening paren and a deeper-indented continuation
        /// line. The split runs after blank-line processing so the
        /// chain-glue pass can stitch a trailing <c>.method()</c>
        /// back onto the closing paren.
        /// </summary>
        /// <param name="lines">The lines to process.</param>
        /// <returns>The lines with opening-paren expressions split.</returns>
        private static List<string> SplitOpeningParenFromChain(List<string>
            lines)
        {
            var result = new List<string>(lines.Count);
            int indentSize = GDScriptTextUtils.IndentSize;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                int indentLen = line.Length - line.TrimStart().Length;
                string body = line.Substring(indentLen);

                if (result.Count > 0 && body.Length > 0 && body[0] == '(' &&
                    body.Length >= 40)
                {
                    string inner = body.Substring(1);

                    string contIndent = line.Substring(0, indentLen) +
                        new string(' ', indentSize);

                    int prevIdx = result.Count - 1;

                    while (prevIdx >= 0 && result[prevIdx].Trim().Length == 0)
                    {
                        prevIdx--;
                    }

                    if (prevIdx < 0)
                    {
                        result.Add(line);
                        continue;
                    }

                    string prevBody = result[prevIdx].Trim();

                    if (prevBody.Length == 0 ||
                        (!prevBody.EndsWith("(") && !prevBody.EndsWith("=") &&
                            !prevBody.EndsWith(",")))
                    {
                        result.Add(line);
                        continue;
                    }

                    result.Add(line.Substring(0, indentLen) + "(");
                    result.Add(contIndent + inner);
                    continue;
                }

                result.Add(line);
            }

            return result;
        }

        /// <summary>
        /// Splits a run of consecutive closing parens at the end of
        /// a non-empty line into separate lines, so that chained
        /// constructs such as <c>)).instantiate()</c> become
        /// <c>)</c> followed by <c>).instantiate()</c>. The pass
        /// only fires when the line is part of a multi-line
        /// expression (i.e. the previous line is also non-empty and
        /// has code) and when the chain <c>.</c> immediately
        /// follows the run of <c>)</c>.
        /// </summary>
        /// <param name="lines">The lines to process.</param>
        /// <returns>The lines with closing-paren runs split.</returns>
        private static List<string> SplitClosingParenRun(List<string> lines)
        {
            var result = new List<string>(lines.Count);
            int indentSize = GDScriptTextUtils.IndentSize;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                int indentLen = line.Length - line.TrimStart().Length;
                string body = line.Substring(indentLen);

                int runEnd = 0;

                while (runEnd < body.Length && body[runEnd] == ')')
                {
                    runEnd++;
                }

                if (runEnd < 2 || runEnd == body.Length)
                {
                    result.Add(line);
                    continue;
                }

                if (body[runEnd] != '.')
                {
                    result.Add(line);
                    continue;
                }

                int prevIdx = result.Count - 1;

                while (prevIdx >= 0 && result[prevIdx].Trim().Length == 0)
                {
                    prevIdx--;
                }

                if (prevIdx < 0)
                {
                    result.Add(line);
                    continue;
                }

                string baseIndent = line.Substring(0, indentLen);

                string innerIndent = baseIndent + new string(' ',
                    indentSize);

                string firstRun = new string(')', runEnd - 1);
                string tail = body.Substring(runEnd - 1);

                result.Add(line);
                result[result.Count - 1] = innerIndent + firstRun;
                result.Add(baseIndent + tail);
            }

            return result;
        }

        /// <summary>
        /// Glues a chained method call (a line whose first non-blank
        /// character is <c>.</c>) onto the previous line by removing
        /// the leading <c>. </c>. If the previous line is empty
        /// (the chain was separated by a blank line), the blank is
        /// removed first. The merged line is kept as long as it does
        /// not exceed 80 characters; otherwise the chain is left on
        /// its own line.
        /// </summary>
        /// <param name="lines">The lines to process.</param>
        /// <returns>The lines with chains glued onto the previous line.</returns>
        private static List<string> GlueChainsToPreviousLine(List<string>
            lines)
        {
            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();

                if (trimmed.Length > 0 && trimmed[0] == '.' && result.Count >
                    0)
                {
                    string chainBody = trimmed.Substring(1).TrimStart();

                    int targetIdx = result.Count - 1;

                    while (targetIdx >= 0 && result[targetIdx].Trim().Length

                        == 0)
                    {
                        result.RemoveAt(targetIdx);
                        targetIdx--;
                    }

                    if (targetIdx < 0)
                    {
                        result.Add(line);
                        continue;
                    }

                    string prev = result[targetIdx];
                    int prevIndentLen = prev.Length - prev.TrimStart().Length;
                    string prevIndent = prev.Substring(0, prevIndentLen);
                    string prevBody = prev.TrimEnd();
                    string glued = prevBody + "." + chainBody;

                    if (glued.Length <= GDScriptTextUtils.MaxLineLength)
                    {
                        result[targetIdx] = prevIndent + glued;
                    }
                    else
                    {
                        result.Add(line);
                    }

                    continue;
                }

                result.Add(line);
            }

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

        /// <summary>
        /// Detects multi-line parenthesised expressions (e.g.
        /// <c>x = (\n  inner_expr\n)</c>) and collapses them into
        /// a single line when the joined content would exceed 80
        /// characters AND the resulting single line contains a
        /// re-formatable shape (a top-level <c>=</c> or a comma in
        /// brackets). This lets the subsequent line-length splitter
        /// re-format the expression with one argument per line and
        /// without spurious blank lines or chain-glue whitespace.
        /// Expressions that cannot be cleanly re-split (e.g.
        /// <c>if (a and b):</c>) are left in their multi-line form.
        /// </summary>
        private static void CollapseLongMultiLineExpressions(List<string>
            lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimEnd();

                if (trimmed.Length == 0)
                {
                    continue;
                }

                int openParens = 0;
                int closeParens = 0;

                foreach (char c in trimmed)
                {
                    if (c == '(')
                    {
                        openParens++;
                    }
                    else if (c == ')')
                    {
                        closeParens++;
                    }
                }

                if (openParens <= closeParens)
                {
                    continue;
                }

                int startDepth = 1;
                int closeIdx = -1;
                int closeParenLinePos = -1;

                for (int j = i + 1; j < lines.Count; j++)
                {
                    foreach (char c in lines[j])
                    {
                        if (c == '(')
                        {
                            startDepth++;
                        }
                        else if (c == ')')
                        {
                            startDepth--;

                            if (startDepth == 0)
                            {
                                closeIdx = j;
                                break;
                            }
                        }
                    }

                    if (closeIdx >= 0)
                    {
                        int lastClose = lines[j].LastIndexOf(')');

                        if (lastClose >= 0)
                        {
                            closeParenLinePos = lastClose;
                        }

                        break;
                    }
                }

                if (closeIdx <= i + 1 || closeParenLinePos < 0)
                {
                    continue;
                }

                string firstLine = lines[i];
                int openParenPos = firstLine.LastIndexOf('(');

                if (openParenPos < 0)
                {
                    continue;
                }

                string prefix = firstLine.Substring(0, openParenPos + 1);
                string suffix = lines[closeIdx].Substring(closeParenLinePos);

                var contentParts = new List<string>();
                int totalLen = prefix.Length + suffix.Length;

                for (int j = i + 1; j < closeIdx; j++)
                {
                    string t = lines[j].Trim();

                    if (t.Length == 0)
                    {
                        continue;
                    }

                    contentParts.Add(t);
                    totalLen += 1 + t.Length;
                }

                if (totalLen <= GDScriptTextUtils.MaxLineLength)
                {
                    continue;
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

                int indentLen = line.Length - line.TrimStart().Length;
                string indent = line.Substring(0, indentLen);
                string collapsed = indent + prefix + joined + suffix;

                if (!IsReformatableExpression(collapsed))
                {
                    continue;
                }

                lines.RemoveRange(i, closeIdx - i + 1);
                lines.Insert(i, collapsed);
                i--;
            }
        }

        /// <summary>
        /// Returns true if the collapsed single-line expression can
        /// be re-formatted by the line-length splitter. An
        /// expression is considered re-formatable when it has a
        /// top-level <c>=</c> (handled by the equals-wrap strategy)
        /// or a comma at bracket depth &gt; 0 (handled by the
        /// unclosed/closed bracket comma-split strategies).
        /// </summary>
        private static bool IsReformatableExpression(string collapsed)
        {
            int depth = 0;

            for (int i = 0; i < collapsed.Length; i++)
            {
                char c = collapsed[i];

                if (c == '(' || c == '[' || c == '{')
                {
                    depth++;
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                }
                else if (c == '=' && depth == 0)
                {
                    if (i > 0 && collapsed[i - 1] != '=' &&
                        collapsed[i - 1] != '!' && collapsed[i - 1] != '<' &&
                        collapsed[i - 1] != '>' && collapsed[i - 1] != '+' &&
                        collapsed[i - 1] != '-' && collapsed[i - 1] != '*' &&
                        collapsed[i - 1] != '/' && collapsed[i - 1] != ':')
                    {
                        if (i + 1 < collapsed.Length && collapsed[i + 1] != '=')
                        {
                            return true;
                        }
                    }
                }
                else if (c == ',' && depth > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSiblingContentAtSameIndent(
            List<string> lines,
            int openIdx,
            int closeIdx,
            int indentOpen
        )
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
