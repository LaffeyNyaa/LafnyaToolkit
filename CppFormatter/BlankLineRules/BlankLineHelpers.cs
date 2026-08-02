using LafnyaToolkit.Core.Text;

namespace CppFormatter
{
    /// <summary>
    /// Shared predicate helpers used by the per-rule blank-line
    /// partial classes under <c>BlankLineRules/</c>. All methods
    /// are stateless static utilities.
    /// </summary>
    internal static class BlankLineHelpers
    {
        /// <summary>
        /// Determines whether a trimmed line is part of a
        /// documentation comment block: <c>///</c>, <c>/**</c>
        /// (doc comment start), or <c>*</c> (continuation line
        /// inside a <c>/** */</c> block, including the <c>*/</c>
        /// closing).
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>True if the line is a doc comment line.</returns>
        public static bool IsDocCommentLine(string trimmed)
        {
            return trimmed.StartsWith("///") ||
                trimmed.StartsWith("/**") ||
                trimmed.StartsWith("*");
        }

        /// <summary>
        /// Determines whether a trimmed preprocessor line is a
        /// <c>#define</c> that defines a value (e.g.
        /// <c>#define MAX_SIZE 1024</c>) as opposed to an empty
        /// include-guard-style <c>#define</c> (e.g.
        /// <c>#define MY_HEADER_H</c>). Returns false for non-
        /// <c>#define</c> lines.
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>True if the line is a value-bearing <c>#define</c>.</returns>
        public static bool IsDefineWithValue(string trimmed)
        {
            if (!trimmed.StartsWith("#define"))
            {
                return false;
            }

            string rest = trimmed.Substring("#define".Length).TrimStart();

            if (rest.Length == 0)
            {
                return false;
            }

            int nameEnd = 0;

            while (nameEnd < rest.Length &&
                (char.IsLetterOrDigit(rest[nameEnd]) ||
                    rest[nameEnd] == '_'))
            {
                nameEnd++;
            }

            if (nameEnd == 0)
            {
                return false;
            }

            string afterName = rest.Substring(nameEnd).TrimStart();

            if (afterName.Length == 0)
            {
                return false;
            }

            if (afterName.StartsWith("//") || afterName.StartsWith("/*"))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether a trimmed line is a plain single-line
        /// C++ statement: not protected, ends with <c>;</c>, not a
        /// block-end line, not a block-start line, and not a comment.
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <param name="isProtected">Whether the line is inside a multi-line string or comment token.</param>
        /// <returns>True if the line is a plain single-line statement.</returns>
        public static bool IsPlainSingleLineStatement(string trimmed,
            bool isProtected)
        {
            if (isProtected)
            {
                return false;
            }

            if (!trimmed.EndsWith(";"))
            {
                return false;
            }

            if (CppLineClassifier.Instance.IsBlockEndLine(trimmed))
            {
                return false;
            }

            if (CppLineClassifier.Instance.IsBlockStartLine(trimmed))
            {
                return false;
            }

            if (TextUtils.IsCommentLine(trimmed))
            {
                return false;
            }

            return true;
        }
    }
}
