using System;
using System.Collections.Generic;

namespace GDScriptFormatter
{
    /// <summary>
    /// Splits lines exceeding the maximum length at safe token boundaries.
    /// </summary>
    internal static class LineLengthProcessor
    {
        /// <summary>
        /// Splits lines exceeding 80 characters: split after commas inside already-open brackets;
        /// for assignment statements, wrap the RHS in (...) then split; leave the line unchanged if no safe split point is found.
        /// <paramref name="lineContinuesNext"/> flags whether each line ends with a continuation
        /// indicator; when a line is itself a continuation of the previous line, its split
        /// segments reuse the line's current indent (no extra level) so that splitting a
        /// continuation line does not cascade into deeper indents on a second pass.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <param name="lineContinuesNext">Per-line flags indicating whether the line ends with
        /// a continuation indicator; entry i corresponds to line i. May be null when
        /// continuation detection is not available.</param>
        /// <returns>The lines with long lines split.</returns>
        internal static List<string> ApplyLineLengthLimit(List<string> lines,
            bool[] lineContinuesNext)
        {
            var result = new List<string>(lines.Count);
            int runningBraceDepth = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                // Guard clause: short lines pass through unchanged

                if (line.Length <= TextUtils.MaxLineLength)
                {
                    result.Add(line);

                    runningBraceDepth = BracketDepthTracker.UpdateDepth(
                        line, runningBraceDepth);

                    continue;
                }

                // If this line is itself a continuation of the previous line
                // (previous line ends with a continuation indicator), the
                // continuation indent equals this line's current indent — do
                // NOT add another indent level. Otherwise, continuation
                // segments are indented one level deeper than the statement
                // base indent (handled by passing null to SplitLongLine).
                bool isContinuation = lineContinuesNext != null &&
                    i > 0 && i - 1 < lineContinuesNext.Length &&
                    lineContinuesNext[i - 1];

                string fixedContIndent;

                if (isContinuation)
                {
                    int indentLen = 0;

                    while (indentLen < line.Length &&
                        line[indentLen] == ' ')
                    {
                        indentLen++;
                    }

                    fixedContIndent = line.Substring(0, indentLen);
                }
                else
                {
                    fixedContIndent = null;
                }

                // Check whether this line has continuation lines
                // following it. If so, skip top-level = wrapping to
                // avoid creating orphan continuation lines.
                bool continuesNext = lineContinuesNext != null &&
                    i < lineContinuesNext.Length &&
                    lineContinuesNext[i];

                var split = SplitLongLine(line, fixedContIndent,
                    continuesNext, runningBraceDepth);

                result.AddRange(split);

                runningBraceDepth = BracketDepthTracker.UpdateDepth(
                    line, runningBraceDepth);
            }

            return result;
        }

