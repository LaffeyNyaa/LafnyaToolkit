using System.Collections.Generic;

using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace GDScriptFormatter
{
    /// <summary>
    /// Splits lines exceeding the maximum length at safe token
    /// boundaries. Splitting priority: unclosed-bracket comma split,
    /// closed-bracket comma split, top-level equals wrapping, dict
    /// /array-literal alignment, otherwise leave the line unchanged.
    /// </summary>
    public sealed class LineLengthProcessor
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly LineLengthProcessor Instance =
            new LineLengthProcessor();

        private LineLengthProcessor()
        {
        }

        /// <summary>
        /// Splits lines exceeding 80 characters: split after commas
        /// inside already-open brackets; for assignment statements,
        /// wrap the RHS in (...) then split; leave the line unchanged
        /// if no safe split point is found. <paramref name="lineContinuesNext"/>
        /// flags whether each line ends with a continuation indicator;
        /// when a line is itself a continuation of the previous line,
        /// its split segments reuse the line's current indent (no extra
        /// level) so that splitting a continuation line does not
        /// cascade into deeper indents on a second pass.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <param name="lineContinuesNext">Per-line flags indicating whether the line ends with a continuation indicator; entry i corresponds to line i. May be null when continuation detection is not available.</param>
        /// <returns>The lines with long lines split.</returns>
        public List<string> ApplyLineLengthLimit(List<string> lines,
            bool[] lineContinuesNext)
        {
            var result = new List<string>(lines.Count);
            int runningBraceDepth = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                if (line.Length <= GDScriptTextUtils.MaxLineLength)
                {
                    result.Add(line);

                    runningBraceDepth =
                        BracketDepthTracker.Instance.UpdateDepth(
                        line, runningBraceDepth);

                    continue;
                }

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

                bool continuesNext = lineContinuesNext != null &&
                    i < lineContinuesNext.Length &&
                    lineContinuesNext[i];

                var split = SplitLongLine(line, fixedContIndent,
                    continuesNext, runningBraceDepth);

                result.AddRange(split);

                runningBraceDepth = BracketDepthTracker.Instance.UpdateDepth(
                    line, runningBraceDepth);
            }

            return result;
        }

        /// <summary>
        /// Recursively splits a line so each segment is at most 80
        /// characters. Splitting priority: unclosed-bracket comma
        /// split; closed-bracket comma split (commas inside
        /// already-balanced brackets); top-level equals wrapping;
        /// otherwise leave the line unchanged. <paramref name="fixedContIndent"/>
        /// is the fixed continuation indent reused across all
        /// continuation segments so that 3+ segment splits do not
        /// cascade; pass null on the first call to trigger computation
        /// from the original line's indent.
        /// </summary>
        /// <param name="line">The line to split.</param>
        /// <param name="fixedContIndent">The fixed continuation indent, or null to compute from the line's indent on the first split.</param>
        /// <param name="continuesNext">Whether the next line is a continuation of this line; when true, top-level = wrapping is skipped to avoid orphan continuation lines.</param>
        /// <param name="inheritedBraceDepth">Brace depth accumulated from previous lines; when greater than 0 the line is inside a brace-delimited construct (dictionary, array, or parenthesised expression) and all splitting is skipped.</param>
        /// <returns>The list of split segments.</returns>
        private List<string> SplitLongLine(string line,
            string fixedContIndent, bool continuesNext = false,
            int inheritedBraceDepth = 0)
        {
            if (line.Length <= GDScriptTextUtils.MaxLineLength)
            {
                return new List<string> { line };
            }

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

            string contIndent = fixedContIndent ?? (indent +
                new string(' ', GDScriptTextUtils.IndentSize));

            var tokens = GDScriptTokenizer.Instance.Tokenize(line);

            bool[] isCode = GDScriptTokenizer.Instance.BuildCodeMask(line,
                tokens);

            var result = TryArgumentPerLineSplit(line, contIndent, isCode,
                indentLen);

            if (result != null)
            {
                return result;
            }

            result = TryUnclosedBracketSplit(line, contIndent, isCode,
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

            if (result != null && result[0].Length <=
                GDScriptTextUtils.MaxLineLength)
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
        /// Attempts to split a line by placing each top-level
        /// comma-separated argument of a function/method call onto
        /// its own continuation line. The closing <c>)</c> is placed
        /// on its own line at the original indent. Applies to any
        /// comma-separated argument list opened by an outermost
        /// unclosed or just-closed <c>(</c>, including <c>func</c>
        /// declarations, regular method calls, and nested calls.
        /// Returns the split segments, or null if this strategy does
        /// not apply (e.g. the first argument is itself too long).
        /// </summary>
        /// <param name="line">The line to split.</param>
        /// <param name="contIndent">The continuation indent.</param>
        /// <param name="isCode">The code mask of the line.</param>
        /// <param name="indentLen">The leading-space count.</param>
        /// <returns>The split segments or null.</returns>
        private List<string> TryArgumentPerLineSplit(string line,
            string contIndent, bool[] isCode, int indentLen)
        {
            int bracketDepth =
                BracketDepthTracker.Instance.FindBracketDepth(line,
                isCode, indentLen);

            if (bracketDepth <= 0 &&
                !IsConservativeArgumentListLine(line, isCode, indentLen))
            {
                return null;
            }

            if (FindTopLevelEquals(line, isCode, indentLen) >= 0)
            {
                return null;
            }

            int outerOpenParen = FindOuterOpenParenAtDepth0(line, isCode,
                indentLen);

            if (outerOpenParen < 0)
            {
                return null;
            }

            int matchingCloseParen = FindMatchingCloseForParen(line, isCode,
                outerOpenParen);

            if (bracketDepth == 0 && matchingCloseParen < 0)
            {
                return null;
            }

            int contentEnd = matchingCloseParen >= 0 ? matchingCloseParen :
            line.Length;

            string prefix = line.Substring(0, outerOpenParen + 1).TrimEnd();

            if (prefix.Length <= 0)
            {
                return null;
            }

            string argsText = line.Substring(outerOpenParen + 1,
                contentEnd - outerOpenParen - 1);

            var items = SplitByTopLevelCommas(argsText, isCode,
                outerOpenParen + 1);

            if (items.Count <= 1)
            {
                return null;
            }

            int firstItemLen = contIndent.Length +
                items[0].TrimStart().Length;

            if (firstItemLen > GDScriptTextUtils.MaxLineLength)
            {
                return null;
            }

            string closeSuffix;

            if (matchingCloseParen >= 0)
            {
                closeSuffix = line.Substring(matchingCloseParen).TrimEnd();
            }
            else
            {
                closeSuffix = ")";
            }

            string indent = line.Substring(0, indentLen);
            var built = new List<string>(items.Count + 2) { prefix };

            for (int i = 0; i < items.Count; i++)
            {
                string item = items[i].Trim();

                if (item.Length == 0)
                {
                    continue;
                }

                bool isLast = i == items.Count - 1;
                built.Add(contIndent + item + (isLast ? string.Empty : ","));
            }

            built.Add(indent + closeSuffix);

            var finalResult = new List<string>(built.Count);

            foreach (var segment in built)
            {
                if (segment.Length <= GDScriptTextUtils.MaxLineLength)
                {
                    finalResult.Add(segment);
                }
                else
                {
                    finalResult.AddRange(SplitLongLine(segment, contIndent));
                }
            }

            return finalResult;
        }

        /// <summary>
        /// Detects a function/method-call argument list using a
        /// conservative rule: a line qualifies when its content after
        /// the leading indent begins with <c>func </c>, or when it has
        /// an outermost <c>(</c> at bracket depth 0 followed by at
        /// least one code character. Used to broaden detection to
        /// <c>func</c> declarations and closed-on-same-line calls
        /// where the strict unclosed-bracket rule does not apply.
        /// </summary>
        /// <param name="line">The line to check.</param>
        /// <param name="isCode">The code mask of the line.</param>
        /// <param name="indentLen">The leading-space count.</param>
        /// <returns>True if the line looks like a function/method argument list.</returns>
        private static bool IsConservativeArgumentListLine(string line,
            bool[] isCode, int indentLen)
        {
            string content = line.Substring(indentLen).TrimStart();

            if (content.StartsWith("func "))
            {
                return true;
            }

            int firstParen = FindOuterOpenParenAtDepth0(line, isCode,
                indentLen);

            if (firstParen < 0)
            {
                return false;
            }

            for (int i = firstParen + 1; i < line.Length; i++)
            {
                if (i < isCode.Length && !isCode[i])
                {
                    continue;
                }

                if (line[i] == ')' || line[i] == ']' || line[i] == '}')
                {
                    break;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the index of the first <c>(</c> at bracket depth 0
        /// in the line, scanning only Code-region characters. Returns
        /// -1 if no such <c>(</c> exists.
        /// </summary>
        /// <param name="line">The line to scan.</param>
        /// <param name="isCode">The code mask of the line.</param>
        /// <param name="startIdx">The starting index (typically the indent length).</param>
        /// <returns>The position of the first depth-0 <c>(</c>, or -1.</returns>
        private static int FindOuterOpenParenAtDepth0(string line,
            bool[] isCode, int startIdx)
        {
            int depth = 0;

            for (int i = startIdx; i < line.Length; i++)
            {
                if (i < isCode.Length && !isCode[i])
                {
                    continue;
                }

                char c = line[i];

                if (c == '(')
                {
                    if (depth == 0)
                    {
                        return i;
                    }

                    depth++;
                }
                else if (c == '[' || c == '{')
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
            }

            return -1;
        }

        /// <summary>
        /// Finds the matching closing <c>)</c> for the open <c>(</c>
        /// at <paramref name="openParenIdx"/>, scanning forward.
        /// Returns -1 if no matching close exists on the line.
        /// </summary>
        /// <param name="line">The line to scan.</param>
        /// <param name="isCode">The code mask of the line.</param>
        /// <param name="openParenIdx">The index of the opening <c>(</c>.</param>
        /// <returns>The position of the matching <c>)</c>, or -1.</returns>
        private static int FindMatchingCloseForParen(string line,
            bool[] isCode, int openParenIdx)
        {
            int depth = 1;

            for (int i = openParenIdx + 1; i < line.Length; i++)
            {
                if (i < isCode.Length && !isCode[i])
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

                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Attempts to split a line at a comma inside unclosed
        /// brackets. Returns the split segments, or null if this
        /// strategy does not apply.
        /// </summary>
        /// <param name="line">The line to split.</param>
        /// <param name="contIndent">The continuation indent.</param>
        /// <param name="isCode">The code mask of the line.</param>
        /// <param name="indentLen">The leading-space count.</param>
        /// <returns>The split segments or null.</returns>
        private List<string> TryUnclosedBracketSplit(string line,
            string contIndent, bool[] isCode, int indentLen)
        {
            int bracketDepth =
                BracketDepthTracker.Instance.FindBracketDepth(line,
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
        /// Attempts to split a line at a comma inside already-balanced
        /// brackets. Returns the split segments, or null if this
        /// strategy does not apply.
        /// </summary>
        /// <param name="line">The line to split.</param>
        /// <param name="contIndent">The continuation indent.</param>
        /// <param name="isCode">The code mask of the line.</param>
        /// <param name="indentLen">The leading-space count.</param>
        /// <returns>The split segments or null.</returns>
        private List<string> TryClosedBracketSplit(string line,
            string contIndent, bool[] isCode, int indentLen)
        {
            int bracketDepth =
                BracketDepthTracker.Instance.FindBracketDepth(line,
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
        /// Attempts to split a line at a top-level assignment equals
        /// sign by wrapping the RHS in parentheses and splitting
        /// inside them. Returns the split segments, or null if this
        /// strategy does not apply.
        /// </summary>
        /// <param name="line">The line to split.</param>
        /// <param name="contIndent">The continuation indent.</param>
        /// <param name="indent">The line's leading indent.</param>
        /// <param name="isCode">The code mask of the line.</param>
        /// <param name="indentLen">The leading-space count.</param>
        /// <param name="continuesNext">Whether the next line is a continuation.</param>
        /// <returns>The split segments or null.</returns>
        private List<string> TryTopLevelEqualsSplit(string line,
            string contIndent, string indent, bool[] isCode, int indentLen,
            bool continuesNext)
        {
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
                string closeLine = indent + ")";

                var rhsSplit = SplitLongLine(rhsCont, contIndent);

                if (rhsSplit.Count == 1 &&
                    rhsSplit[0].TrimStart().Length >
                    GDScriptTextUtils.MaxLineLength)
                {
                    return null;
                }

                var res2 = new List<string> { firstLine };
                res2.AddRange(rhsSplit);
                res2.Add(closeLine);
                return CleanupEqualsWrap(res2);
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
                        return CleanupEqualsWrap(res3);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Post-processes the segments produced by the
        /// <c>=</c> wrap+split pass. Removes blank segments,
        /// glues <c>. method(</c> / <c>. method</c> onto the
        /// preceding segment as <c>.method(</c> / <c>.method</c>,
        /// and merges a chain such as <c>)</c> followed by
        /// <c>.method(...)</c> when the combined line still fits
        /// in 80 characters. When the chain does not fit, the
        /// <c>.method(...)</c> segment is kept on its own line at
        /// the same indent so the chain flows visually.
        /// </summary>
        /// <param name="segments">The wrap+split segments.</param>
        /// <returns>The cleaned segments.</returns>
        private static List<string> CleanupEqualsWrap(List<string> segments)
        {
            var result = new List<string>(segments.Count);

            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }

                if (result.Count > 0)
                {
                    string prev = result[result.Count - 1];
                    string prevTrimEnd = prev.TrimEnd();

                    if (segment.StartsWith(".") &&
                        prevTrimEnd.EndsWith(")"))
                    {
                        string curTrimmed = segment.TrimStart();
                        string merged = prevTrimEnd + curTrimmed;
                        merged = merged.Replace(". ", ".");

                        if (merged.Length <= GDScriptTextUtils.MaxLineLength)
                        {
                            result[result.Count - 1] = merged;
                            continue;
                        }
                    }
                }

                result.Add(segment);
            }

            return result;
        }

        /// <summary>
        /// Finds a safe break point after a comma inside brackets.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="isCode">The code mask of the line.</param>
        /// <param name="startIdx">The starting index.</param>
        /// <returns>The break position, or -1 if no break found.</returns>
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

                    if (bp <= GDScriptTextUtils.MaxLineLength)
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
        /// Finds the position of a top-level (outside brackets)
        /// assignment equals sign in a line (excluding ==, !=, &lt;=,
        /// &gt;=).
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="isCode">The code mask of the line.</param>
        /// <param name="startIdx">The starting index.</param>
        /// <returns>The position of the equals sign, or -1 if none.</returns>
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
        /// Attempts to split a line containing a dict/array literal as
        /// the sole argument of a method call (e.g.
        /// <c>expr({"key": value, ...})</c>). Splits at the opening
        /// brace boundary, expands each comma-separated item onto its
        /// own continuation line, and places the closing <c>})</c> on
        /// its own line at the original indent. This strategy runs
        /// after unclosed-bracket splitting and before closed-bracket
        /// comma splitting, so that dict/array literals are expanded
        /// in one step rather than fragmented by simple comma
        /// splitting.
        /// </summary>
        /// <param name="line">The line to split.</param>
        /// <param name="contIndent">The continuation indent.</param>
        /// <param name="isCode">The code mask of the line.</param>
        /// <param name="indentLen">The leading-space count.</param>
        /// <returns>The split segments or null.</returns>
        private List<string> TryBraceAlignSplit(string line,
            string contIndent, bool[] isCode, int indentLen)
        {
            int bracketDepth =
                BracketDepthTracker.Instance.FindBracketDepth(line,
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

            string first = line.Substring(0, openBrace).TrimEnd() + "{";

            if (first.Length > GDScriptTextUtils.MaxLineLength)
            {
                return null;
            }

            string rawAfterBrace = line.Substring(openBrace + 1);
            string afterBrace = rawAfterBrace.TrimStart();
            int trimmedStart = rawAfterBrace.Length - afterBrace.Length;
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
        /// Splits <paramref name="text"/> by commas that are at
        /// bracket depth 0 (i.e., not inside nested parentheses, square
        /// brackets, or braces). Uses the <paramref name="isCode"/>
        /// mask relative to the original line via
        /// <paramref name="lineOffset"/> to skip non-code regions.
        /// </summary>
        /// <param name="text">The text to split.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="lineOffset">The offset of the text in the original line.</param>
        /// <returns>The list of comma-separated items.</returns>
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
