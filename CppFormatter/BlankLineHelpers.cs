namespace CppFormatter
{
    /// <summary>
    /// Helper methods for blank-line processing.
    /// </summary>
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Determines whether a trimmed line is part of a documentation
        /// comment block: ///, /** (doc comment start), or * (continuation
        /// line inside a /** */ block, including the */ closing).
        /// </summary>
        private static bool IsDocCommentLine(string trimmed)
        {
            return trimmed.StartsWith("///") ||
                trimmed.StartsWith("/**") ||
                trimmed.StartsWith("*");
        }

        /// <summary>
        /// Determines whether a trimmed line is a plain single-line C++
        /// statement: not protected, ends with ";", not a block-end line,
        /// not a block-start line, and not a comment.
        /// </summary>
        private static bool IsPlainSingleLineStatement(string trimmed,
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

            if (TextUtils.IsBlockEndLine(trimmed))
            {
                return false;
            }

            if (TextUtils.IsBlockStartLine(trimmed))
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
