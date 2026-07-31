namespace PythonFormatter
{
    /// <summary>
    /// Central dispatcher for the per-rule blank-line checks. The
    /// per-rule logic lives in the partial method files under
    /// <c>BlankLineRules/</c>; this file documents the dispatch
    /// ordering and exposes the entry point used by
    /// <see cref="BlankLineProcessor.ApplyBlankLineRules"/>.
    /// </summary>
    internal static class BlankLineMainRules
    {
        /// <summary>Run order: add-blank rules first, suppress rules last.</summary>
        public static readonly string[] RunOrder =
            {
            "TopLevelDefClass", "Method", "FirstMethodInClass",
            "MultiLineStatement", "BlockStart", "BlockEnd",
            "ImportAfterCode", "CodeAfterImport", "PreserveAuthor",
            "SuppressBlankAbove"
        };
    }
}
