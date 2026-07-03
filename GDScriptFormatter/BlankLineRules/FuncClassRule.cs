using static GDScriptFormatter.DeclarationClassifier;

namespace GDScriptFormatter
{
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns 2 blank lines when the current line is a func/class declaration,
        /// or when the previous line was a func/class declaration at the same indent level.
        /// </summary>
        private static int ApplyFuncClassBlankRule(string prevTrimmed,
            string curTrimmed, bool sameIndent)
        {
            if (IsFuncOrClassDecl(curTrimmed))
            {
                return 2;
            }

            if (sameIndent && IsFuncOrClassDecl(prevTrimmed) &&
                !IsFuncOrClassDecl(curTrimmed))
            {
                return 2;
            }

            return 0;
        }
    }
}
