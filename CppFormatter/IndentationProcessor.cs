using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Recomputes indentation for each line based on nesting depth,
    /// continuation indicators, enum-block membership, and switch case
    /// scope. Also trims blank lines inside namespace bodies.
    /// </summary>
    internal static class IndentationProcessor
    {
        /// <summary>
        /// Recomputes leading whitespace for each line based on nesting depth.
        /// Lines fully inside a VerbatimString or MultiLineComment token (but not the first line
        /// of such a token) preserve their original leading whitespace to avoid damaging
        /// string/comment content.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full source text corresponding to
        /// <paramref name="lines"/>.</param>
        /// <param name="tokens">Pre-computed tokens of
        /// <paramref name="text"/> (avoid re-tokenization).</param>
        /// <param name="isCode">Pre-computed code mask of
        /// <paramref name="text"/>.</param>
        /// <returns>The re-indented line list.</returns>
        internal static List<string> Reindent(List<string> lines, string text,
            List<Token> tokens, bool[] isCode)
        {
            int[] depths = new int[lines.Count];
            bool[] preserveIndent = ComputePreserveIndent(lines, tokens);
            bool[] inEnumBlock = ComputeInEnumBlock(lines, text, isCode);
            bool[] caseBody = ComputeCaseScope(lines, text, isCode);
            int depth = 0;
            int lineIdx = 0;
            bool pendingNamespace = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\n')
                {
                    lineIdx++;

                    if (lineIdx < depths.Length)
                    {
                        depths[lineIdx] = depth;
                    }

                    continue;
                }

                if (isCode[i] && c == '{')
                {
                    if (pendingNamespace)
                    {
                        pendingNamespace = false;
                    }
                    else
                    {
                        depth++;
                    }
                }
                else if (isCode[i] && c == '}')
                {
                    depth--;

                    if (depth < 0)
                    {
                        depth = 0;
                    }

                    // Only update depths[lineIdx] when the closing brace
                    // reduces depth below what was recorded at the start of
                    // this line.  This prevents a `}` that merely closes a
                    // `{` _on the same line_ (e.g. inside {{"x", y}}) from
                    // overwriting the line-start depth that was correctly
                    // set by the preceding `\n` handler.

                    if (lineIdx < depths.Length)
                    {
                        int startDepth = depths[lineIdx];

                        if (depth < startDepth)
                        {
                            depths[lineIdx] = depth;
                        }
                    }
                }

                if (isCode[i] && c == 'n' &&
                    (i == 0 || !TextUtils.IsWordChar(text[i - 1])) &&
                    TextUtils.MatchesWord(text, i, "namespace"))
                {
                    pendingNamespace = true;
                }

                // Reset pendingNamespace if we encounter characters that
                // terminate a namespace declaration (; for alias, = for
                // assignment, or non-identifier chars that are not { or :).

                if (pendingNamespace && c == ';')
                {
                    pendingNamespace = false;
                }

                if (pendingNamespace && c == '=')
                {
                    pendingNamespace = false;
                }

                if (pendingNamespace && c == '(')
                {
                    pendingNamespace = false;
                }
            }

            var result = new List<string>(lines.Count);
            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

            for (int i = 0; i < lines.Count; i++)
            {
                if (preserveIndent[i])
                {
                    result.Add(lines[i]);
                    continue;
                }

                string content = lines[i].TrimStart();

                if (content.Length == 0)
                {
                    result.Add(string.Empty);
                    continue;
                }

                int baseDepth = depths[i];

                if (i > 0 && !inEnumBlock[i] && !caseBody[i])
                {
                    // Scan backward through blank lines AND string-only
                    // continuation lines to find the actual code line that
                    // carries the continuation indicator.
                    //
                    // Blank lines inserted by BlankLineProcessor must not
                    // break the continuation chain on subsequent passes.
                    // Similarly, pure string-literal continuation lines
                    // (e.g. "SELECT ... ") contain no code-region
                    // characters, so IsContinuationIndicator cannot detect
                    // the continuation from them alone; we must walk back
                    // to the preceding code-carrying line.
                    int scanLine = i - 1;

                    while (scanLine >= 0)
                    {
                        // Skip blank lines.

                        if (lines[scanLine].Trim().Length == 0)
                        {
                            scanLine--;
                            continue;
                        }

                        if (IsContinuationIndicator(lines[scanLine],
                            lineStarts[scanLine], text, isCode))
                        {
                            baseDepth++;
                            break;
                        }

                        // If this line has at least one code-region
                        // character, it terminates the backward scan.
                        // A line with code that is NOT a continuation
                        // indicator is a statement boundary.

                        if (HasCodeChar(lines[scanLine],
                            lineStarts[scanLine], text, isCode))
                        {
                            break;
                        }

                        // This line has no code-region characters
                        // (e.g. pure string continuation). Continue
                        // scanning backward.
                        scanLine--;
                    }
                }

                if (caseBody[i])
                {
                    baseDepth++;
                }

                // Consecutive namespace declarations are kept at the same
                // indentation level as their enclosing block's content:
                // reduce namespace keyword depth by 1.

                if (TextUtils.StartsWithKeyword(content, "namespace"))
                {
                    baseDepth = baseDepth > 0 ? baseDepth - 1 : 0;
                }

                if (content == "public:" || content == "private:" || content ==
                    "protected:")
                {
                    baseDepth = baseDepth > 0 ? baseDepth - 1 : 0;
                }

                result.Add(new string(' ', baseDepth * TextUtils.IndentSize) +
                    content);
            }

            return result;
        }

        /// <summary>
        /// Determines whether the given line ends with a continuation indicator.
        /// Scans backward for the last code-region non-whitespace character so
        /// that trailing comments do not mask the real indicator. Recognized
        /// operators: <c>,</c>, <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>,
        /// <c>%</c>, <c>(</c>, <c>=</c>, <c>?</c>, <c>&lt;</c>, <c>&gt;</c>,
        /// <c>:</c> (unless a label), <c>&amp;&amp;</c>, <c>||</c>.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The starting offset of this line in
        /// <paramref name="text"/>.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>true if the line ends with a continuation indicator;
        /// otherwise false.</returns>
        internal static bool IsContinuationIndicator(string line, int lineStart,
            string text, bool[] isCode)
        {
            int lastCodeIdx = LastCodeCharIndex(line, lineStart, text,
                isCode);

            if (lastCodeIdx < 0)
            {
                return false;
            }

            char last = line[lastCodeIdx];

            if (last == ',' || last == '+' || last == '-' || last == '*' ||
                last == '/' || last == '%' || last == '(' || last == '=' ||
                last == '?' || last == '<' || last == '>')
            {
                return true;
            }

            if (last == ':')
            {
                return !IsLabelLine(line.Substring(0, lastCodeIdx + 1));
            }

            if (lastCodeIdx < 1)
            {
                return false;
            }

            int prevTextPos = lineStart + lastCodeIdx - 1;

            if (prevTextPos < 0 || prevTextPos >= isCode.Length ||
                !isCode[prevTextPos])
            {
                return false;
            }

            string last2 = line.Substring(lastCodeIdx - 1, 2);
            return last2 == "&&" || last2 == "||";
        }

        /// <summary>
        /// Determines whether a line contains at least one code-region
        /// character (excluding whitespace). Useful for checking whether a
        /// line is a pure string/comment continuation that transparently
        /// passes the continuation chain through to the preceding line.
        /// </summary>
        private static bool HasCodeChar(string line, int lineStart,
            string text, bool[] isCode)
        {
            for (int i = 0; i < line.Length; i++)
            {
                int textPos = lineStart + i;

                if (textPos < 0 || textPos >= isCode.Length ||
                    !isCode[textPos])
                {
                    continue;
                }

                char c = line[i];

                if (c != ' ' && c != '\t')
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the index of the last non-whitespace code-region character in
        /// the line. Scans backward from the end of <paramref name="line"/>,
        /// skipping positions whose corresponding <paramref name="isCode"/>
        /// entry is false and skipping space/tab characters. Correctly handles
        /// trailing comments (e.g., <c>code, // comment</c>).
        /// </summary>
        private static int LastCodeCharIndex(string line, int lineStart,
            string text, bool[] isCode)
        {
            for (int i = line.Length - 1; i >= 0; i--)
            {
                int textPos = lineStart + i;

                if (textPos < 0 || textPos >= isCode.Length ||
                    !isCode[textPos])
                {
                    continue;
                }

                char c = line[i];

                if (c == ' ' || c == '\t')
                {
                    continue;
                }

                return i;
            }

            return -1;
        }

        /// <summary>
        /// Determines whether a line that ends with ':' is a label line
        /// (access specifier, default label, case label, or plain identifier
        /// label) rather than a ternary-operator continuation.
        /// The input is fully trimmed (both leading and trailing) to handle
        /// re-indented lines that carry leading whitespace.
        /// </summary>
        private static bool IsLabelLine(string line)
        {
            string trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed == "public:" || trimmed == "private:" ||
                trimmed == "protected:")
            {
                return true;
            }

            if (trimmed == "default:")
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "case"))
            {
                return true;
            }

            if (trimmed.EndsWith(":") && trimmed.Length > 1)
            {
                string label = trimmed.Substring(0, trimmed.Length - 1);

                if (IsPureIdentifier(label))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a string is a pure C++ identifier: starting with
        /// a letter or underscore and containing only letters, digits, or
        /// underscores.
        /// </summary>
        private static bool IsPureIdentifier(string s)
        {
            if (s.Length == 0)
            {
                return false;
            }

            if (!char.IsLetter(s[0]) && s[0] != '_')
            {
                return false;
            }

            foreach (char c in s)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Computes whether each line lies inside an enum block.
        /// </summary>
        private static bool[] ComputeInEnumBlock(List<string> lines,
            string text, bool[] isCode)
        {
            var inEnumBlock = new bool[lines.Count];
            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

            var enumRanges = new List<KeyValuePair<int, int>>();
            int depth = 0;
            int enumDepth = -1;
            int enumStart = -1;
            bool pendingEnum = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = text[i];

                if (c == 'e' && (i == 0 || !TextUtils.IsWordChar(text[i -
                    1])) &&
                    TextUtils.MatchesWord(text, i, "enum"))
                {
                    pendingEnum = true;
                }

                if (c == '{')
                {
                    if (pendingEnum)
                    {
                        enumStart = i;
                        enumDepth = depth + 1;
                        pendingEnum = false;
                    }

                    depth++;
                }
                else if (c == '}')
                {
                    depth--;

                    if (depth < 0)
                    {
                        depth = 0;
                    }

                    if (enumDepth >= 0 && depth < enumDepth)
                    {
                        enumRanges.Add(new KeyValuePair<int, int>(enumStart,
                            i));

                        enumStart = -1;
                        enumDepth = -1;
                    }
                }
                else if (c == ';')
                {
                    pendingEnum = false;
                }
            }

            foreach (var range in enumRanges)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    // Use <= for range.Value so that a line starting exactly
                    // at the closing brace position (e.g. "};" on its own
                    // line) is still treated as inside the enum block.
                    // Without this, the backward continuation indicator scan
                    // would see the preceding enum member's trailing comma
                    // as a continuation and incorrectly indent "};" by one
                    // extra level on every formatting pass.
                    if (lineStarts[i] > range.Key &&
                        lineStarts[i] <= range.Value)
                    {
                        inEnumBlock[i] = true;
                    }
                }
            }

            return inEnumBlock;
        }

        /// <summary>
        /// Computes which lines within a switch block belong to a case body (indented one extra level beyond the case label).
        /// </summary>
        private static bool[] ComputeCaseScope(List<string> lines, string text,
            bool[] isCode)
        {
            var caseBody = new bool[lines.Count];
            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

            var switchRanges = new List<KeyValuePair<int, int>>();
            var braceStack = new Stack<KeyValuePair<bool, int>>();
            bool pendingSwitch = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = text[i];

                if (c == 's' && (i == 0 || !TextUtils.IsWordChar(text[i -
                    1])) &&
                    TextUtils.MatchesWord(text, i, "switch"))
                {
                    pendingSwitch = true;
                }

                if (c == '{')
                {
                    braceStack.Push(new KeyValuePair<bool, int>(pendingSwitch,
                        i));

                    pendingSwitch = false;
                }
                else if (c == '}')
                {
                    if (braceStack.Count > 0)
                    {
                        var top = braceStack.Pop();

                        if (top.Key)
                        {
                            switchRanges.Add(new KeyValuePair<int, int>(
                                top.Value, i));
                        }
                    }
                }
                else if (c == ';')
                {
                    pendingSwitch = false;
                }
            }

            switchRanges.Sort((a, b) => a.Key.CompareTo(b.Key));

            foreach (var range in switchRanges)
            {
                int braceStart = range.Key;
                int braceEnd = range.Value;
                var innerRanges = new List<KeyValuePair<int, int>>();

                foreach (var r in switchRanges)
                {
                    if (r.Key > braceStart && r.Value < braceEnd)
                    {
                        innerRanges.Add(r);
                    }
                }

                bool inCaseBody = false;

                for (int li = 0; li < lines.Count; li++)
                {
                    int ls = lineStarts[li];

                    if (ls <= braceStart || ls >= braceEnd)
                    {
                        continue;
                    }

                    int lineEndPos = ls + lines[li].Length;

                    if (braceEnd >= ls && braceEnd < lineEndPos)
                    {
                        inCaseBody = false;
                        continue;
                    }

                    bool inInner = false;

                    foreach (var ir in innerRanges)
                    {
                        if (ls > ir.Key && ls < ir.Value)
                        {
                            inInner = true;
                            break;
                        }
                    }

                    if (inInner)
                    {
                        continue;
                    }

                    string trimmed = lines[li].Trim();

                    if (IsCaseLabelLine(trimmed))
                    {
                        inCaseBody = true;
                    }
                    else if (inCaseBody)
                    {
                        caseBody[li] = true;
                    }
                }
            }

            return caseBody;
        }

        /// <summary>
        /// Determines whether a line is a case/default label line for a switch.
        /// </summary>
        private static bool IsCaseLabelLine(string trimmed)
        {
            if (trimmed.Length == 0 || !trimmed.EndsWith(":"))
            {
                return false;
            }

            return TextUtils.StartsWithKeyword(trimmed, "case") ||
                TextUtils.StartsWithKeyword(trimmed, "default");
        }

        /// <summary>
        /// Computes whether each line should preserve its original leading whitespace.
        /// </summary>
        private static bool[] ComputePreserveIndent(List<string> lines,
            List<Token> tokens)
        {
            var preserveIndent = new bool[lines.Count];
            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

            int tokenPos = 0;

            foreach (var token in tokens)
            {
                int tokenStart = tokenPos;
                int tokenEnd = tokenPos + token.Text.Length;

                if (token.Kind == TokenKind.VerbatimString ||
                    token.Kind == TokenKind.MultiLineComment)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lineStarts[i] > tokenStart &&
                            lineStarts[i] < tokenEnd)
                        {
                            preserveIndent[i] = true;
                        }
                    }
                }

                tokenPos = tokenEnd;
            }

            return preserveIndent;
        }

        /// <summary>
        /// Removes blank lines immediately after the opening { and immediately before the closing } of a namespace body.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="tokens">The token list.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>The processed line list.</returns>
        internal static List<string> TrimNamespaceBodyBlankLines(
            List<string> lines, string text, List<Token> tokens,
            bool[] isCode)
        {
            var result = new List<string>(lines.Count);

            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

            var nsBlocks = new List<KeyValuePair<int, int>>();
            int braceDepth = 0;
            var braceStack = new Stack<int>();
            bool pendingNamespace = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = text[i];

                if (c == 'n' && (i == 0 || !TextUtils.IsWordChar(text[i -
                    1])) &&
                    TextUtils.MatchesWord(text, i, "namespace"))
                {
                    pendingNamespace = true;
                }

                if (c == '{')
                {
                    if (pendingNamespace)
                    {
                        nsBlocks.Add(new KeyValuePair<int, int>(i, -1));
                        braceStack.Push(nsBlocks.Count - 1);
                    }
                    else
                    {
                        braceStack.Push(-1);
                    }

                    pendingNamespace = false;
                    braceDepth++;
                }
                else if (c == '}')
                {
                    braceDepth--;

                    if (braceStack.Count > 0)
                    {
                        int idx = braceStack.Pop();

                        if (idx >= 0 && idx < nsBlocks.Count)
                        {
                            nsBlocks[idx] = new KeyValuePair<int, int>(
                                nsBlocks[idx].Key, i);
                        }
                    }

                    pendingNamespace = false;
                }
                else if (c == ';')
                {
                    pendingNamespace = false;
                }
            }

            var removeSet = new HashSet<int>();

            foreach (var block in nsBlocks)
            {
                if (block.Value < 0)
                {
                    continue;
                }

                int openBracePos = block.Key;
                int closeBracePos = block.Value;

                for (int li = 0; li < lines.Count; li++)
                {
                    int ls = lineStarts[li];

                    if (ls <= openBracePos || ls >= closeBracePos)
                    {
                        continue;
                    }

                    if (lines[li].Trim().Length != 0)
                    {
                        continue;
                    }

                    int nextNonBlank = li + 1;

                    while (nextNonBlank < lines.Count &&
                        lines[nextNonBlank].Trim().Length == 0)
                    {
                        nextNonBlank++;
                    }

                    bool isAfterOpenBrace = li > 0 &&
                        lineStarts[li - 1] + lines[li - 1].Length <=
                        openBracePos + 1;

                    if (isAfterOpenBrace)
                    {
                        removeSet.Add(li);
                    }

                    bool isBeforeCloseBrace = nextNonBlank <
                        lines.Count &&
                        lineStarts[nextNonBlank] >= closeBracePos;

                    if (isBeforeCloseBrace)
                    {
                        removeSet.Add(li);
                    }
                }
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (!removeSet.Contains(i))
                {
                    result.Add(lines[i]);
                }
            }

            return result;
        }
    }
}
