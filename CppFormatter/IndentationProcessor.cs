using System;
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

            bool[] preserveIndent = PreserveIndentComputer.Compute(lines,
                tokens);

            bool[] inEnumBlock = EnumBlockDetector.ComputeInEnumBlock(lines,
                text, isCode);

            bool[] caseBody = CaseScopeDetector.ComputeCaseScope(lines, text,
                isCode);

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
                // Special case for constructor initializer list colon:
                // When the current line starts with ':' (followed by member initializer),
                // and the previous non-blank line ends with ')', this is a constructor
                // initializer list - the colon should NOT get continuation indent.
                // It should be at the same indent level as the constructor signature.
                bool isConstructorInitializerColon = content.StartsWith(":") &&
                    content.Length > 1 &&
                    LooksLikeMemberInitializer(content.Substring(1).TrimStart());

                if (isConstructorInitializerColon)
                {
                    // Find the previous non-blank line
                    int prevLine = i - 1;

                    while (prevLine >= 0 && lines[prevLine].Trim().Length == 0)
                    {
                        prevLine--;
                    }

                    if (prevLine >= 0 && lines[prevLine].Trim().EndsWith(")"))
                    {
                        // This is a constructor initializer list colon
                        // Find the correct depth by looking for the constructor signature line
                        // The colon should be at the same indent level as the constructor signature
                        // We scan backward to find the line with the smallest indentation
                        int colonIndent = FindMinimumIndentDepth(lines, i);
                        // Use the corrected indent
                        result.Add(new string(' ', colonIndent) + content);
                        continue;
                    }
                }

                if (i > 0 && !inEnumBlock[i] && !isConstructorInitializerColon)
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
                    // Case body lines (inside switch case/default blocks)
                    // also participate in continuation detection so that
                    // continuation indentation inside case bodies is
                    // preserved across formatting passes.
                    int scanLine = i - 1;

                    while (scanLine >= 0)
                    {
                        // Skip blank lines.

                        if (lines[scanLine].Trim().Length == 0)
                        {
                            scanLine--;
                            continue;
                        }

                        // Special case: line ending with ") {" or "){"
                        // indicates multi-line parameter list closing + body opening,
                        // which should trigger continuation for body content.
                        // Exclude for-loop headers which also end with ") {".
                        string scanTrimmed = lines[scanLine].Trim();
                        // Terminate scan when we hit a statement boundary (semicolon-terminated line)

                        if (scanTrimmed.EndsWith(";"))
                        {
                            break;
                            // Stop scanning - don't apply continuation from unrelated earlier blocks
                        }

                        bool isForHeader = scanTrimmed.StartsWith("for (") ||
                            scanTrimmed.StartsWith("for(");

                        // Detect single-line function declarations vs multi-line parameter lists.
                        // Single-line declarations: "void foo() {" - ) and { adjacent, preceded by function name
                        // Multi-line params: "const X& param) {" - ) and { adjacent, preceded by parameter content
                        // Lambda multi-line: "[](..., param) {" - contains ]( and ) { pattern
                        bool isSingleLineDecl = false;
                        // Check for lambda pattern: starts with [ and contains ](

                        if (scanTrimmed.StartsWith("[") &&
                            scanTrimmed.Contains("]("))
                        {
                            // Lambda with possible multi-line params - should trigger continuation
                            // Not a single-line declaration
                        }
                        else if ((scanTrimmed.EndsWith(") {") ||
                            scanTrimmed.EndsWith("){")))
                        {
                            // Find the position of ) and { to analyze what's between them
                            int parenPos = scanTrimmed.LastIndexOf(')');
                            int bracePos = scanTrimmed.IndexOf('{', parenPos);
                            // Get the content before ) - this tells us if it's a param or a function name
                            string beforeParen = parenPos >
                                0 ? scanTrimmed.Substring(0,
                                parenPos).TrimEnd() : "";

                            // If beforeParen ends with '(' (from parameter list or function call), remove it
                            // to get the actual content (function name or last parameter)

                            if (beforeParen.EndsWith("("))
                            {
                                beforeParen = beforeParen.Substring(0,
                                    beforeParen.Length - 1).TrimEnd();
                            }

                            // Single-line function declaration: returns type + name + ()
                            // Pattern: "ReturnType FunctionName(..."
                            // Multi-line params: "const Type& param" or "Type param" or just ")"

                            if (beforeParen.Length > 0)
                            {
                                // Check if it looks like a function name (identifier) vs a parameter
                                // Function names are typically single identifiers after the return type
                                // Parameters have type qualifiers like const, &, *, or type names
                                char lastCharBeforeParen =
                                    beforeParen[beforeParen.Length - 1];

                                // If last char before ) is an identifier char, it might be a function name
                                // If it's a comma, it's definitely a multi-line param continuation
                                // If it's a type qualifier (&, *), it's a param

                                if (lastCharBeforeParen == ',')
                                {
                                    // Definitely a multi-line param continuation - not single-line decl
                                }
                                else if (TextUtils.IsWordChar(lastCharBeforeParen))
                                {
                                    // Could be function name or param name - check the start of the line
                                    // Single-line declarations: "ReturnType FuncName(" - return type followed by function name
                                    // Multi-line params: "Type param_name(" - type followed by param name
                                    // Heuristic: if starts with "std::", it's likely a param (std::vector, std::string, etc.)
                                    // If starts with "const ", it's likely a param
                                    // If starts with common return types + function name pattern, it's likely a decl

                                    if (scanTrimmed.StartsWith("std::") ||
                                        scanTrimmed.StartsWith("const ") ||
                                        scanTrimmed.StartsWith("const&") ||
                                        scanTrimmed.StartsWith("typename ") ||
                                        TextUtils.StartsWithKeyword(scanTrimmed,
                                        "const") ||
                                        scanTrimmed.Contains("std::"))
                                    {
                                        // Starts with param qualifiers or contains std:: - likely multi-line param
                                        // Do NOT set isSingleLineDecl
                                    }
                                    else if (TextUtils.StartsWithKeyword(beforeParen,
                                        "void") ||
                                        TextUtils.StartsWithKeyword(beforeParen,
                                        "int") ||
                                        TextUtils.StartsWithKeyword(beforeParen,
                                        "auto") ||
                                        TextUtils.StartsWithKeyword(beforeParen,
                                        "bool") ||
                                        TextUtils.StartsWithKeyword(beforeParen,
                                        "char") ||
                                        TextUtils.StartsWithKeyword(beforeParen,
                                        "double") ||
                                        TextUtils.StartsWithKeyword(beforeParen,
                                        "float") ||
                                        TextUtils.StartsWithKeyword(beforeParen,
                                        "size_t") ||
                                        beforeParen.StartsWith("void ") ||
                                        beforeParen.StartsWith("int ") ||
                                        beforeParen.StartsWith("auto ") ||
                                        beforeParen.StartsWith("bool ") ||
                                        beforeParen.StartsWith("char ") ||
                                        beforeParen.StartsWith("double ") ||
                                        beforeParen.StartsWith("float ") ||
                                        beforeParen.StartsWith("size_t "))
                                    {
                                        // Starts with return type - check if it looks like function name pattern
                                        // Function name pattern: ReturnType followed by identifier (no & or *)
                                        // Param pattern: Type followed by identifier with & or * or with qualifiers

                                        if (!beforeParen.Contains("&") &&
                                            !beforeParen.Contains("*"))
                                        {
                                            isSingleLineDecl = true;
                                        }
                                    }
                                }
                                else if (lastCharBeforeParen == '&' ||
                                    lastCharBeforeParen == '*')
                                {
                                    // Ends with type qualifier - definitely a param, not single-line decl
                                }
                            }
                            else
                            {
                                // Empty before ) - this is just "() {" which is a single-line empty param list
                                // But if it starts with a keyword like "void", it's a declaration

                                if (TextUtils.StartsWithKeyword(scanTrimmed,
                                    "void") ||
                                    TextUtils.StartsWithKeyword(scanTrimmed,
                                    "int") ||
                                    TextUtils.StartsWithKeyword(scanTrimmed,
                                    "auto") ||
                                    TextUtils.StartsWithKeyword(scanTrimmed,
                                    "bool"))
                                {
                                    isSingleLineDecl = true;
                                }
                            }
                        }

                        if ((scanTrimmed.EndsWith(") {") ||
                            scanTrimmed.EndsWith("){")) &&
                            !isForHeader && !isSingleLineDecl)
                        {
                            // Only apply continuation bonus for LAMBDAS (opening line contains []),
                            // not regular functions. Check if the opening line (contains opening '(')
                            // has lambda capture [].
                            bool isLambda = false;
                            int openingLine = scanLine;

                            while (openingLine >= 0)
                            {
                                string openingTrimmed =
                                    lines[openingLine].Trim();

                                if (openingTrimmed.Contains("("))
                                {
                                    // Check if this is a lambda (opening line contains [])

                                    if (openingTrimmed.Contains("["))
                                    {
                                        isLambda = true;
                                    }

                                    break;
                                }

                                openingLine--;
                            }

                            // Only apply bonus if it's a lambda AND previous line is a continuation

                            if (isLambda)
                            {
                                int prevScanLine = scanLine - 1;

                                while (prevScanLine >= 0 &&
                                    lines[prevScanLine].Trim().Length == 0)
                                {
                                    prevScanLine--;
                                }

                                if (prevScanLine >= 0)
                                {
                                    string prevTrimmed =
                                        lines[prevScanLine].Trim();

                                    // Only apply bonus if previous line ends with continuation indicator

                                    if (prevTrimmed.EndsWith(",") ||
                                        prevTrimmed.EndsWith("+") ||
                                        prevTrimmed.EndsWith("-") ||
                                        prevTrimmed.EndsWith("("))
                                    {
                                        baseDepth++;
                                    }
                                }
                            }

                            break;
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

                // Special case for closing braces in continuation bodies.
                // When current line starts with `}`, scan backward to find the
                // opening `{` and check if it has the `) {` pattern
                // (lambda/function with multi-line params).

                if (TextUtils.IsBlockEndLine(content) && i > 0)
                {
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

                        // Terminate scan when we hit a statement boundary (semicolon-terminated line)

                        if (scanLineText.EndsWith(";"))
                        {
                            break;
                            // Stop scanning - don't apply continuation from unrelated earlier blocks
                        }

                        // Count braces to find the matching opener.
                        int openBraces = CountChar(scanLineText, '{');
                        int closeBraces = CountChar(scanLineText, '}');
                        braceDepth += openBraces;
                        braceDepth -= closeBraces;
                        // Found the matching block opener.

                        if (braceDepth == 0)
                        {
                            // Check for `) {` pattern using precise EndsWith.
                            // Exclude for-loop headers which also match this pattern.
                            // Also exclude single-line function declarations.
                            bool isForHeader =
                                scanLineText.StartsWith("for (") ||
                                scanLineText.StartsWith("for(");

                            // Detect single-line function declarations (same logic as first scan loop)
                            bool isSingleLineDecl = false;

                            if (scanLineText.StartsWith("[") &&
                                scanLineText.Contains("]("))
                            {
                                // Lambda pattern - not single-line decl
                            }
                            else if ((scanLineText.EndsWith(") {") ||
                                scanLineText.EndsWith("){")))
                            {
                                int parenPos = scanLineText.LastIndexOf(')');

                                string beforeParen = parenPos >
                                    0 ? scanLineText.Substring(0,
                                    parenPos).TrimEnd() : "";

                                // If beforeParen ends with '(' (from parameter list or function call), remove it

                                if (beforeParen.EndsWith("("))
                                {
                                    beforeParen = beforeParen.Substring(0,
                                        beforeParen.Length - 1).TrimEnd();
                                }

                                if (beforeParen.Length > 0)
                                {
                                    // Same detection logic as first scan loop

                                    if (scanLineText.StartsWith("std::") ||
                                        scanLineText.StartsWith("const ") ||
                                        scanLineText.Contains("std::"))
                                    {
                                        // Likely multi-line param
                                    }
                                    else if (TextUtils.StartsWithKeyword(beforeParen,
                                        "void") ||
                                        TextUtils.StartsWithKeyword(beforeParen,
                                        "int") ||
                                        TextUtils.StartsWithKeyword(beforeParen,
                                        "auto") ||
                                        TextUtils.StartsWithKeyword(beforeParen,
                                        "bool") ||
                                        TextUtils.StartsWithKeyword(beforeParen,
                                        "char") ||
                                        TextUtils.StartsWithKeyword(beforeParen,
                                        "double") ||
                                        TextUtils.StartsWithKeyword(beforeParen,
                                        "float") ||
                                        TextUtils.StartsWithKeyword(beforeParen,
                                        "size_t") ||
                                        beforeParen.StartsWith("void ") ||
                                        beforeParen.StartsWith("int ") ||
                                        beforeParen.StartsWith("auto ") ||
                                        beforeParen.StartsWith("bool ") ||
                                        beforeParen.StartsWith("char ") ||
                                        beforeParen.StartsWith("double ") ||
                                        beforeParen.StartsWith("float ") ||
                                        beforeParen.StartsWith("size_t "))
                                    {
                                        // Starts with return type - check if no & or * (function name pattern)

                                        if (!beforeParen.Contains("&") &&
                                            !beforeParen.Contains("*"))
                                        {
                                            isSingleLineDecl = true;
                                        }
                                    }
                                }
                                else
                                {
                                    if (TextUtils.StartsWithKeyword(scanLineText,
                                        "void") ||
                                        TextUtils.StartsWithKeyword(scanLineText,
                                        "int") ||
                                        TextUtils.StartsWithKeyword(scanLineText,
                                        "auto") ||
                                        TextUtils.StartsWithKeyword(scanLineText,
                                        "bool"))
                                    {
                                        isSingleLineDecl = true;
                                    }
                                }
                            }

                            if ((scanLineText.EndsWith(") {") ||
                                scanLineText.EndsWith("){")) &&
                                !isForHeader && !isSingleLineDecl)
                            {
                                // Only apply continuation bonus for LAMBDAS (opening line contains []),
                                // not regular functions. Check if the opening line (contains opening '(')
                                // has lambda capture [].
                                bool isLambda = false;
                                int openingLine = scanLine;

                                while (openingLine >= 0)
                                {
                                    string openingTrimmed =
                                        lines[openingLine].Trim();

                                    if (openingTrimmed.Contains("("))
                                    {
                                        // Check if this is a lambda (opening line contains [])

                                        if (openingTrimmed.Contains("["))
                                        {
                                            isLambda = true;
                                        }

                                        break;
                                    }

                                    openingLine--;
                                }

                                // Only apply bonus if it's a lambda AND previous line is a continuation

                                if (isLambda)
                                {
                                    int prevScanLine = scanLine - 1;

                                    while (prevScanLine >= 0 &&
                                        lines[prevScanLine].Trim().Length == 0)
                                    {
                                        prevScanLine--;
                                    }

                                    if (prevScanLine >= 0)
                                    {
                                        string prevTrimmed =
                                            lines[prevScanLine].Trim();

                                        // Only apply bonus if previous line ends with continuation indicator

                                        if (prevTrimmed.EndsWith(",") ||
                                            prevTrimmed.EndsWith("+") ||
                                            prevTrimmed.EndsWith("-") ||
                                            prevTrimmed.EndsWith("("))
                                        {
                                            baseDepth++;
                                        }
                                    }
                                }
                            }

                            break;
                        }

                        // Stop if we find block-start keywords (class, namespace, etc.).

                        if (TextUtils.IsBlockStartLine(scanLineText))
                        {
                            break;
                        }

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
                // However, namespace alias declarations (ending with ';')
                // should not have their depth reduced.

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
        /// Also detects constructor initializer list lines starting with ':'
        /// followed by member initializer content.
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

            // Check for constructor initializer list colon:
            // Lines starting with ':' followed by member initializer content
            // Pattern: ": member_(args)" or ": member_{args}" etc.

            if (trimmed.StartsWith(":") && trimmed.Length > 1)
            {
                string afterColon = trimmed.Substring(1).TrimStart();

                if (LooksLikeMemberInitializer(afterColon))
                {
                    return true;
                }
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
        /// Determines whether a string looks like a member initializer
        /// (identifier followed by parentheses or braces for initialization).
        /// </summary>
        private static bool LooksLikeMemberInitializer(string s)
        {
            if (s.Length == 0)
            {
                return false;
            }

            // Find first '(' or '{' that follows an identifier
            int parenPos = s.IndexOf('(');
            int bracePos = s.IndexOf('{');
            int initPos = -1;

            if (parenPos >= 0 && bracePos >= 0)
            {
                initPos = Math.Min(parenPos, bracePos);
            }
            else if (parenPos >= 0)
            {
                initPos = parenPos;
            }
            else if (bracePos >= 0)
            {
                initPos = bracePos;
            }

            if (initPos <= 0)
            {
                return false;
            }

            // Check if the part before '(' or '{' is an identifier
            string beforeInit = s.Substring(0, initPos);

            return IsPureIdentifier(beforeInit) ||
                (beforeInit.EndsWith("_") && beforeInit.Length > 1 &&
                IsPureIdentifier(beforeInit.Substring(0, beforeInit.Length -
                1)));
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
        /// Counts the occurrences of a specific character in a string.
        /// </summary>
        private static int CountChar(string s, char c)
        {
            int count = 0;

            foreach (char ch in s)
            {
                if (ch == c)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Finds the correct indentation depth for a constructor initializer list colon
        /// by scanning backward to find the constructor signature start line.
        /// The colon should be at the same indent level as the constructor signature.
        /// </summary>
        private static int FindConstructorColonDepth(List<string> lines,
            int colonLineIndex,
            int[] depths)
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

                if (scanTrimmed == "public:" || scanTrimmed == "private:" ||
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
                        IsPureIdentifier(beforeParen))
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

            string[] blockKeywords = { "namespace", "struct", "switch", "catch",
                    "class", "while", "union", "enum", "else", "for", "try",
                    "do",
                "if" };

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
    }
}
