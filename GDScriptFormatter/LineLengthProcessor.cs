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
        /// Updates the running brace depth by scanning a line for bracket
        /// characters, accounting for the input depth from previous lines.
        /// </summary>
        private static int UpdateBraceDepth(string line,
            int currentDepth)
        {
            return BracketDepthTracker.UpdateDepth(line, currentDepth);
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
    }
}