        /// <summary>
        /// Recursively splits a line so each segment is at most 80 characters. Splitting priority:
        /// unclosed-bracket comma split; closed-bracket comma split (commas inside already-balanced
        /// brackets); top-level equals wrapping; otherwise leave the line unchanged.
        /// <paramref name="fixedContIndent"/> is the fixed continuation indent reused across all
        /// continuation segments so that 3+ segment splits do not cascade; pass null on the first
        /// call to trigger computation from the original line's indent.
        /// </summary>
        /// <param name="line">The line to split.</param>
        /// <param name="fixedContIndent">The fixed continuation indent, or null to compute from
        /// the line's indent on the first split.</param>
        /// <param name="continuesNext">Whether the next line is a continuation of this line;
        /// when true, top-level = wrapping is skipped to avoid orphan continuation lines.</param>
        /// <param name="inheritedBraceDepth">Brace depth accumulated from previous lines;
        /// when greater than 0 the line is inside a brace-delimited construct (dictionary,
        /// array, or parenthesised expression) and all splitting is skipped.</param>
        /// <returns>The list of split segments.</returns>
        private static List<string> SplitLongLine(string line,
            string fixedContIndent, bool continuesNext = false,
            int inheritedBraceDepth = 0)
        {
            // Guard clause: short lines pass through unchanged

            if (line.Length <= TextUtils.MaxLineLength)
            {
                return new List<string> { line };
            }

            // Guard clause: skip splitting inside brace-delimited constructs

            if (inheritedBraceDepth > 0)
            {
                return new List<string> { line };
            }

            int indentLen = 0;

            while (indentLen < line.Length && line[indentLen] == ' ')
            {
                indentLen++;
            }

            if (indentLen >= line.Length)
            {
                return new List<string> { line };
            }

            string indent = line.Substring(0, indentLen);
            // On the first call (fixedContIndent == null), compute the fixed
            // continuation indent from the original line's indent. This indent
            // is reused for ALL continuation segments so that 3+ segment
            // splits do not cascade (parent+4 for every continuation line,
            // matching Reindent's behaviour for continuation lines).
            string contIndent = fixedContIndent ?? (indent +
                new string(' ', TextUtils.IndentSize));

            var tokens = Tokenizer.Tokenize(line);
            bool[] isCode = Tokenizer.BuildCodeMask(line, tokens);
            // Try each splitting strategy in priority order
            var result = TryUnclosedBracketSplit(line, contIndent, isCode,
                indentLen);

            if (result != null)
            {
                return result;
            }

            result = TryBraceAlignSplit(line, contIndent, isCode, indentLen);

            if (result != null)
            {
                return result;
            }

            result = TryClosedBracketSplit(line, contIndent, isCode, indentLen);

            if (result != null && result[0].Length <= TextUtils.MaxLineLength)
            {
                return result;
            }

            result = TryTopLevelEqualsSplit(line, contIndent, indent, isCode,
                indentLen, continuesNext);

            if (result != null)
            {
                return result;
            }

            return new List<string> { line };
        }

        /// <summary>
        /// Attempts to split a line at a comma inside unclosed brackets.
        /// Returns the split segments, or null if this strategy does not apply.
        /// </summary>
        private static List<string> TryUnclosedBracketSplit(string line,
            string contIndent, bool[] isCode, int indentLen)
        {
            int bracketDepth = BracketDepthTracker.FindBracketDepth(line,
                isCode, indentLen);

            if (bracketDepth <= 0)
            {
                return null;
            }

            int breakAt = FindCommaBreakInBrackets(line, isCode, indentLen);

            if (breakAt <= 0 || breakAt >= line.Length)
            {
                return null;
            }

            string first = line.Substring(0, breakAt).TrimEnd();

            string rest = contIndent +
                line.Substring(breakAt).TrimStart();

            if (first.Length <= 0 || first.Length >= line.Length)
            {
                return null;
            }

            var res = new List<string> { first };
            res.AddRange(SplitLongLine(rest, contIndent));
            return res;
        }

        /// <summary>
        /// Attempts to split a line at a comma inside already-balanced brackets.
        /// Returns the split segments, or null if this strategy does not apply.
        /// </summary>
        private static List<string> TryClosedBracketSplit(string line,
            string contIndent, bool[] isCode, int indentLen)
        {
            int bracketDepth = BracketDepthTracker.FindBracketDepth(line,
                isCode, indentLen);

            if (bracketDepth > 0)
            {
                return null;
            }

            int breakAt = FindCommaBreakInBrackets(line, isCode, indentLen);

            if (breakAt <= 0 || breakAt >= line.Length)
            {
                return null;
            }

            string first = line.Substring(0, breakAt).TrimEnd();

            if (first.Length <= 0 || first.Length >= line.Length)
            {
                return null;
            }

            string rest = contIndent +
                line.Substring(breakAt).TrimStart();

            var res = new List<string> { first };
            res.AddRange(SplitLongLine(rest, contIndent));
            return res;
        }

