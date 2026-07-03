namespace GDScriptFormatter
{
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Determines whether a trimmed line is an elif or else block start.
        /// These are continuations of the preceding if/elif block and should
        /// not have blank lines inserted before them.
        /// </summary>
        private static bool IsElifOrElseBlock(string trimmed)
        {
            return TextUtils.StartsWithKeyword(trimmed, "elif") ||
                TextUtils.StartsWithKeyword(trimmed, "else");
        }
    }
}
