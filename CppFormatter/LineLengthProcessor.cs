using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Splits lines exceeding the maximum length at safe token boundaries.
    /// </summary>
    internal static class LineLengthProcessor
    {
        /// <summary>Two-char operators whose break point sits right after
        /// the operator. Excludes &lt;&lt;/&gt;&gt; (stream ops need
        /// IsStreamOpContext) and single-char ops.</summary>
        private static readonly string[] TwoCharBreakOps =
            { "==", "!=", "<=", ">=", "=>", "+=", "-=", "&&", "||" };

        /// <summary>
        /// Splits lines exceeding 80 characters at safe token boundaries;
        /// continuation lines are indented one extra level (except after
        /// semicolons, where base indent is used). Lines entirely inside a
        /// multi-line string or comment token are preserved verbatim and
        /// never split.
        /// <paramref name="lineContinuesNext"/> flags whether each line ends
        /// with a continuation indicator; when a line is itself a continuation
        /// of the previous line, its split segments reuse the line's current
        /// indent (no extra level) so that splitting a continuation line does
        /// not cascade into deeper indents on a second pass.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full source text corresponding to
        /// <paramref name="lines"/>.</param>
        /// <param name="lineContinuesNext">Per-line flags indicating whether
        /// the line ends with a continuation indicator; entry i corresponds
        /// to line i. May be null when continuation detection is not
        /// available.</param>
        /// <returns>The processed line list.</returns>
        internal static List<string> ApplyLineLengthLimit(List<string> lines,
            string text, bool[] lineContinuesNext,
            List<Token> preTokens = null, bool[] preIsCode = null)
        {
            var tokens = preTokens ?? Tokenizer.Tokenize(text);

            bool[] isCode = preIsCode ?? Tokenizer.BuildCodeMask(text,
                tokens);

            bool[] protectedLines = Tokenizer.ComputeProtectedLines(text,
                tokens, lines.Count);

            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                if (i < protectedLines.Length && protectedLines[i])
                {
                    result.Add(lines[i]);
                    continue;
                }

                // Protect preprocessor directive lines from being split
                string trimmedLine = lines[i].TrimStart();

                if (trimmedLine.StartsWith("#"))
                {
                    result.Add(lines[i]);
                    continue;
                }

                string line = lines[i];
                // If this line is itself a continuation of the previous line
                // (previous line ends with a continuation indicator), the
                // continuation indent equals this line's current indent — do
                // NOT add another indent level. Otherwise, continuation
                // segments are indented one level deeper than the statement
                // base indent (handled by passing null to SplitLongLine).
                bool isContinuation = lineContinuesNext != null &&
                    i > 0 && i - 1 < lineContinuesNext.Length &&
                    lineContinuesNext[i - 1];

                // Attempt to unwrap stream-operator chains that were
                // previously wrapped with << at end of line (old style).
                // When the current line ends with << in stream context and
                // has continuation lines, we merge them into a single
                // expression and re-split operator-first.

                if (!isContinuation && TryUnwrapStreamChain(lines,
                    line, i, out var unwrapped) &&
                    unwrapped.Length > TextUtils.MaxLineLength)
                {
                    var split = SplitLongLine(unwrapped, null, null);
                    result.AddRange(split);
                    SkipContinuationLines(lines, lineContinuesNext, ref i);
                    continue;
                }

                if (line.Length <= TextUtils.MaxLineLength)
                {
                    result.Add(line);
                    continue;
                }

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

                var split2 = SplitLongLine(line, fixedContIndent, null);
                result.AddRange(split2);
            }

            return result;
        }

        /// <summary>
        /// Recursively splits a single line so that each segment does not
        /// exceed 80 characters. <paramref name="fixedContIndent"/> is the
        /// fixed continuation indent reused across all continuation segments
        /// so that 3+ segment splits do not cascade; pass null on the first
        /// call to trigger computation from the original line's indent.
        /// <paramref name="baseIndent"/> is the base indent of the original
        /// line; pass null on the first call, and it will be computed from
        /// the leading whitespace. When a break occurs at a semicolon in a
        /// recursive call, the continuation indent is reset to baseIndent
        /// so that trailing doc comments (/**<) use the correct indent.
        /// </summary>
        private static List<string> SplitLongLine(string line,
            string fixedContIndent, string baseIndent)
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
            // Compute baseIndent from the current line's leading whitespace
            // when it is not yet set (first call from ApplyLineLengthLimit).

            if (baseIndent == null)
            {
                baseIndent = indent;
            }

            // Tokenize the line once for all code-mask-dependent checks
            // (binary operators, safe break points).
            var tokens = Tokenizer.Tokenize(line);
            bool[] isCode = Tokenizer.BuildCodeMask(line, tokens);
            // Stream operator lines: one-pass split at all << positions

            if (HasStreamOperators(line, indentLen, out var streamPositions))
            {
                var streamResult = SplitAtStreamOperators(line,
                    streamPositions, fixedContIndent, baseIndent);
                // Recursively split the first segment if it still exceeds
                // the max line length, ensuring idempotent behavior.

                if (streamResult.Count > 0 &&
                    streamResult[0].Length > TextUtils.MaxLineLength)
                {
                    var split = SplitLongLine(streamResult[0], null, null);
                    streamResult.RemoveAt(0);
                    streamResult.InsertRange(0, split);
                }

                return streamResult;
            }

            // Binary operator lines: one-pass split at all binary operator
            // positions (+ - * / %) when the line exceeds the max length.

            if (HasBinaryOperators(line, isCode, indentLen,
                out var binaryPositions))
            {
                var binaryResult = SplitAtBinaryOperators(line,
                    binaryPositions, fixedContIndent, baseIndent);
                // Recursively split the first segment if it still exceeds
                // the max line length.  This prevents non-idempotent
                // behavior where a binary-operator split leaves the first
                // segment over 80 chars (e.g. when a comma before the
                // operator is a better break point but the binary operator
                // path fires first).

                if (binaryResult.Count > 0 &&
                    binaryResult[0].Length > TextUtils.MaxLineLength)
                {
                    var split = SplitLongLine(binaryResult[0], null, null);
                    binaryResult.RemoveAt(0);
                    binaryResult.InsertRange(0, split);
                }

                return binaryResult;
            }

            int breakAt = FindSafeBreakPoint(line, isCode, indentLen);

            if (breakAt < 0 || breakAt >= line.Length)
            {
                return new List<string> { line };
            }

            // On the first call (fixedContIndent == null), compute the fixed
            // continuation indent from the original line's indent. This indent
            // is reused for ALL continuation segments so that 3+ segment
            // splits do not cascade. When the break is at a semicolon, the
            // content after is a new structural element (e.g. trailing /**<
            // comment) and should use base indent to match
            // IndentationProcessor's expectation.
            //
            // On recursive calls (fixedContIndent != null), also re-evaluate
            // the semicolon break condition so that continuation segments
            // after a semicolon always reset to baseIndent. This ensures
            // that the /**< trailing doc comment portion always uses the
            // correct base indent regardless of how many split levels
            // preceded it.

            if (fixedContIndent == null)
            {
                if (IsSemicolonBreak(line, isCode, breakAt))
                {
                    fixedContIndent = indent;
                }
                else
                {
                    fixedContIndent = indent + new string(' ',
                        TextUtils.IndentSize);
                }
            }
            else if (IsSemicolonBreak(line, isCode, breakAt))
            {
                fixedContIndent = baseIndent;
            }

            string first = line.Substring(0, breakAt).TrimEnd();
            string rest = fixedContIndent + line.Substring(breakAt).TrimStart();

            if (first.Length == 0 || first.Length >= line.Length)
            {
                return new List<string> { line };
            }

            var result = new List<string> { first };
            result.AddRange(SplitLongLine(rest, fixedContIndent, baseIndent));
            return result;
        }

        /// <summary>
        /// Finds a safe break point within Code tokens. Additionally supports &lt;&lt; and &gt;&gt; beyond the C# break point set.
        /// </summary>
        private static int FindSafeBreakPoint(string line, bool[] isCode,
            int startIdx)
        {
            int bestInRange = -1;
            int firstOutOfRange = -1;
            int i = startIdx;

            while (i < line.Length)
            {
                if (!isCode[i])
                {
                    i++;
                    continue;
                }

                char c = line[i];
                int bp = -1;

                if (i + 1 < line.Length)
                {
                    string pair = line.Substring(i, 2);

                    foreach (var op in TwoCharBreakOps)
                    {
                        if (pair == op)
                        {
                            bp = i + 2;
                            i++;
                            break;
                        }
                    }
                }

                if (bp < 0 && c == '<' && i + 1 < line.Length &&
                    line[i + 1] == '<' &&
                    IsStreamOpContext(line, i, startIdx))
                {
                    bp = i;
                    i++;
                }
                else if (bp < 0 && c == '>' && i + 1 < line.Length &&
                    line[i + 1] == '>' &&
                    IsStreamOpContext(line, i, startIdx))
                {
                    bp = i;
                    i++;
                }
                else if (bp < 0 && c == ',')
                {
                    bp = i + 1;
                }
                else if (bp < 0 && c == ';')
                {
                    if (i + 1 < line.Length)
                    {
                        bp = i + 1;
                    }
                }
                else if (bp < 0 && i > startIdx &&
                    IsBinaryOpContext(line, i, startIdx) &&
                    (c == '+' || c == '-' || c == '*' || c == '/' ||
                    c == '%' || c == '<' || c == '>'))
                {
                    // Don't break at -> (pointer member access operator)

                    if (c == '-' && i + 1 < line.Length && line[i + 1] == '>')
                    {
                        i++;
                        continue;
                    }

                    bp = i + 1;
                }
                else if (bp < 0 && c == '=' && i > startIdx &&
                    IsBinaryOpContext(line, i, startIdx) &&
                    (i + 1 >= line.Length || (line[i + 1] != '=' &&
                    line[i + 1] != '>')))
                {
                    bp = i + 1;
                }

                if (bp > 0)
                {
                    if (bp <= TextUtils.MaxLineLength)
                    {
                        bestInRange = bp;
                    }
                    else if (firstOutOfRange < 0)
                    {
                        firstOutOfRange = bp;
                    }
                }

                i++;
            }

            if (bestInRange > 0)
            {
                return bestInRange;
            }

            return firstOutOfRange;
        }

        /// <summary>
        /// Determines whether the break point is immediately after a
        /// semicolon in code context.
        /// </summary>
        private static bool IsSemicolonBreak(string line, bool[] isCode,
            int breakAt)
        {
            if (breakAt <= 0 || breakAt > line.Length)
            {
                return false;
            }

            int semiPos = breakAt - 1;

            return semiPos < isCode.Length && isCode[semiPos] &&
                line[semiPos] == ';';
        }

        /// <summary>
        /// Determines whether a position with &lt;&lt; or &gt;&gt; is in a stream operator context:
        /// i.e., the preceding non-whitespace character is ), ], an identifier character, _, " or '.
        /// This avoids breaking inside template parameter lists (e.g., vector&lt;vector&lt;int&gt;&gt;).
        /// </summary>
        private static bool IsStreamOpContext(string line, int i,
            int startIdx)
        {
            int prev = i - 1;

            while (prev >= startIdx && line[prev] == ' ')
            {
                prev--;
            }

            if (prev < startIdx)
            {
                return false;
            }

            char pc = line[prev];

            return pc == ')' || pc == ']' || char.IsLetterOrDigit(pc) ||
                pc == '_' || pc == '"' || pc == '\'';
        }

        /// <summary>
        /// Scans a line for stream operator (&lt;&lt;) positions in stream context.
        /// </summary>
        private static bool HasStreamOperators(string line, int startIdx,
            out List<int> positions)
        {
            positions = new List<int>();

            for (int i = startIdx; i < line.Length - 1; i++)
            {
                if (line[i] == '<' && line[i + 1] == '<' &&
                    IsStreamOpContext(line, i, startIdx))
                {
                    positions.Add(i);
                    i++;
                }
            }

            return positions.Count > 0;
        }

        /// <summary>
        /// Performs a one-pass split of a line at all stream operator positions,
        /// placing each &lt;&lt; at the start of its own continuation line.
        /// </summary>
        private static List<string> SplitAtStreamOperators(string line,
            List<int> positions, string fixedContIndent, string baseIndent)
        {
            int indentLen = 0;

            while (indentLen < line.Length && line[indentLen] == ' ')
            {
                indentLen++;
            }

            string indent = line.Substring(0, indentLen);

            string contIndent;

            if (fixedContIndent != null)
            {
                contIndent = fixedContIndent;
            }
            else
            {
                contIndent = indent + new string(' ', TextUtils.IndentSize);
            }

            var result = new List<string>();
            // First segment: everything before the first <<
            result.Add(line.Substring(0, positions[0]).TrimEnd());
            // Subsequent segments: contIndent + << + content up to next <<

            for (int j = 0; j < positions.Count; j++)
            {
                int end = (j + 1 < positions.Count)
                ? positions[j + 1]
                : line.Length;

                string segment = contIndent +
                    line.Substring(positions[j], end - positions[j])
                .TrimStart();

                result.Add(segment.TrimEnd());
            }

            return result;
        }

        /// <summary>
        /// Scans a line for binary operator (+ - * / %) positions in
        /// binary context (preceded by an expression term). Excludes
        /// pointer member access (-&gt;). Uses the code mask to skip
        /// non-code regions (comments, string literals) so that
        /// operator characters inside comments are not mistaken for
        /// actual binary operators.
        /// </summary>
        private static bool HasBinaryOperators(string line, bool[] isCode,
            int startIdx, out List<int> positions)
        {
            positions = new List<int>();

            for (int i = startIdx; i < line.Length; i++)
            {
                // Skip non-code positions (comments, string literals)

                if (i >= isCode.Length || !isCode[i])
                {
                    continue;
                }

                char c = line[i];

                if ((c == '+' || c == '-' || c == '*' || c == '/' ||
                    c == '%') &&
                    IsBinaryOpContext(line, i, startIdx))
                {
                    // Skip -> (pointer member access)

                    if (c == '-' && i + 1 < line.Length &&
                        line[i + 1] == '>')
                    {
                        i++;
                        continue;
                    }

                    positions.Add(i);
                }
            }

            return positions.Count > 0;
        }

        /// <summary>
        /// Performs a one-pass split of a line at all binary operator
        /// positions, placing each operator at the start of its own
        /// continuation line.
        /// </summary>
        private static List<string> SplitAtBinaryOperators(string line,
            List<int> positions, string fixedContIndent, string baseIndent)
        {
            int indentLen = 0;

            while (indentLen < line.Length && line[indentLen] == ' ')
            {
                indentLen++;
            }

            string indent = line.Substring(0, indentLen);

            string contIndent;

            if (fixedContIndent != null)
            {
                contIndent = fixedContIndent;
            }
            else
            {
                contIndent = indent + new string(' ', TextUtils.IndentSize);
            }

            var result = new List<string>();
            // First segment: everything before the first operator
            result.Add(line.Substring(0, positions[0]).TrimEnd());
            // Subsequent segments: contIndent + operator + content up to
            // next operator

            for (int j = 0; j < positions.Count; j++)
            {
                int end = (j + 1 < positions.Count)
                ? positions[j + 1]
                : line.Length;

                string segment = contIndent +
                    line.Substring(positions[j], end - positions[j])
                .TrimStart();

                result.Add(segment.TrimEnd());
            }

            return result;
        }

        /// <summary>
        /// Determines whether the position at line[i] is in a binary operator context.
        /// </summary>
        private static bool IsBinaryOpContext(string line, int i,
            int startIdx)
        {
            int prev = i - 1;

            while (prev >= startIdx && line[prev] == ' ')
            {
                prev--;
            }

            if (prev < startIdx)
            {
                return false;
            }

            char pc = line[prev];

            return pc == ')' || pc == ']' || char.IsLetterOrDigit(pc) ||
                pc == '_' || pc == '"';
        }

        /// <summary>
        /// Checks whether <paramref name="line"/> ends with a stream operator
        /// (&lt;&lt; or &gt;&gt;) in stream context, indicating it is the
        /// first line of a wrapped multi-line stream expression. If so,
        /// merges all continuation lines into a single unwrapped expression
        /// string returned via <paramref name="unwrapped"/>.
        /// </summary>
        private static bool TryUnwrapStreamChain(List<string> lines,
            string line, int startIndex, out string unwrapped)
        {
            string trimmed = line.TrimEnd();
            // Check if the trimmed content ends with << in stream context.
            // The << must be at a valid character position.

            if (trimmed.Length < 2)
            {
                unwrapped = null;
                return false;
            }

            if (!(trimmed[trimmed.Length - 2] == '<' &&
                trimmed[trimmed.Length - 1] == '<'))
            {
                unwrapped = null;
                return false;
            }

            // Verify stream context: preceding non-space char must be valid.
            int lastCodeIdx = trimmed.Length - 3;

            while (lastCodeIdx >= 0 && trimmed[lastCodeIdx] == ' ')
            {
                lastCodeIdx--;
            }

            if (lastCodeIdx < 0)
            {
                unwrapped = null;
                return false;
            }

            char pc = trimmed[lastCodeIdx];

            if (!(pc == ')' || pc == ']' || char.IsLetterOrDigit(pc) ||
                pc == '_' || pc == '"' || pc == '\''))
            {
                unwrapped = null;
                return false;
            }

            // Collect continuation lines: lines with greater indent than
            // the current line, until we hit a line with same-or-less
            // indent or the end of the list.
            int indentLen = CountLeadingSpaces(line);

            var parts = new List<string>();
            // Strip trailing << from the first line.
            parts.Add(line.Substring(0, trimmed.Length - 2).TrimEnd());

            int j = startIndex + 1;

            while (j < lines.Count)
            {
                string next = lines[j];
                // Blank lines break the chain.

                if (string.IsNullOrWhiteSpace(next))
                {
                    break;
                }

                int nextIndent = CountLeadingSpaces(next);
                // Continuation must have greater indent.

                if (nextIndent <= indentLen)
                {
                    break;
                }

                // Strip trailing << from this continuation part.
                string nextTrimmed = next.TrimEnd();

                if (nextTrimmed.EndsWith("<<") && nextTrimmed.Length >= 2)
                {
                    nextTrimmed = nextTrimmed.Substring(0,
                        nextTrimmed.Length - 2).TrimEnd();
                }

                parts.Add(nextTrimmed.TrimStart());
                j++;
            }

            // No continuation lines found.

            if (parts.Count <= 1)
            {
                unwrapped = null;
                return false;
            }

            // Build the combined (unwrapped) expression.
            var sb = new System.Text.StringBuilder(parts[0]);

            for (int k = 1; k < parts.Count; k++)
            {
                string part = parts[k];
                // Skip empty parts (e.g. a standalone << on its own line
                // that was stripped to nothing) — the connector from the
                // previous part already provides the <<.

                if (part.Length == 0)
                {
                    continue;
                }

                if (part.StartsWith("<<") || part.StartsWith(">>"))
                {
                    sb.Append(' ');
                    sb.Append(part);
                }
                else
                {
                    sb.Append(" << ");
                    sb.Append(part);
                }
            }

            unwrapped = sb.ToString();
            return true;
        }

        /// <summary>
        /// Advances <paramref name="i"/> past all continuation lines
        /// following the current line, so the outer loop skips them.
        /// Uses indent-based detection (continuation has greater indent
        /// than the first line of the chain).
        /// </summary>
        private static void SkipContinuationLines(List<string> lines,
            bool[] lineContinuesNext, ref int i)
        {
            int indentLen = CountLeadingSpaces(lines[i]);

            while (i + 1 < lines.Count)
            {
                string next = lines[i + 1];

                if (string.IsNullOrWhiteSpace(next))
                {
                    break;
                }

                int nextIndent = CountLeadingSpaces(next);

                if (nextIndent > indentLen)
                {
                    i++;
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Counts the number of leading space characters in a line.
        /// </summary>
        private static int CountLeadingSpaces(string line)
        {
            int count = 0;

            while (count < line.Length && line[count] == ' ')
            {
                count++;
            }

            return count;
        }
    }
}
