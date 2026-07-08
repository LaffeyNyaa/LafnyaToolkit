namespace CppFormatter
{
    /// <summary>
    /// Classifies declaration patterns in C++ source lines:
    /// single-line function declarations, for-loop headers,
    /// and lambda capture lines.
    /// </summary>
    internal static class DeclarationClassifier
    {
        /// <summary>
        /// Determines whether a trimmed line is a single-line function
        /// declaration (e.g. "void foo() {") rather than a multi-line
        /// parameter list continuation or lambda.
        ///
        /// Single-line pattern: "ReturnType FuncName(...) {" — the content
        /// before the closing parenthesis looks like a return type followed
        /// by a function name, not like a parameter declaration.
        /// </summary>
        internal static bool IsSingleLineFunctionDeclaration(
            string scanTrimmed)
        {
            // Lambda lines starting with [ and containing ]( are never
            // single-line function declarations.

            if (scanTrimmed.StartsWith("[") &&
                scanTrimmed.Contains("]("))
            {
                return false;
            }

            if (!scanTrimmed.EndsWith(") {") &&
                !scanTrimmed.EndsWith("){"))
            {
                return false;
            }

            int parenPos = scanTrimmed.LastIndexOf(')');
            string beforeParen = parenPos > 0
            ? scanTrimmed.Substring(0, parenPos).TrimEnd()
            : "";

            if (beforeParen.EndsWith("("))
            {
                beforeParen = beforeParen.Substring(0,
                    beforeParen.Length - 1).TrimEnd();
            }

            // No content before () — check if the line starts with a
            // return type keyword (e.g. "void () {").

            if (beforeParen.Length == 0)
            {
                return TextUtils.StartsWithKeyword(scanTrimmed, "void") ||
                    TextUtils.StartsWithKeyword(scanTrimmed, "int") ||
                    TextUtils.StartsWithKeyword(scanTrimmed, "auto") ||
                    TextUtils.StartsWithKeyword(scanTrimmed, "bool");
            }

            char lastChar = beforeParen[beforeParen.Length - 1];
            // Comma before ) means multi-line param continuation.

            if (lastChar == ',')
            {
                return false;
            }

            // Reference/pointer qualifier means it is a parameter.

            if (lastChar == '&' || lastChar == '*')
            {
                return false;
            }

            // Word character before ) — could be function name or param name.

            if (!TextUtils.IsWordChar(lastChar))
            {
                return false;
            }

            // Lines starting with param qualifiers are likely multi-line
            // parameter continuations.

            if (scanTrimmed.StartsWith("std::") ||
                scanTrimmed.StartsWith("const ") ||
                scanTrimmed.StartsWith("const&") ||
                scanTrimmed.StartsWith("typename ") ||
                TextUtils.StartsWithKeyword(scanTrimmed, "const") ||
                scanTrimmed.Contains("std::"))
            {
                return false;
            }

            // Check for return-type keyword at start of beforeParen.
            // If the part before ) starts with a return type and does
            // not contain '&' or '*', it is a function name pattern.
            bool hasReturnType =
                TextUtils.StartsWithKeyword(beforeParen, "void") ||
                TextUtils.StartsWithKeyword(beforeParen, "int") ||
                TextUtils.StartsWithKeyword(beforeParen, "auto") ||
                TextUtils.StartsWithKeyword(beforeParen, "bool") ||
                TextUtils.StartsWithKeyword(beforeParen, "char") ||
                TextUtils.StartsWithKeyword(beforeParen, "double") ||
                TextUtils.StartsWithKeyword(beforeParen, "float") ||
                TextUtils.StartsWithKeyword(beforeParen, "size_t") ||
                beforeParen.StartsWith("void ") ||
                beforeParen.StartsWith("int ") ||
                beforeParen.StartsWith("auto ") ||
                beforeParen.StartsWith("bool ") ||
                beforeParen.StartsWith("char ") ||
                beforeParen.StartsWith("double ") ||
                beforeParen.StartsWith("float ") ||
                beforeParen.StartsWith("size_t ");

            if (!hasReturnType)
            {
                return false;
            }

            return !beforeParen.Contains("&") && !beforeParen.Contains("*");
        }

        /// <summary>
        /// Detects whether a line contains a lambda capture pattern.
        /// Returns true if the line contains '[' (indicating a lambda capture).
        /// </summary>
        internal static bool IsLambdaLine(string line)
        {
            return line.Contains("[");
        }

        /// <summary>
        /// Detects whether a trimmed line is a for-loop header.
        /// </summary>
        internal static bool IsForLoopHeader(string trimmed)
        {
            return trimmed.StartsWith("for (") ||
                trimmed.StartsWith("for(");
        }
    }
}
