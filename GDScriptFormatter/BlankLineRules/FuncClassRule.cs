namespace GDScriptFormatter
{
    /// <summary>
    /// Func/class rule: returns 2 blank lines when the current line
    /// is a func/class declaration, or when the previous line was a
    /// func/class declaration at the same indent level.
    /// </summary>
    public sealed partial class BlankLineProcessor
    {
        private static int ApplyFuncClassBlankRule(string prevTrimmed,
            string curTrimmed, bool sameIndent)
        {
            if (DeclarationClassifier.Instance.IsFuncOrClassDecl(curTrimmed))
            {
                return 2;
            }

            if (sameIndent &&
                DeclarationClassifier.Instance.IsFuncOrClassDecl(prevTrimmed) &&
                !DeclarationClassifier.Instance.IsFuncOrClassDecl(curTrimmed))
            {
                return 2;
            }

            return 0;
        }
    }
}
