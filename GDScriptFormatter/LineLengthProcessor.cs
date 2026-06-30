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

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                if (line.Length <= TextUtils.MaxLineLength)
                {
                    result.Add(line);
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

                var split = SplitLongLine(line, fixedContIndent);
                result.AddRange(split);
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
        /// <returns>The list of split segments.</returns>
        private static List<string> SplitLongLine(string line,
            string fixedContIndent)
        {
            if (line.Length <= TextUtils.MaxLineLength)
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

            int bracketDepth = 0;

            for (int ci = indentLen; ci < line.Length; ci++)
            {
                if (!isCode[ci])
                {
                    continue;
                }

                char c = line[ci];

                if (c == '(' || c == '[' || c == '{')
                {
                    bracketDepth++;
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (bracketDepth > 0)
                    {
                        bracketDepth--;
                    }
                }
            }

            bool hasUnclosedBracket = bracketDepth > 0;

            if (hasUnclosedBracket)
            {
                int breakAt = FindCommaBreakInBrackets(line, isCode, indentLen);

                if (breakAt > 0 && breakAt < line.Length)
                {
                    string first = line.Substring(0, breakAt).TrimEnd();

                    string rest = contIndent +
                        line.Substring(breakAt).TrimStart();

                    if (first.Length > 0 && first.Length < line.Length)
                    {
                        var res = new List<string> { first };
                        res.AddRange(SplitLongLine(rest, contIndent));
                        return res;
                    }
                }
            }

            if (!hasUnclosedBracket)
            {
                int breakAt = FindCommaBreakInBrackets(line, isCode, indentLen);

                if (breakAt > 0 && breakAt < line.Length)
                {
                    string first = line.Substring(0, breakAt).TrimEnd();

                    if (first.Length > 0 && first.Length <=
                        TextUtils.MaxLineLength &&
                        first.Length < line.Length)
                    {
                        string rest = contIndent +
                            line.Substring(breakAt).TrimStart();

                        var res = new List<string> { first };
                        res.AddRange(SplitLongLine(rest, contIndent));
                        return res;
                    }
                }
            }

            int eqPos = FindTopLevelEquals(line, isCode, indentLen);

            if (eqPos >= 0)
            {
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
            }

            return new List<string> { line };
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
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                }
                else if (c == ',' && depth > 0)
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
