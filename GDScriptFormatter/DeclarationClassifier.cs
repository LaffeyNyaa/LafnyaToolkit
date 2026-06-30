namespace GDScriptFormatter
{
    /// <summary>
    /// Classifies GDScript lines by their declaration type.
    /// </summary>
    internal static class DeclarationClassifier
    {
        /// <summary>
        /// Determines whether a line is a declaration line (func/class/signal/enum/const/var/annotation).
        /// </summary>
        internal static bool IsDeclarationLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "func"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "class ") &&
                !TextUtils.StartsWithKeyword(trimmed, "class_name"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "signal"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "enum"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "const"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "var"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "static"))
            {
                return true;
            }

            if (trimmed.StartsWith("@"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a line is a func or nested class declaration.
        /// </summary>
        internal static bool IsFuncOrClassDecl(string trimmed)
        {
            if (TextUtils.StartsWithKeyword(trimmed, "func"))
            {
                return true;
            }

            // Handle "static func" prefix

            if (trimmed.StartsWith("static ") &&
                TextUtils.StartsWithKeyword(trimmed.Substring("static ".Length).TrimStart(),
                "func"))
            {
                return true;
            }

            if (trimmed.StartsWith("class ") &&
                !trimmed.StartsWith("class_name"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a line is a file-level header line (@tool/@icon/@static_unload/class_name/extends/## doc).
        /// </summary>
        internal static bool IsFileHeaderLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "@tool"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "@icon"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "@static_unload"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "class_name"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "extends"))
            {
                return true;
            }

            if (trimmed.StartsWith("##"))
            {
                return true;
            }

            return false;
        }
    }
}
