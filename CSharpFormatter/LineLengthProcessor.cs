using System.Collections.Generic;

using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace CSharpFormatter
{
    /// <summary>
    /// Splits lines exceeding the maximum length at safe token
    /// boundaries.
    /// </summary>
    internal sealed class LineLengthProcessor
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly LineLengthProcessor Instance =
            new LineLengthProcessor();

        private LineLengthProcessor()
        {
        }

        /// <summary>
        /// Splits lines exceeding 80 characters at safe token
        /// boundaries; continuation lines are indented one extra
        /// level. <paramref name="lineContinuesNext"/> flags whether
        /// each line ends with a continuation indicator; when a line
        /// is itself a continuation of the previous line, its split
        /// segments reuse the line's current indent (no extra level)
        /// so that splitting a continuation line does not cascade
        /// into deeper indents on a second pass.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="lineContinuesNext">Per-line flags indicating whether the line ends with a continuation indicator; entry i corresponds to line i. May be null when continuation detection is not available.</param>
        /// <returns>The processed line list.</returns>
        public List<string> ApplyLineLengthLimit(
            List<string> lines,
            bool[] lineContinuesNext
        )
        {
            var result = new List<string>(lines.Count);

            bool[] inInitializer = ComputeInitializerLines(lines,
                lineContinuesNext);

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                // Check for multi-parameter '(' layout regardless of
                // line length. When a line has '(' with multiple
                // comma-separated parameters, always apply the
                // per-parameter layout with ')' on its own line.
                // This catches both single-line declarations and
                // already-broken parameter continuations.

                if (line.Length <= TextUtils.MaxLineLength)
                {
                    if (TryApplyMultiParamLayout(line, result, lines,
                        ref i))
                    {
                        continue;
                    }

                    result.Add(line);
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

                bool lineInInitializer = inInitializer != null &&
                    i < inInitializer.Length && inInitializer[i];

                var split = SplitLongLine(line, fixedContIndent,
                    lineInInitializer);

                result.AddRange(split);
            }

            return result;
        }

        /// <summary>
        /// Checks whether the line has '(' with multiple parameters
        /// and, if so, applies the per-parameter layout with ')' on
        /// its own line. Collects parameters from the current line
        /// and any continuation lines that form the rest of the
        /// parameter list. Returns true when the layout was applied.
        /// </summary>
        private static bool TryApplyMultiParamLayout(
            string line,
            List<string> result,
            List<string> allLines,
            ref int lineIndex
        )
        {
            string trimmed = line.TrimStart();

            if (trimmed.Length == 0 || trimmed[0] == '(')
            {
                return false;
            }

            int firstParen = trimmed.IndexOf('(');

            if (firstParen < 0)
            {
                return false;
            }

            // Quick scan for comma in the parameter area
            string afterParen = trimmed.Substring(firstParen + 1);
            int depth = 0;
            bool hasComma = false;
            bool hasCloseParen = false;

            for (int j = 0; j < afterParen.Length; j++)
            {
                char c = afterParen[j];

                if (c == '(' || c == '[' || c == '{')
                {
                    depth++;
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (depth == 0)
                    {
                        hasCloseParen = true;
                        break;
                    }

                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    hasComma = true;
                    break;
                }
            }

            if (!hasComma)
            {
                if (hasCloseParen)
                {
                    // Parameter list is complete with no commas — single
                    // parameter, no multi-param layout needed.
                    return false;
                }

                // No closing ')' on the same line — parameters continue
                // on the next line. Check continuation lines for commas.
                bool contHasComma = false;
                int scanIdx = lineIndex;

                while (scanIdx + 1 < allLines.Count)
                {
                    string next = allLines[scanIdx + 1];
                    string nt = next.Trim();

                    if (nt.Length == 0 || nt == "{")
                    {
                        break;
                    }

                    if (nt.Contains(","))
                    {
                        contHasComma = true;
                        break;
                    }

                    if (nt.EndsWith(")"))
                    {
                        break;
                    }

                    scanIdx++;
                }

                if (!contHasComma)
                {
                    return false;
                }
            }

            // Tokenize for proper code mask check
            var tokens = CSharpTokenizer.Instance.Tokenize(line);

            bool[] isCode = CSharpTokenizer.Instance.BuildCodeMask(line,
                tokens);

            int parenBreakAt = FindMultiParamParenBreak(line, isCode, 0);

            if (parenBreakAt <= 0)
            {
                // FindMultiParamParenBreak may return -1 when the '('
                // is at the end of the line (no commas on the same
                // line). Compute the position from firstParen.
                int indentCount = 0;

                while (indentCount < line.Length &&
                    line[indentCount] == ' ')
                {
                    indentCount++;
                }

                parenBreakAt = indentCount + firstParen + 1;
            }

            // Verify this is a method declaration, not a method call.
            string beforeParenRaw = line.Substring(0, parenBreakAt).
            TrimEnd();
            string beforeParenTrimmed = beforeParenRaw.TrimStart();

            if (!IsMethodDeclarationLine(beforeParenTrimmed))
            {
                return false;
            }

            // Collect all parameters from the current line and any
            // continuation lines
            var allParams = new List<string>();
            string beforeParen = line.Substring(0, parenBreakAt).TrimEnd();
            string afterParenFull = line.Substring(parenBreakAt);

            ExtractParameters(afterParenFull, allParams);
            // Collect parameters from continuation lines

            while (lineIndex + 1 < allLines.Count)
            {
                string next = allLines[lineIndex + 1];
                string nt = next.Trim();

                if (nt.Length == 0 || nt == "{" || nt == "}")
                {
                    break;
                }

                lineIndex++;
                ExtractParameters(nt, allParams);
            }

            if (allParams.Count == 0)
            {
                return false;
            }

            // Merge parameters with unbalanced angle brackets.
            // When a generic type parameter like "List<Insertion>" is
            // split across lines, ExtractParameters may treat "List<"
            // and "Insertion> insertions" as separate parameters.
            // Merge them back into a single parameter.

            for (int p = 0; p < allParams.Count - 1; p++)
            {
                int openAngles = TextUtils.CountChar(allParams[p], '<');
                int closeAngles = TextUtils.CountChar(allParams[p], '>');

                if (openAngles > closeAngles)
                {
                    allParams[p] = allParams[p] + allParams[p + 1];
                    allParams.RemoveAt(p + 1);
                    p--;
                }
            }

            int indentLen = 0;

            while (indentLen < line.Length &&
                line[indentLen] == ' ')
            {
                indentLen++;
            }

            string baseIndent = line.Substring(0, indentLen);

            string paramIndent = baseIndent +
                new string(' ', TextUtils.IndentSize);

            result.Add(beforeParen);

            for (int p = 0; p < allParams.Count; p++)
            {
                string param = allParams[p];
                bool isLast = p == allParams.Count - 1;

                string paramLine = paramIndent + param +
                    (isLast ? string.Empty : ",");

                if (paramLine.Length > TextUtils.MaxLineLength)
                {
                    string deepIndent = paramIndent +
                        new string(' ', TextUtils.IndentSize);

                    result.AddRange(SplitLongLine(paramLine, deepIndent,
                        false));
                }
                else
                {
                    result.Add(paramLine);
                }
            }

            result.Add(baseIndent + ")");

            return true;
        }

        /// <summary>
        /// Extracts comma-separated parameters from a parameter-list
        /// string fragment. Handles nested depth (parentheses,
        /// brackets, braces). The last parameter may include the
        /// closing ')' which is stripped.
        /// </summary>
        private static void ExtractParameters(
            string fragment,
            List<string> allParams
        )
        {
            int depth = 0;
            int paramStart = 0;

            for (int i = 0; i < fragment.Length; i++)
            {
                char c = fragment[i];

                if (c == '(' || c == '[' || c == '{' || c == '<')
                {
                    depth++;
                }
                else if (c == ')' || c == ']' || c == '}' || c == '>')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                    else if (c == ')')
                    {
                        string last = fragment.Substring(paramStart,
                            i - paramStart).Trim();

                        if (last.Length > 0)
                        {
                            allParams.Add(last);
                        }

                        return;
                    }
                }
                else if (c == ',' && depth == 0)
                {
                    string param = fragment.Substring(paramStart,
                        i - paramStart).Trim();

                    if (param.Length > 0)
                    {
                        allParams.Add(param);
                    }

                    paramStart = i + 1;
                }
            }

            // If no closing ')' was found, add remaining as a parameter
            string remaining = fragment.Substring(paramStart).Trim();

            if (remaining.Length > 0)
            {
                // Remove trailing ')' if present

                if (remaining.EndsWith(")"))
                {
                    remaining = remaining.Substring(0,
                        remaining.Length - 1).TrimEnd();
                }

                if (remaining.Length > 0)
                {
                    allParams.Add(remaining);
                }
            }
        }

        /// <summary>
        /// Recursively splits a single line so that each segment does
        /// not exceed 80 characters; splits only at Code token
        /// boundaries. <paramref name="fixedContIndent"/> is the
        /// fixed continuation indent reused across all continuation
        /// segments so that 3+ segment splits do not cascade; pass
        /// null on the first call to trigger computation from the
        /// original line's indent. <paramref name="inInitializer"/>
        /// indicates whether the line is inside an array/collection/
        /// object initializer block; when true and the line is too
        /// long, it is split per-element at top-level commas.
        /// </summary>
        /// <param name="line">The line to split.</param>
        /// <param name="fixedContIndent">The fixed continuation indent (or null on the first call).</param>
        /// <param name="inInitializer">True if the line is inside an initializer block.</param>
        /// <returns>The list of split segments.</returns>
        private static List<string> SplitLongLine(
            string line,
            string fixedContIndent,
            bool inInitializer
        )
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
            // Per-element wrapping for initializer content.
            // Must happen after indent computation since initializer
            // elements use the line's own indent level, not the
            // continuation indent.

            if (inInitializer)
            {
                var initializerResult = TrySplitInitializerLine(line,
                    indent);

                if (initializerResult != null)
                {
                    return initializerResult;
                }
            }

            if (fixedContIndent == null)
            {
                fixedContIndent = indent +
                    new string(' ', TextUtils.IndentSize);
            }

            var tokens = CSharpTokenizer.Instance.Tokenize(line);

            bool[] isCode = CSharpTokenizer.Instance.BuildCodeMask(line,
                tokens);

            int breakAt = FindSafeBreakPoint(line, isCode, indentLen);

            if (breakAt < 0 || breakAt >= line.Length)
            {
                return new List<string> { line };
            }

            // Multi-parameter layout: when the line has '(' with multiple
            // comma-separated parameters, always break at '(' and place
            // each parameter on its own line with ')' on its own line.
            // Only applies to method declarations, not method calls.
            int parenBreakAt = FindMultiParamParenBreak(line, isCode,
                indentLen);

            if (parenBreakAt > 0)
            {
                // Verify this is a method declaration
                string beforeParen = line.Substring(0, parenBreakAt).
                TrimEnd();
                string beforeParenTrimmed = beforeParen.TrimStart();

                if (IsMethodDeclarationLine(beforeParenTrimmed))
                {
                    // Only apply multi-param layout when the line has
                    // a complete parameter list (closing ')' is present
                    // on the same line). Lines that wrap at a generic
                    // angle bracket (e.g. "List<") are continuation
                    // lines from a previous split and should not be
                    // re-formatted here.
                    int closeParen = line.IndexOf(')', parenBreakAt);

                    if (closeParen >= 0)
                    {
                        string baseIndent = line.Substring(0, indentLen);

                        string paramIndent = baseIndent +
                            new string(' ', TextUtils.IndentSize);

                        return SplitParametersPerLine(line, parenBreakAt,
                            paramIndent, baseIndent);
                    }

                    // Closing ')' is not on the same line — this
                    // line is a continuation from a previous split.
                    // Avoid breaking at '<' or '>' inside generic
                    // type parameter lists. If the safe break point
                    // would break at these characters, return the
                    // line as-is instead.

                    if (breakAt > 0 && breakAt <= line.Length &&
                        (line[breakAt - 1] == '<' ||
                        line[breakAt - 1] == '>'))
                    {
                        return new List<string> { line };
                    }
                }
            }

            string afterTrimmed = line.Substring(breakAt).TrimStart();

            if (afterTrimmed.StartsWith("//") ||
                afterTrimmed.StartsWith("/*") ||
                afterTrimmed.StartsWith("*"))
            {
                fixedContIndent = indent;
            }

            string first = line.Substring(0, breakAt).TrimEnd();
            string rest = fixedContIndent + line.Substring(breakAt).TrimStart();

            if (first.Length == 0 || first.Length >= line.Length)
            {
                return new List<string> { line };
            }

            var result = new List<string> { first };
            result.AddRange(SplitLongLine(rest, fixedContIndent, false));
            return result;
        }

        /// <summary>
        /// Attempts to split an initializer line by placing each
        /// top-level comma-separated element onto its own continuation
        /// line. Returns null if the line has no top-level commas
        /// (fall through to regular splitting).
        /// </summary>
        /// <param name="line">The line to split.</param>
        /// <param name="elementIndent">The indent for each element line (same as the line's own indent).</param>
        /// <returns>The split segments, or null if no commas found.</returns>
        private static List<string> TrySplitInitializerLine(
            string line,
            string elementIndent
        )
        {
            var tokens = CSharpTokenizer.Instance.Tokenize(line);

            bool[] isCode = CSharpTokenizer.Instance.BuildCodeMask(line,
                tokens);

            // Find top-level comma positions
            var commaPositions = new List<int>();
            int depth = 0;

            for (int j = 0; j < line.Length; j++)
            {
                if (!isCode[j])
                {
                    continue;
                }

                char ch = line[j];

                if (ch == '(' || ch == '[' || ch == '{')
                {
                    depth++;
                }
                else if (ch == ')' || ch == ']' || ch == '}')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                }
                else if (ch == ',' && depth == 0)
                {
                    commaPositions.Add(j);
                }
            }

            if (commaPositions.Count == 0)
            {
                return null;
            }

            // Continuation indent for recursive splits of long elements
            string elementContIndent = elementIndent +
                new string(' ', TextUtils.IndentSize);

            var result = new List<string>(commaPositions.Count + 1);
            int start = 0;

            foreach (int commaPos in commaPositions)
            {
                string element = line.Substring(start,
                    commaPos - start).Trim();

                if (element.Length > 0)
                {
                    string elementLine = elementIndent + element + ",";

                    result.AddRange(SplitLongLine(elementLine,
                        elementContIndent, false));
                }

                start = commaPos + 1;
            }

            // Handle the last element after the last comma
            string lastElement = line.Substring(start).Trim();

            if (lastElement.Length > 0)
            {
                string lastElementLine = elementIndent + lastElement;

                result.AddRange(SplitLongLine(lastElementLine,
                    elementContIndent, false));
            }

            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// Computes which lines are inside array/collection/object
        /// initializer blocks (between { and } where { follows a
        /// continuation indicator).
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="lineContinuesNext">Per-line continuation flags; entry i corresponds to line i.</param>
        /// <returns>A boolean array; true means the line is inside an initializer block.</returns>
        private static bool[] ComputeInitializerLines(
            List<string> lines,
            bool[] lineContinuesNext
        )
        {
            var inInitializer = new bool[lines.Count];

            if (lineContinuesNext == null)
            {
                return inInitializer;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (inInitializer[i])
                {
                    continue;
                }

                // Find the first non-whitespace character of the line
                int firstNonWs = 0;

                while (firstNonWs < lines[i].Length &&
                    (lines[i][firstNonWs] == ' ' ||
                    lines[i][firstNonWs] == '\t'))
                {
                    firstNonWs++;
                }

                if (firstNonWs >= lines[i].Length)
                {
                    continue;
                }

                // The first code character must be `{`

                if (lines[i][firstNonWs] != '{')
                {
                    continue;
                }

                // The previous non-blank line must be a continuation
                int prev = i - 1;

                while (prev >= 0 && lines[prev].Trim().Length == 0)
                {
                    prev--;
                }

                if (prev < 0 || prev >= lineContinuesNext.Length ||
                    !lineContinuesNext[prev])
                {
                    continue;
                }

                // Find the matching `}` for this `{`
                int depth = 1;
                int endLine = -1;

                for (int j = i + 1; j < lines.Count && depth > 0; j++)
                {
                    string lineContent = lines[j];

                    for (int k = 0; k < lineContent.Length; k++)
                    {
                        if (lineContent[k] == '{')
                        {
                            depth++;
                        }
                        else if (lineContent[k] == '}')
                        {
                            depth--;

                            if (depth == 0)
                            {
                                endLine = j;
                                break;
                            }
                        }
                    }
                }

                if (endLine < 0)
                {
                    continue;
                }

                // Mark all lines between the opening `{` and
                // matching `}` as inside initializer

                for (int j = i + 1; j < endLine; j++)
                {
                    inInitializer[j] = true;
                }
            }

            return inInitializer;
        }

        /// <summary>
        /// Finds a safe break point within a Code token: prefers the
        /// largest break point not exceeding 80 characters; if no
        /// such point exists, returns the first break point beyond
        /// 80 characters. If the line starts with a <c>case</c> or
        /// <c>default</c> label whose colon falls inside the line,
        /// that colon is preferred as the break point so that the
        /// case label stands on its own line and the body starts on
        /// a freshly indented line.
        /// </summary>
        /// <param name="line">The line to scan.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="startIdx">The position to start scanning from.</param>
        /// <returns>The break position, or -1 if none exists.</returns>
        private static int FindSafeBreakPoint(
            string line,
            bool[] isCode,
            int startIdx
        )
        {
            int caseColonBp = TryCaseLabelBreakPoint(line, isCode, startIdx);

            if (caseColonBp > 0)
            {
                return caseColonBp;
            }

            int assignBp = TryAssignmentBreakPoint(line, isCode, startIdx);

            if (assignBp > 0)
            {
                return assignBp;
            }

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

                if ((c == '+' || c == '-') && i + 1 < line.Length &&
                    line[i + 1] == c)
                {
                    i += 2;
                    continue;
                }

                if ((c == '&' || c == '|') && i + 1 < line.Length &&
                    line[i + 1] == c)
                {
                    int bpOp = i;

                    if (bpOp <= TextUtils.MaxLineLength)
                    {
                        bestInRange = bpOp;
                    }
                    else if (firstOutOfRange < 0)
                    {
                        firstOutOfRange = bpOp;
                    }

                    i += 2;
                    continue;
                }

                int bp = -1;
                bp = TryMatchTwoCharOperator(line, i, c);

                if (bp < 0 && c == ',')
                {
                    bp = i + 1;
                }

                if (bp < 0 && c == ';' && i + 1 < line.Length)
                {
                    bp = i + 1;
                }

                if (bp < 0 && i > startIdx)
                {
                    bp = TryMatchSingleCharOp(line, i, c, startIdx);
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
        /// Detects whether the line begins with a <c>case</c> or
        /// <c>default</c> switch label and, if so, returns the break
        /// position immediately after the label's colon. Returns -1
        /// when the line is not a case label or when no colon is
        /// present.
        /// </summary>
        /// <param name="line">The line to scan.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="startIdx">The position to start scanning from.</param>
        /// <returns>The break position after the case label colon, or -1.</returns>
        private static int TryCaseLabelBreakPoint(
            string line,
            bool[] isCode,
            int startIdx
        )
        {
            int i = startIdx;

            while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
            {
                i++;
            }

            bool isCase = i + 4 <= line.Length && line[i] == 'c' &&
                line[i + 1] == 'a' && line[i + 2] == 's' &&
                line[i + 3] == 'e' &&
                (i + 4 == line.Length || !TextUtils.IsWordChar(line[i + 4]));

            bool isDefault = !isCase && i + 7 <= line.Length && line[i] ==
                'd' &&
                line[i + 1] == 'e' && line[i + 2] == 'f' &&
                line[i + 3] == 'a' && line[i + 4] == 'u' &&
                line[i + 5] == 'l' && line[i + 6] == 't' &&
                (i + 7 == line.Length || !TextUtils.IsWordChar(line[i + 7]));

            if (!isCase && !isDefault)
            {
                return -1;
            }

            int keywordLen = isCase ? 4 : 7;
            int scan = i + keywordLen;

            while (scan < line.Length && (line[scan] == ' ' ||
                line[scan] == '\t'))
            {
                scan++;
            }

            if (isDefault)
            {
                if (scan >= line.Length || line[scan] != ':' ||
                    scan >= isCode.Length || !isCode[scan])
                {
                    return -1;
                }

                return scan + 1;
            }

            int depth = 0;

            while (scan < line.Length)
            {
                if (scan < isCode.Length && !isCode[scan])
                {
                    scan++;
                    continue;
                }

                char c = line[scan];

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
                else if (c == ':' && depth == 0)
                {
                    return scan + 1;
                }

                scan++;
            }

            return -1;
        }

        /// <summary>
        /// Scans the line for a standalone <c>=</c> assignment operator
        /// (not <c>==</c>, <c>=&gt;</c>, or compound assignments) and
        /// returns the break position after the rightmost <c>=</c> that
        /// falls within the maximum line length. Returns -1 if no
        /// suitable assignment operator is found.
        /// </summary>
        private static int TryAssignmentBreakPoint(
            string line,
            bool[] isCode,
            int startIdx
        )
        {
            int bestBp = -1;

            for (int i = startIdx; i < line.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                if (line[i] == '=')
                {
                    if (i + 1 < line.Length &&
                        (line[i + 1] == '=' || line[i + 1] == '>'))
                    {
                        continue;
                    }

                    if (IsBinaryOpContext(line, i, startIdx))
                    {
                        int bp = i + 1;

                        if (bp <= TextUtils.MaxLineLength)
                        {
                            bestBp = bp;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            return bestBp;
        }

        /// <summary>
        /// Determines whether the content before the opening paren
        /// of a method parameter list is a method declaration (not
        /// a method call). A method declaration line starts with a
        /// C# keyword such as <c>public</c>, <c>private</c>,
        /// <c>static</c>, <c>virtual</c>, etc.
        /// </summary>
        /// <param name="beforeParenTrimmed">The trimmed content of
        /// the line up to and including the <c>(</c>.</param>
        /// <returns>True if the line is a method declaration.</returns>
        private static bool IsMethodDeclarationLine(
            string beforeParenTrimmed)
        {
            string text = beforeParenTrimmed.TrimEnd('(').TrimEnd();

            if (text.Length == 0)
            {
                return false;
            }

            string firstWord = text.Split(' ')[0];

            return firstWord == "public" || firstWord == "private" ||
                firstWord == "internal" || firstWord == "protected" ||
                firstWord == "static" || firstWord == "virtual" ||
                firstWord == "override" || firstWord == "abstract" ||
                firstWord == "sealed" || firstWord == "async" ||
                firstWord == "unsafe" || firstWord == "extern";
        }

        /// <summary>
        /// Scans the line for the first <c>(</c> with multiple
        /// comma-separated parameters and returns the position
        /// immediately after it. Returns -1 when no such <c>(</c>
        /// exists.
        /// </summary>
        private static int FindMultiParamParenBreak(
            string line,
            bool[] isCode,
            int startIdx
        )
        {
            for (int i = startIdx; i < line.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                if (line[i] == '(')
                {
                    // Check for multiple parameters: comma at depth 1
                    int depth = 1;
                    bool hasComma = false;

                    for (int j = i + 1; j < line.Length; j++)
                    {
                        if (!isCode[j])
                        {
                            continue;
                        }

                        char c = line[j];

                        if (c == '(' || c == '[' || c == '{')
                        {
                            depth++;
                        }
                        else if (c == ')' || c == ']' || c == '}')
                        {
                            depth--;

                            if (depth == 0)
                            {
                                break;
                            }
                        }
                        else if (c == ',' && depth == 1)
                        {
                            hasComma = true;
                            break;
                        }
                    }

                    return hasComma ? i + 1 : -1;
                }
            }

            return -1;
        }

        /// <summary>
        /// Splits the line at a parameter list start so that each
        /// parameter occupies its own continuation line. The closing
        /// <c>)</c> is placed on its own line at the base indent.
        /// </summary>
        private static List<string> SplitParametersPerLine(
            string line,
            int breakAt,
            string paramIndent,
            string baseIndent
        )
        {
            string beforeParen = line.Substring(0, breakAt).TrimEnd();
            string afterParen = line.Substring(breakAt);

            var parameters = new List<string>();
            int depth = 0;
            int paramStart = 0;
            int closeParenPos = -1;

            for (int i = 0; i < afterParen.Length; i++)
            {
                char c = afterParen[i];

                if (c == '(' || c == '[' || c == '{' || c == '<')
                {
                    depth++;
                }
                else if (c == ')' || c == ']' || c == '}' || c == '>')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                    else if (c == ')')
                    {
                        closeParenPos = i;

                        string lastParam = afterParen.Substring(paramStart,
                            i - paramStart).Trim();

                        if (lastParam.Length > 0)
                        {
                            parameters.Add(lastParam);
                        }

                        break;
                    }
                }
                else if (c == ',' && depth == 0)
                {
                    parameters.Add(afterParen.Substring(paramStart,
                        i - paramStart).Trim());

                    paramStart = i + 1;
                }
            }

            var result = new List<string>(parameters.Count + 2);
            result.Add(beforeParen);

            for (int p = 0; p < parameters.Count; p++)
            {
                string param = parameters[p];
                bool isLast = p == parameters.Count - 1;

                string paramLine = paramIndent + param +
                    (isLast ? string.Empty : ",");

                if (paramLine.Length > TextUtils.MaxLineLength)
                {
                    string deepIndent = paramIndent +
                        new string(' ', TextUtils.IndentSize);

                    result.AddRange(SplitLongLine(paramLine, deepIndent,
                        false));
                }
                else
                {
                    result.Add(paramLine);
                }
            }

            // Closing ')' on its own line at base indent
            result.Add(baseIndent + ")");
            // Capture any trailing code after ')' (e.g. "{ }")

            if (closeParenPos >= 0 && closeParenPos + 1 < afterParen.Length)
            {
                string trailing = afterParen.Substring(
                    closeParenPos + 1).TrimEnd();

                if (trailing.Length > 0)
                {
                    result.Add(baseIndent + trailing);
                }
            }

            return result;
        }

        /// <summary>
        /// Attempts to match a two-character operator at position
        /// <paramref name="i"/> and returns the break position after
        /// it.
        /// </summary>
        /// <param name="line">The line to scan.</param>
        /// <param name="i">The position of the first character of the operator.</param>
        /// <param name="c">The first character of the operator.</param>
        /// <returns>The break position after the operator, or -1 if not matched.</returns>
        private static int TryMatchTwoCharOperator(
            string line,
            int i,
            char c
        )
        {
            if (i + 1 >= line.Length)
            {
                return -1;
            }

            char next = line[i + 1];

            if (c == '=' && (next == '=' || next == '>'))
            {
                return i + 2;
            }

            if (c == '!' && next == '=')
            {
                return i + 2;
            }

            if (c == '<' && next == '=')
            {
                return i + 2;
            }

            if (c == '>' && next == '=')
            {
                return i + 2;
            }

            if (c == '+' && next == '=')
            {
                return i + 2;
            }

            if (c == '-' && next == '=')
            {
                return i + 2;
            }

            if (c == '&' && next == '&')
            {
                return i + 2;
            }

            if (c == '|' && next == '|')
            {
                return i + 2;
            }

            return -1;
        }

        /// <summary>
        /// Attempts to match a single-character binary operator at
        /// position <paramref name="i"/>.
        /// </summary>
        /// <param name="line">The line to scan.</param>
        /// <param name="i">The position of the operator.</param>
        /// <param name="c">The operator character.</param>
        /// <param name="startIdx">The position to start scanning from.</param>
        /// <returns>The break position after the operator, or -1 if not matched.</returns>
        private static int TryMatchSingleCharOp(
            string line,
            int i,
            char c,
            int startIdx
        )
        {
            bool isBinaryChar = c == '+' || c == '-' || c == '*' ||
                c == '/' || c == '%' || c == '<' || c == '>';

            if (isBinaryChar && IsBinaryOpContext(line, i, startIdx))
            {
                return i + 1;
            }

            if (c == '=' && IsBinaryOpContext(line, i, startIdx) &&
                (i + 1 >= line.Length ||
                (line[i + 1] != '=' && line[i + 1] != '>')))
            {
                return i + 1;
            }

            return -1;
        }

        /// <summary>
        /// Determines whether position <paramref name="i"/> in the
        /// line is in a binary operator context (preceded by
        /// <c>)</c>, <c>]</c>, identifier, <c>_</c>, or <c>"</c>).
        /// </summary>
        /// <param name="line">The line to inspect.</param>
        /// <param name="i">The position of the candidate operator.</param>
        /// <param name="startIdx">The position to start scanning from.</param>
        /// <returns>True if the operator is in a binary context.</returns>
        private static bool IsBinaryOpContext(
            string line,
            int i,
            int startIdx
        )
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
    }
}
