namespace GDScriptFormatter
{
    /// <summary>
    /// Elif/else rule: identifies elif/else block starts so that the
    /// main rule loop can suppress blank lines before them (they are
    /// continuations of the preceding if/elif block).
    /// </summary>
    public sealed partial class BlankLineProcessor
    {
        private static bool IsElifOrElseBlock(string trimmed)
        {
            return LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(trimmed, "elif") ||
                LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(trimmed, "else");
        }
    }
}
