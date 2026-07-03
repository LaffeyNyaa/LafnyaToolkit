using static GDScriptFormatter.MemberClassifier;

namespace GDScriptFormatter
{
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns 1 blank line when the current line starts a block and is not
        /// in the same group as the previous line, or when entering a block from
        /// a non-block line.
        /// </summary>
        private static int ApplyBlockStartBlankRule(string prevTrimmed,
            string curTrimmed, bool sameIndent, bool deeperThanPrev)
        {
            if (TextUtils.IsBlockStartLine(curTrimmed) &&
                !IsSameGroup(prevTrimmed, curTrimmed) && sameIndent)
            {
                return 1;
            }

            if (TextUtils.IsBlockStartLine(curTrimmed) &&
                !deeperThanPrev &&
                prevTrimmed.Length > 0 && prevTrimmed != ":" &&
                !TextUtils.EndsWithColon(prevTrimmed))
            {
                return 1;
            }

            return 0;
        }
    }
}
