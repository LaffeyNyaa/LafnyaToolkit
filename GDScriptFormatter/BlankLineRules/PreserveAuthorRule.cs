namespace GDScriptFormatter
{
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Preserves author-inserted blank lines between adjacent plain
        /// single-line statements at the same indent. Only preserves an
        /// existing blank (HadBlankAbove); never adds one.
        /// </summary>
        private static int ApplyPreserveAuthorBlankRule(NonBlankEntry curEntry,
            string prevTrimmed, string curTrimmed)
        {
            if (curEntry.HadBlankAbove &&
                IsPlainSingleLineStatement(prevTrimmed) &&
                IsPlainSingleLineStatement(curTrimmed))
            {
                return 1;
            }

            return 0;
        }
    }
}
