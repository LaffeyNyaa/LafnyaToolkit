using System.Collections.Generic;

using LafnyaToolkit.Core.Text;

namespace CppFormatter
{
    /// <summary>
    /// Recomputes indentation for each line based on nesting depth,
    /// continuation indicators, enum-block membership, and switch
    /// case scope. Also trims blank lines inside namespace bodies.
    /// Stateless; the shared instance is exposed via
    /// <see cref="Instance"/>.
    /// </summary>
    internal sealed class IndentationProcessor
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly IndentationProcessor Instance =
            new IndentationProcessor();

        private static readonly string[] BlockStartKeywords =
            {
            "namespace", "struct", "switch", "catch", "class", "while",
            "union", "enum", "else", "for", "try", "do", "if"
        };

        private IndentationProcessor()
        {
        }

        /// <summary>
        /// Recomputes leading whitespace for each line based on nesting
        /// depth. Lines fully inside a VerbatimString or MultiLineComment
        /// token (but not the first line of such a token) preserve their
        /// original leading whitespace to avoid damaging string/comment
        /// content.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full source text corresponding to <paramref name="lines"/>.</param>
        /// <param name="tokens">Pre-computed tokens of <paramref name="text"/>.</param>
        /// <param name="isCode">Pre-computed code mask of <paramref name="text"/>.</param>
        /// <returns>The re-indented line list.</returns>
        public List<string> Reindent(List<string> lines, string text, List<
            LafnyaToolkit.Core.Tokenization.Token> tokens, bool[] isCode)
        {
            int[] depths =
                IndentationDepthComputer.Instance.ComputeDepths(lines, text,
                isCode);

            bool[] preserveIndent =
                PreserveIndentComputer.Instance.Compute(lines, tokens);

            bool[] inEnumBlock =
                EnumBlockDetector.Instance.ComputeInEnumBlock(lines, text,
                isCode);

            bool[] caseBody = CaseScopeDetector.Instance.ComputeCaseScope(lines,
                text, isCode);

            int[] lineStarts = CppTokenizer.Instance.ComputeLineStarts(lines);

            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                result.Add(ComputeIndentedLine(i, lines, depths, preserveIndent,
                    inEnumBlock, caseBody, text, isCode, lineStarts));
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
        private string ComputeIndentedLine(int i, List<string> lines,
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

            if (IsPreprocessorConditionalDirective(content))
            {
                return new string(' ', depths[i] * TextUtils.IndentSize) +
                    content;
            }

            int baseDepth = depths[i];

            bool isConstructorColon = content.StartsWith(":") &&
                content.Length > 1 &&
                CppTextUtils.Instance.LooksLikeMemberInitializer(content.Substring(1).TrimStart());

            if (isConstructorColon)
            {
                string handled = TryConstructorColon(i, lines, depths, content);

                if (handled != null)
                {
                    return handled;
                }
            }

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

            if (!foundBackwardContinuation)
            {
                if (content.StartsWith("<<") || content.StartsWith(">>"))
                {
                    baseDepth = ComputeStreamOperatorDepth(i, lines, baseDepth);
                }
                else if (content.Length > 1)
                {
                    char first = content[0];

                    if ((first == '+' || first == '-' || first == '*' ||
                        first == '/' || first == '%') && content[1] == ' ')
                    {
                        baseDepth = ComputeBinaryOpDepth(i, lines, baseDepth,
                            first);
                    }
                }
            }

            baseDepth = AdjustClosingBraceDepth(i, lines, text, isCode,
                baseDepth);

            if (caseBody[i])
            {
                baseDepth++;
            }

            if (TextUtils.StartsWithKeyword(content, "namespace") &&
                !content.TrimEnd().EndsWith(";"))
            {
                baseDepth = baseDepth > 0 ? baseDepth - 1 : 0;
            }

            if (content == "public:" || content == "private:" || content ==
                "protected:")
            {
                baseDepth = baseDepth > 0 ? baseDepth - 1 : 0;
            }

            return new string(' ', baseDepth * TextUtils.IndentSize) + content;
        }

        /// <summary>
        /// Handles constructor initializer list colon lines. Returns the
        /// indented line string, or null if the line is not a
        /// constructor initializer list colon.
        /// </summary>
        private string TryConstructorColon(int i, List<string> lines,
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
        /// preceding code-carrying line ends with a continuation
        /// operator, increments the base depth. Stops at statement
        /// boundaries (semicolon-terminated lines) and code-carrying
        /// lines that are not continuations.
        /// </summary>
        private int ApplyContinuationScan(int i, List<string> lines,
            int[] lineStarts, string text, bool[] isCode, int baseDepth)
        {
            int scanLine = i - 1;

            while (scanLine >= 0)
            {
                if (lines[scanLine].Trim().Length == 0)
                {
                    scanLine--;
                    continue;
                }

                string scanTrimmed = lines[scanLine].Trim();

                if (scanTrimmed.EndsWith(";"))
                {
                    break;
                }

                bool isForHeader =
                    DeclarationClassifier.Instance.IsForLoopHeader(scanTrimmed);

                bool isSingleLineDecl =
                    DeclarationClassifier.Instance.IsSingleLineFunctionDeclaration(scanTrimmed);

                if ((scanTrimmed.EndsWith(") {") ||
                    scanTrimmed.EndsWith("){")) && !isForHeader &&
                    !isSingleLineDecl)
                {
                    TryApplyLambdaContinuation(lines, scanLine, ref baseDepth);
                    break;
                }

                if (ContinuationScanner.Instance.IsContinuationIndicator(lines[scanLine],
                    lineStarts[scanLine], text, isCode))
                {
                    baseDepth++;
                    break;
                }

                if (ContinuationScanner.Instance.HasCodeChar(lines[scanLine],
                    lineStarts[scanLine], text, isCode))
                {
                    break;
                }

                scanLine--;
            }

            return baseDepth;
        }

        /// <summary>
        /// When the current line starts with a closing brace (<c>}</c>),
        /// scans backward to find the matching opening <c>{</c> and
        /// checks whether the opener has a <c>) {"</c> pattern
        /// (multi-line parameter block). If the block is a lambda with
        /// a continuation indicator on the line before it, increments
        /// the base depth so the closing brace aligns with the
        /// continuation-adjusted content.
        /// </summary>
        private int AdjustClosingBraceDepth(int i, List<string> lines,
            string text, bool[] isCode, int baseDepth)
        {
            string trimmed = lines[i].TrimStart();

            if (!CppLineClassifier.Instance.IsBlockEndLine(trimmed) || i <= 0)
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

                if (scanLineText.EndsWith(";"))
                {
                    break;
                }

                int openBraces = TextUtils.CountChar(scanLineText, '{');
                int closeBraces = TextUtils.CountChar(scanLineText, '}');
                braceDepth += openBraces;
                braceDepth -= closeBraces;

                if (braceDepth == 0)
                {
                    bool isForHeader =
                        DeclarationClassifier.Instance.IsForLoopHeader(scanLineText);

                    bool isSingleLineDecl =
                        DeclarationClassifier.Instance.IsSingleLineFunctionDeclaration(scanLineText);

                    if ((scanLineText.EndsWith(") {") ||
                        scanLineText.EndsWith("){")) && !isForHeader &&
                        !isSingleLineDecl)
                    {
                        TryApplyLambdaContinuation(lines, scanLine,
                            ref baseDepth);
                    }

                    break;
                }

                if (CppLineClassifier.Instance.IsBlockStartLine(scanLineText))
                {
                    break;
                }

                scanLine--;
            }

            return baseDepth;
        }

        /// <summary>
        /// When a line ends with <c>) {"</c> (multi-line parameter block),
        /// scans backward to find the opening line that contains
        /// <c>(</c>. If that opening line is a lambda (contains <c>[</c>),
        /// scans further backward to check whether the line before the
        /// lambda ends with a continuation indicator (<c>,</c>, <c>+</c>,
        /// <c>-</c>, <c>(</c>). If so, increments <paramref name="baseDepth"/>
        /// so that the content inside the multi-line parameter block
        /// receives an extra indent.
        /// </summary>
        private void TryApplyLambdaContinuation(List<string> lines,
            int scanLine, ref int baseDepth)
        {
            int openingLine = scanLine;
            bool isLambda = false;

            while (openingLine >= 0)
            {
                string openingTrimmed = lines[openingLine].Trim();

                if (openingTrimmed.Contains("("))
                {
                    if (DeclarationClassifier.Instance.IsLambdaLine(openingTrimmed))
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

            if (!lines[scanLine].Trim().Contains("["))
            {
                return;
            }

            int prevScanLine = scanLine - 1;

            while (prevScanLine >= 0 && lines[prevScanLine].Trim().Length == 0)
            {
                prevScanLine--;
            }

            if (prevScanLine >= 0)
            {
                string prevTrimmed = lines[prevScanLine].Trim();

                if (prevTrimmed.EndsWith(",") || prevTrimmed.EndsWith("+") ||
                    prevTrimmed.EndsWith("-") || prevTrimmed.EndsWith("("))
                {
                    baseDepth++;
                }
            }
        }

        /// <summary>
        /// Finds the correct indentation depth for a constructor
        /// initializer list colon by scanning backward to find the
        /// constructor signature start line. The colon should be at
        /// the same indent level as the constructor signature.
        /// </summary>
        private int FindConstructorColonDepth(List<string> lines,
            int colonLineIndex, int[] depths)
        {
            for (int scanIdx = colonLineIndex - 1; scanIdx >= 0; scanIdx--)
            {
                string scanLine = lines[scanIdx];
                string scanTrimmed = scanLine.Trim();

                if (scanTrimmed.Length == 0)
                {
                    continue;
                }

                if (scanTrimmed == "public:" || scanTrimmed == "private:" ||
                    scanTrimmed == "protected:")
                {
                    int depth = depths[scanIdx] - 1;
                    return depth > 0 ? depth : 0;
                }

                int parenPos = scanTrimmed.IndexOf('(');

                if (parenPos >= 0 && parenPos > 0)
                {
                    string beforeParen = scanTrimmed.Substring(0,
                        parenPos).TrimEnd();

                    if (beforeParen.Contains("::") ||
                        TextUtils.IsPureIdentifier(beforeParen))
                    {
                        int depth = depths[scanIdx] - 1;
                        return depth > 0 ? depth : 0;
                    }
                }

                if (scanTrimmed.EndsWith(";") ||
                    IsBlockStartKeywordLine(scanTrimmed))
                {
                    break;
                }
            }

            int fallbackDepth = depths[colonLineIndex] >
                0 ? depths[colonLineIndex] - 1 : depths[colonLineIndex];

            return fallbackDepth > 0 ? fallbackDepth : 0;
        }

        /// <summary>
        /// Determines whether a line starts with a C++ block-start keyword.
        /// </summary>
        private static bool IsBlockStartKeywordLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            foreach (var kw in BlockStartKeywords)
            {
                if (trimmed.StartsWith(kw) && (trimmed.Length == kw.Length ||
                    (!char.IsLetterOrDigit(trimmed[kw.Length]) &&
                    trimmed[kw.Length] != '_')))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Computes the correct indent depth for a stream-operator line
        /// (<c>&lt;&lt;</c> or <c>&gt;&gt;</c>). When the previous
        /// non-blank line also starts with <c>&lt;&lt;</c>/<c>&gt;&gt;</c>,
        /// returns that previous line's indent so all chain entries
        /// share the same hanging indent. Otherwise returns
        /// <paramref name="baseDepth"/> + 1.
        /// </summary>
        private int ComputeStreamOperatorDepth(int i, List<string> lines,
            int baseDepth)
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
        /// (starting with +, -, *, /, or %). When the previous non-blank
        /// line already starts with the same binary operator, returns
        /// its indent so all operator lines stay at the same hanging
        /// indent. Otherwise returns <paramref name="baseDepth"/> + 1.
        /// </summary>
        private int ComputeBinaryOpDepth(int i, List<string> lines,
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

                if (prevContent.Length > 1 && prevContent[0] == op &&
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
        /// Determines whether the trimmed line content is a
        /// preprocessor conditional directive.
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

            while (kwEnd < afterHash.Length && char.IsLetter(afterHash[kwEnd]))
            {
                kwEnd++;
            }

            if (kwEnd == 0)
            {
                return false;
            }

            string keyword = afterHash.Substring(0, kwEnd);

            return keyword == "if" || keyword == "ifdef" || keyword ==
                "ifndef" || keyword == "elif" || keyword == "else" || keyword ==
                "endif";
        }
    }
}
