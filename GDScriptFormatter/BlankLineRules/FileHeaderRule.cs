using static GDScriptFormatter.DeclarationClassifier;

namespace GDScriptFormatter
{
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns 1 blank line after a file-level header line when the
        /// current line is not itself a header and is not entering a deeper block.
        /// </summary>
        private static int ApplyFileHeaderBlankRule(string prevTrimmed,
            string curTrimmed, bool deeperThanPrev)
        {
            if (IsFileHeaderLine(prevTrimmed) &&
                !IsFileHeaderLine(curTrimmed) && !deeperThanPrev)
            {
                return 1;
            }

            return 0;
        }
    }
}