        /// <summary>
        /// Attempts to split a line at a top-level assignment equals sign by
        /// wrapping the RHS in parentheses and splitting inside them.
        /// Returns the split segments, or null if this strategy does not apply.
        /// </summary>
        private static List<string> TryTopLevelEqualsSplit(string line,
            string contIndent, string indent, bool[] isCode, int indentLen,
            bool continuesNext)
        {
            // If this line is followed by continuation lines, do NOT
            // apply top-level = wrapping — it would orphan the continuation
            // lines and produce invalid GDScript.

            if (continuesNext)
            {
                return null;
            }

            int eqPos = FindTopLevelEquals(line, isCode, indentLen);

            if (eqPos < 0)
            {
                return null;
            }

            string beforeEq = line.Substring(0, eqPos).TrimEnd();
            string afterEq = line.Substring(eqPos + 1).TrimStart();

            if (afterEq.Length > 0 && !afterEq.StartsWith("("))
            {
                string firstLine = beforeEq + " = (";
                string rhsCont = contIndent + afterEq;
                // The close paren must sit at the same level as the
                // opening line (indent) because Reindent treats a line
                // that starts with a closing bracket as returning to
                // the parent indent level in the next pass.
                string closeLine = indent + ")";

                var rhsSplit = SplitLongLine(rhsCont, contIndent);
                // If the RHS could not be split further (still a single line
                // exceeding the limit), wrapping it in parentheses is useless
                // — return null to leave the original line unchanged.

                if (rhsSplit.Count == 1 &&
                    rhsSplit[0].TrimStart().Length > TextUtils.MaxLineLength)
                {
                    return null;
                }

                var res2 = new List<string> { firstLine };
                res2.AddRange(rhsSplit);
                res2.Add(closeLine);
                return res2;
            }

            if (afterEq.StartsWith("("))
            {
                int breakAt2 = FindCommaBreakInBrackets(
                    line, isCode, eqPos + 1);

                if (breakAt2 > 0 && breakAt2 < line.Length)
                {
                    string first2 = line.Substring(0, breakAt2).TrimEnd();

                    string rest2 = contIndent +
                        line.Substring(breakAt2).TrimStart();

                    if (first2.Length > 0 && first2.Length < line.Length)
                    {
                        var res3 = new List<string> { first2 };
                        res3.AddRange(SplitLongLine(rest2, contIndent));
                        return res3;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a safe break point after a comma inside brackets.
        /// </summary>
        private static int FindCommaBreakInBrackets(string line,
            bool[] isCode, int startIdx)
        {
            int best = -1;
            int depth = 0;

            for (int i = startIdx; i < line.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = line[i];

                if (c == '(' || c == '[' || c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == ')' || c == ']' || c == '}')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }

                    continue;
                }

                if (c == ',' && depth > 0)
                {
                    int bp = i + 1;

                    if (bp <= TextUtils.MaxLineLength)
                    {
                        best = bp;
                    }
                    else if (best < 0)
                    {
                        best = bp;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Finds the position of a top-level (outside brackets) assignment equals sign in a line (excluding ==, !=, &lt;=, &gt;=).
        /// </summary>
        private static int FindTopLevelEquals(string line, bool[] isCode,
            int startIdx)
        {
            int depth = 0;

            for (int i = startIdx; i < line.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = line[i];

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
                    if (i > 0 && isCode[i - 1])
                    {
                        char prev = line[i - 1];

                        if (prev == '=' || prev == '!' || prev == '<' ||
                            prev == '>' || prev == '+' || prev == '-' ||
                            prev == '*' || prev == '/' || prev == ':')
                        {
                            continue;
                        }
                    }

                    if (i + 1 < line.Length && isCode[i + 1] &&
                        line[i + 1] == '=')
                    {
                        continue;
                    }

                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Attempts to split a line containing a dict/array literal as the sole argument
        /// of a method call (e.g. <c>expr({"key": value, ...})</c>). Splits at the opening
        /// brace boundary, expands each comma-separated item onto its own continuation line,
        /// and places the closing <c>})</c> on its own line at the original indent.
        ///
        /// This strategy runs after unclosed-bracket splitting and before closed-bracket
        /// comma splitting, so that dict/array literals are expanded in one step rather
        /// than fragmented by simple comma-splitting.</summary>
        private static List<string> TryBraceAlignSplit(string line,
            string contIndent, bool[] isCode, int indentLen)
        {
            int bracketDepth = BracketDepthTracker.FindBracketDepth(line,
                isCode, indentLen);

            if (bracketDepth > 0)
            {
                return null;
            }

            string trimmed = line.TrimEnd();

            if (!trimmed.EndsWith("})") && !trimmed.EndsWith("])"))
            {
                return null;
            }

            // Find the last opening brace/bracket that is directly inside a
            // paren/bracket with only whitespace between (e.g. the { in
            // append({...}) or the [ in some_func([...])).
            int openBrace = -1;
            int depth = 0;
            int lastParenAtDepth0 = -1;

            for (int i = indentLen; i < line.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = line[i];

                if (c == '(' || c == '[')
                {
                    if (depth == 0)
                    {
                        lastParenAtDepth0 = i;
                    }

                    depth++;
                }
                else if (c == '{')
                {
                    if (depth == 1 && lastParenAtDepth0 >= 0)
                    {
                        // Check there is only whitespace between the
                        // opening paren/bracket and this brace/bracket.
                        bool onlyWhitespace = true;

                        for (int j = lastParenAtDepth0 + 1; j < i; j++)
                        {
                            if (isCode[j] && !char.IsWhiteSpace(line[j]))
                            {
                                onlyWhitespace = false;
                                break;
                            }
                        }

                        if (onlyWhitespace)
                        {
                            openBrace = i;
                        }
                    }

                    depth++;
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                }
            }

            if (openBrace < 0)
            {
                return null;
            }

            // The first segment must fit within the line limit.
            string first = line.Substring(0, openBrace).TrimEnd() + "{";

            if (first.Length > TextUtils.MaxLineLength)
            {
                return null;
            }

            // Extract the content between { and the trailing }).
            // Track leading whitespace trimmed so that the isCode offset passed
            // to SplitByTopLevelCommas remains accurate (TrimStart shifts the
            // substring, and the lineOffset must account for that shift).
            string rawAfterBrace = line.Substring(openBrace + 1);
            string afterBrace = rawAfterBrace.TrimStart();
            int trimmedStart = rawAfterBrace.Length - afterBrace.Length;
            // Extract closing suffix: collect trailing closing characters
            string afterBraceTrimmed = afterBrace.TrimEnd();
            int suffixStart = afterBraceTrimmed.Length;

            while (suffixStart > 0 && (afterBraceTrimmed[suffixStart - 1] == ')'
                || afterBraceTrimmed[suffixStart - 1] == ']'
                || afterBraceTrimmed[suffixStart - 1] == '}'))
            {
                suffixStart--;
            }

            string suffix = afterBraceTrimmed.Substring(suffixStart);
            string closingSuffix;

            if (suffix.Length > 0)
            {
                closingSuffix = suffix;

                afterBrace = afterBraceTrimmed.Substring(0,
                    suffixStart).TrimEnd();
            }
            else
            {
                return null;
            }

            // Split the dict/array content by top-level commas.
            var items = SplitByTopLevelCommas(afterBrace, isCode,
                openBrace + 1 + trimmedStart);

            var result = new List<string> { first };

            foreach (var rawItem in items)
            {
                string item = rawItem.Trim();

                if (item.Length > 0)
                {
                    result.Add(contIndent + item + ",");
                }
            }

            string indent = line.Substring(0, indentLen);
            result.Add(indent + closingSuffix);
            return result;
        }

        /// <summary>
        /// Splits <paramref name="text"/> by commas that are at bracket depth 0
        /// (i.e., not inside nested parentheses, square brackets, or braces).
        /// Uses the <paramref name="isCode"/> mask relative to the original line
        /// via <paramref name="lineOffset"/> to skip non-code regions.
        /// </summary>
        private static List<string> SplitByTopLevelCommas(string text,
            bool[] isCode, int lineOffset)
        {
            var items = new List<string>();
            int start = 0;
            int depth = 0;

            for (int i = 0; i < text.Length; i++)
            {
                int globalIdx = lineOffset + i;

                if (globalIdx < isCode.Length && !isCode[globalIdx])
                {
                    continue;
                }

                char c = text[i];

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
                else if (c == ',' && depth == 0)
                {
                    items.Add(text.Substring(start, i - start));
                    start = i + 1;
                }
            }

            if (start < text.Length)
            {
                items.Add(text.Substring(start));
            }

            return items;
        }
    }
}
