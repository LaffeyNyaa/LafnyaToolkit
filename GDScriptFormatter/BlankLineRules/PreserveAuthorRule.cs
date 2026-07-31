using System.Collections.Generic;

namespace GDScriptFormatter
{
    /// <summary>
    /// Preserve-author rule: preserves author-inserted blank lines
    /// between adjacent plain single-line statements at the same
    /// indent. Only preserves an existing blank (HadBlankAbove);
    /// never adds one.
    /// </summary>
    public sealed partial class BlankLineProcessor
    {
        private static int ApplyPreserveAuthorBlankRule(List<bool>
            hadBlankAbove,
            List<bool> contList, int curIdx, string prevTrimmed,
            string curTrimmed)
        {
            if (!hadBlankAbove[curIdx] ||
                !IsPlainSingleLineStatement(prevTrimmed) ||
                !IsPlainSingleLineStatement(curTrimmed))
            {
                return 0;
            }

            if (curIdx > 0 && contList[curIdx] && contList[curIdx - 1])
            {
                return 0;
            }

            return 1;
        }
    }
}
