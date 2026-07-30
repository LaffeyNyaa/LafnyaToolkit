namespace GDScriptFormatter
{
    /// <summary>
    /// File-header rule: returns 1 blank line after a file-level
    /// header line when the current line is not itself a header and
    /// is not entering a deeper block.
    /// </summary>
    public sealed partial class BlankLineProcessor
    {
        private static int ApplyFileHeaderBlankRule(string prevTrimmed,
            string curTrimmed, bool deeperThanPrev)
        {
            if (DeclarationClassifier.Instance.IsFileHeaderLine(prevTrimmed) &&
                !DeclarationClassifier.Instance.IsFileHeaderLine(curTrimmed) && !deeperThanPrev)
            {
                return 1;
            }

            return 0;
        }
    }
}
