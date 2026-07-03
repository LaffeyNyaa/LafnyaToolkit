namespace GDScriptFormatter
{
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns 1 blank line when the current line is at a shallower indent
        /// than the previous line (i.e. we just exited a code block).
        /// </summary>
        private static int ApplyDedentBlankRule(int curIndent, int prevIndent)
        {
            if (curIndent < prevIndent)
            {
                return 1;
            }

            return 0;
        }
    }
}
