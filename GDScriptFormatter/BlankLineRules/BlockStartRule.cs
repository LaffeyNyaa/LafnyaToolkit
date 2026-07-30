namespace GDScriptFormatter
{
    /// <summary>
    /// Block-start rule: returns 1 blank line when the current line
    /// starts a block and is not in the same group as the previous
    /// line, or when entering a block from a non-block line.
    /// </summary>
    public sealed partial class BlankLineProcessor
    {
        private static int ApplyBlockStartBlankRule(string prevTrimmed,
            string curTrimmed, bool sameIndent, bool deeperThanPrev)
        {
            if (GDScriptTextUtils.Instance.IsBlockStartLine(curTrimmed) &&
                !MemberClassifier.Instance.IsSameGroup(prevTrimmed, curTrimmed) && sameIndent)
            {
                return 1;
            }

            if (GDScriptTextUtils.Instance.IsBlockStartLine(curTrimmed) &&
                !deeperThanPrev &&
                prevTrimmed.Length > 0 && prevTrimmed != ":" &&
                !GDScriptTextUtils.Instance.EndsWithColon(prevTrimmed))
            {
                return 1;
            }

            return 0;
        }
    }
}
