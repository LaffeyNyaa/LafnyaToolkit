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
            int[] depths = IndentationDepthComputer.ComputeDepths(lines,
                text, isCode);

            bool[] preserveIndent = PreserveIndentComputer.Compute(lines,
                tokens);

            bool[] inEnumBlock = EnumBlockDetector.ComputeInEnumBlock(lines,
                text, isCode);

            bool[] caseBody = CaseScopeDetector.ComputeCaseScope(lines, text,
                isCode);

            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                result.Add(ComputeIndentedLine(i, lines, depths,
                    preserveIndent, inEnumBlock, caseBody, text, isCode,
                    lineStarts));
            }

            return result;
        }

        /// <summary>
        /// Computes the indented line for a single line index. Handles
        /// constructor initializer list colons, continuation-indicator
        /// backward scanning, closing-brace backward matching, case body
        /// adjustment, namespace depth adjustment, and access specifier
        /// adjustment.
        /// </summary>
        private static string ComputeIndentedLine(int i, List<string> lines,
            int[] depths, bool[] preserveIndent, bool[] inEnumBlock,
            bool[] caseBody, string text, bool[] isCode, int[] lineStarts)
        {
            if (preserveIndent[i])
            {
                return lines[i];
            }

            string content = lines[i].TrimStart();

            if (content.Length == 0)
            {
                return string.Empty;
            }

            // Preprocessor conditional directives (#if/#ifdef/#ifndef/
            // #elif/#else/#endif) keep the enclosing scope depth.  The
            // depths[] array from ComputeDepths already includes the
            // correct depth for these lines, so return immediately
            // before any other adjustment can modify it.

            if (IsPreprocessorConditionalDirective(content))
            {
                return new string(' ', depths[i] * TextUtils.IndentSize) +
                    content;
            }

            int baseDepth = depths[i];

            bool isConstructorColon = content.StartsWith(":") &&
                content.Length > 1 &&
                TextUtils.LooksLikeMemberInitializer(
                content.Substring(1).TrimStart());

            // Constructor initializer list colon handling

            if (isConstructorColon)
            {
                string handled = TryConstructorColon(i, lines, depths,
                    content);

                if (handled != null)
                {
                    return handled;
                }
            }

            // Backward continuation indicator scan
            bool foundBackwardContinuation = false;

            if (i > 0 && !inEnumBlock[i] && !isConstructorColon)
            {
                int newBaseDepth = ApplyContinuationScan(i, lines, lineStarts,
                    text, isCode, baseDepth);

                if (newBaseDepth > baseDepth)
                {
                    foundBackwardContinuation = true;
                }

                baseDepth = newBaseDepth;
            }

            // Stream operator continuation: a line starting with << or >>
            // is a continuation even when the previous line does not end
            // with a continuation indicator. BUT only apply this when the
            // backward scan did NOT already detect a continuation — otherwise
            // we would double-count (the trailing << before a string literal
            // is already detected as a code-region continuation indicator).
            // When the previous non-blank line also starts with << or >>,
            // match its indent level rather than starting from baseDepth,
            // so all lines in the stream chain share the same hanging indent.

            if (!foundBackwardContinuation)
            {
                if (content.StartsWith("<<") || content.StartsWith(">>"))
                {
                    baseDepth = ComputeStreamOperatorDepth(i, lines,
                        baseDepth);
                }

                // Binary operator continuation: a continuation line starting
                // with +, -, *, /, or % (followed by a space) after operator-
                // first wrapping also needs an extra indent.  When the
                // previous non-blank line already starts with the same binary
                // operator, match its indent level to keep all operator lines
                // at the same hanging indent.

                else if (content.Length > 1)
                {
                    char first = content[0];

                    if ((first == '+' || first == '-' || first == '*' ||
                        first == '/' || first == '%') &&
                        content[1] == ' ')
                    {
                        baseDepth = ComputeBinaryOpDepth(i, lines, baseDepth,
                            first);
                    }
                }
            }

            // Closing-brace backward matching for continuation bodies
            baseDepth = AdjustClosingBraceDepth(i, lines, text, isCode,
                baseDepth);

            // Case body adjustment

            if (caseBody[i])
            {
                baseDepth++;
            }

            // Namespace depth adjustment

            if (TextUtils.StartsWithKeyword(content, "namespace") &&
                !content.TrimEnd().EndsWith(";"))
            {
                baseDepth = baseDepth > 0 ? baseDepth - 1 : 0;
            }

            // Access specifier depth adjustment

            if (content == "public:" || content == "private:" ||
                content == "protected:")
            {
                baseDepth = baseDepth > 0 ? baseDepth - 1 : 0;
            }

            return new string(' ', baseDepth * TextUtils.IndentSize) +
                content;
        }

        /// <summary>
        /// Handles constructor initializer list colon lines.
        /// When the current line starts with ':' followed by member initializer
        /// content, and the previous non-blank line ends with ')', the colon
        /// is placed at the same indent level as the constructor signature.
        /// Returns the indented line string, or null if the line is not a
        /// constructor initializer list colon (falls through to normal
        /// processing).
        /// </summary>
        private static string TryConstructorColon(int i, List<string> lines,
            int[] depths, string content)
        {
            int prevLine = i - 1;

            while (prevLine >= 0 && lines[prevLine].Trim().Length == 0)
            {
                prevLine--;
            }

            if (prevLine >= 0 && lines[prevLine].Trim().EndsWith(")"))
            {
                int colonDepth = FindConstructorColonDepth(lines, i, depths);
                int colonIndent = colonDepth * TextUtils.IndentSize;
                return new string(' ', colonIndent) + content;
            }

            return null;
        }

        /// <summary>
        /// Scans backward from line i through blank and string-only
        /// continuation lines to find a continuation indicator. If the
        /// preceding code-carrying line ends with a continuation operator,
        /// increments the base depth. Stops at statement boundaries
        /// (semicolon-terminated lines) and code-carrying lines that are not
        /// continuations.
        /// </summary>
        private static int ApplyContinuationScan(int i, List<string> lines,
            int[] lineStarts, string text, bool[] isCode, int baseDepth)
        {
            int scanLine = i - 1;

            while (scanLine >= 0)
            {
                // Skip blank lines.

                if (lines[scanLine].Trim().Length == 0)
                {
                    scanLine--;
                    continue;
                }

                string scanTrimmed = lines[scanLine].Trim();
                // Terminate scan at statement boundaries.

                if (scanTrimmed.EndsWith(";"))
                {
                    break;
                }

                bool isForHeader =
                    DeclarationClassifier.IsForLoopHeader(scanTrimmed);

                bool isSingleLineDecl =
                    DeclarationClassifier.IsSingleLineFunctionDeclaration(
                    scanTrimmed);

                // Multi-line parameter block (lambda/function with params on
                // separate lines before the opening brace).

                if ((scanTrimmed.EndsWith(") {") ||
                    scanTrimmed.EndsWith("){")) &&
                    !isForHeader && !isSingleLineDecl)
                {
                    TryApplyLambdaContinuation(lines, scanLine,
                        ref baseDepth);

                    break;
                }

                // Continuation indicator found.

                if (ContinuationScanner.IsContinuationIndicator(
                    lines[scanLine], lineStarts[scanLine], text, isCode))
                {
                    baseDepth++;
                    break;
                }

                // Code-carrying but not a continuation — statement boundary.

                if (ContinuationScanner.HasCodeChar(lines[scanLine],
                    lineStarts[scanLine], text, isCode))
                {
                    break;
                }

                // String-only continuation — keep scanning backward.
                scanLine--;
            }

            return baseDepth;
        }

        /// <summary>
        /// When the current line starts with a closing brace (<c>}</c>),
        /// scans backward to find the matching opening <c>{</c> and checks
        /// whether the opener has a <c>) {" pattern (multi-line parameter
        /// block). If the block is a lambda with a continuation indicator
        /// on the line before it, increments the base depth so the
        /// closing brace aligns with the continuation-adjusted content.
        /// </summary>
        private static int AdjustClosingBraceDepth(int i, List<string> lines,
            string text, bool[] isCode, int baseDepth)
        {
            string trimmed = lines[i].TrimStart();

            if (!TextUtils.IsBlockEndLine(trimmed) || i <= 0)
            {
                return baseDepth;
            }

            int scanLine = i - 1;
            int braceDepth = 1;

            while (scanLine >= 0)
            {
                string scanLineText = lines[scanLine].Trim();

                if (scanLineText.Length == 0)
                {
                    scanLine--;
                    continue;
                }

                // Terminate at statement boundaries.

                if (scanLineText.EndsWith(";"))
                {
                    break;
                }

                int openBraces = TextUtils.CountChar(scanLineText, '{');
                int closeBraces = TextUtils.CountChar(scanLineText, '}');
                braceDepth += openBraces;
                braceDepth -= closeBraces;
                // Found the matching block opener.

                if (braceDepth == 0)
                {
                    bool isForHeader =
                        DeclarationClassifier.IsForLoopHeader(scanLineText);

                    bool isSingleLineDecl =
                        DeclarationClassifier
                    .IsSingleLineFunctionDeclaration(scanLineText);

                    if ((scanLineText.EndsWith(") {") ||
                        scanLineText.EndsWith("){")) &&
                        !isForHeader && !isSingleLineDecl)
                    {
                        TryApplyLambdaContinuation(lines, scanLine,
                            ref baseDepth);
                    }

                    break;
                }

                // Stop at block-start keywords (class, namespace, etc.).

                if (TextUtils.IsBlockStartLine(scanLineText))
                {
                    break;
                }

                scanLine--;
            }

            return baseDepth;
        }

        /// <summary>
        /// When a line ends with <c>) {" (multi-line parameter block),
        /// scans backward to find the opening line that contains <c>(</c>.
        /// If that opening line is a lambda (contains <c>[</c>), scans
        /// further backward to check whether the line before the lambda
        /// ends with a continuation indicator (<c>,</c>, <c>+</c>,
        /// <c>-</c>, <c>(</c>). If so, increments
        /// <paramref name="baseDepth"/> so that the content inside the
        /// multi-line parameter block receives an extra indent.
        /// </summary>
        private static void TryApplyLambdaContinuation(
            List<string> lines, int scanLine, ref int baseDepth)
        {
            // Find the opening line that contains '('.
            int openingLine = scanLine;
            bool isLambda = false;

            while (openingLine >= 0)
            {
                string openingTrimmed = lines[openingLine].Trim();

                if (openingTrimmed.Contains("("))
                {
                    if (DeclarationClassifier.IsLambdaLine(openingTrimmed))
                    {
                        isLambda = true;
                    }

                    break;
                }

                openingLine--;
            }

            if (!isLambda)
            {
                return;
            }

            // Check if the line before the lambda is a continuation.
            int prevScanLine = scanLine - 1;

            while (prevScanLine >= 0 &&
                lines[prevScanLine].Trim().Length == 0)
            {
                prevScanLine--;
            }

            if (prevScanLine >= 0)
            {
                string prevTrimmed = lines[prevScanLine].Trim();

                if (prevTrimmed.EndsWith(",") ||
                    prevTrimmed.EndsWith("+") ||
                    prevTrimmed.EndsWith("-") ||
                    prevTrimmed.EndsWith("("))
                {
                    baseDepth++;
                }
            }
        }

        /// <summary>
        /// Finds the correct indentation depth for a constructor initializer list colon
        /// by scanning backward to find the constructor signature start line.
        /// The colon should be at the same indent level as the constructor signature.
        /// </summary>
        private static int FindConstructorColonDepth(List<string> lines,
            int colonLineIndex, int[] depths)
        {
            // Scan backward to find the constructor signature start line
            // (the line containing the constructor name and opening '(')
            // We look for a line that starts with an identifier followed by '(',
            // or contains '::' followed by an identifier and '('.

            for (int scanIdx = colonLineIndex - 1; scanIdx >= 0; scanIdx--)
            {
                string scanLine = lines[scanIdx];
                string scanTrimmed = scanLine.Trim();

                if (scanTrimmed.Length == 0)
                {
                    continue;
                }

                // Check if we hit an access specifier - return content level depth

                if (scanTrimmed == "public:" ||
                    scanTrimmed == "private:" ||
                    scanTrimmed == "protected:")
                {
                    // Access specifier is at class content level, which is depth - 1
                    int depth = depths[scanIdx] - 1;
                    return depth > 0 ? depth : 0;
                }

                // Check if this line looks like a constructor signature start
                // (identifier followed by '(' at the start or after qualification)
                int parenPos = scanTrimmed.IndexOf('(');

                if (parenPos >= 0 && parenPos > 0)
                {
                    string beforeParen = scanTrimmed.Substring(0,
                        parenPos).TrimEnd();

                    // Check for qualified name (Class::Class) or simple class name

                    if (beforeParen.Contains("::") ||
                        TextUtils.IsPureIdentifier(beforeParen))
                    {
                        // This looks like a constructor signature line
                        // Constructor content is at depth - 1 from class brace level
                        int depth = depths[scanIdx] - 1;
                        return depth > 0 ? depth : 0;
                    }
                }

                // Stop if we hit a block boundary (class definition, namespace, etc.)

                if (scanTrimmed.EndsWith(";") ||
                    IsBlockStartKeywordLine(scanTrimmed))
                {
                    break;
                }
            }

            // Fallback: use depths[colonLineIndex] - 1 for content level
            int fallbackDepth = depths[colonLineIndex] >
                0 ? depths[colonLineIndex] - 1 : depths[colonLineIndex];

            return fallbackDepth > 0 ? fallbackDepth : 0;
        }

        /// <summary>
        /// Determines whether a line starts with a block-start keyword.
        /// </summary>
        private static bool IsBlockStartKeywordLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            string[] blockKeywords = { "namespace", "struct", "switch",
                    "catch", "class", "while", "union", "enum", "else",
                "for", "try", "do", "if" };

            foreach (var kw in blockKeywords)
            {
                if (trimmed.StartsWith(kw) &&
                    (trimmed.Length == kw.Length ||
                    !char.IsLetterOrDigit(trimmed[kw.Length]) &&
                    trimmed[kw.Length] != '_'))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Computes the correct indent depth for a stream-operator line
        /// (&lt;&lt; or &gt;&gt;). When the current line is the FIRST
        /// &lt;&lt;/&gt;&gt; in a chain (previous non-blank line does NOT
        /// start with &lt;&lt; or &gt;&gt;), returns <paramref name="baseDepth"/> + 1
        /// so the operator gets an extra hanging indent.  When the previous
        /// non-blank line already starts with &lt;&lt; or &gt;&gt;, returns
        /// the indent depth of that previous line so all lines in the chain
        /// share the same hanging indent.
        /// </summary>
        private static int ComputeStreamOperatorDepth(int i,
            List<string> lines, int baseDepth)
        {
            int prev = i - 1;

            while (prev >= 0 && lines[prev].Trim().Length == 0)
            {
                prev--;
            }

            if (prev >= 0)
            {
                string prevContent = lines[prev].TrimStart();

                if (prevContent.StartsWith("<<") ||
                    prevContent.StartsWith(">>"))
                {
                    int prevIndent = lines[prev].Length -
                        lines[prev].TrimStart().Length;

                    return prevIndent / TextUtils.IndentSize;
                }
            }

            return baseDepth + 1;
        }

        /// <summary>
        /// Computes the correct indent depth for a binary-operator line
        /// (starting with +, -, *, /, or %). When the current line is the
        /// FIRST such operator in a chain, returns <paramref name="baseDepth"/> + 1.
        /// When the previous non-blank line already starts with the same
        /// binary operator, returns its indent depth so all operator lines
        /// stay at the same hanging indent.
        /// </summary>
        private static int ComputeBinaryOpDepth(int i, List<string> lines,
            int baseDepth, char op)
        {
            int prev = i - 1;

            while (prev >= 0 && lines[prev].Trim().Length == 0)
            {
                prev--;
            }

            if (prev >= 0)
            {
                string prevContent = lines[prev].TrimStart();

                if (prevContent.Length > 1 &&
                    prevContent[0] == op &&
                    prevContent[1] == ' ')
                {
                    int prevIndent = lines[prev].Length -
                        lines[prev].TrimStart().Length;

                    return prevIndent / TextUtils.IndentSize;
                }
            }

            return baseDepth + 1;
        }

        /// <summary>
        /// Determines whether the trimmed line content is a preprocessor
        /// conditional directive (<c>#if</c>, <c>#ifdef</c>, <c>#ifndef</c>,
        /// <c>#elif</c>, <c>#else</c>, <c>#endif</c>).
        /// </summary>
        private static bool IsPreprocessorConditionalDirective(string content)
        {
            if (content.Length == 0 || content[0] != '#')
            {
                return false;
            }

            string afterHash = content.Substring(1).TrimStart();

            if (afterHash.Length == 0)
            {
                return false;
            }

            int kwEnd = 0;

            while (kwEnd < afterHash.Length &&
                char.IsLetter(afterHash[kwEnd]))
            {
                kwEnd++;
            }

            if (kwEnd == 0)
            {
                return false;
            }

            string keyword = afterHash.Substring(0, kwEnd);

            return keyword == "if" || keyword == "ifdef" ||
                keyword == "ifndef" || keyword == "elif" ||
                keyword == "else" || keyword == "endif";
        }
    }
}
