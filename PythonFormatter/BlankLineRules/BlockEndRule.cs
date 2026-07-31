namespace PythonFormatter
{
    /// <summary>
    /// Block-end rule: returns a blank line above a regular statement
    /// that follows a single-line block body (a body whose only
    /// statement sits on the same line as the block-start keyword via
    /// a <c>:</c> on the same line — e.g. <c>if x: do_something()</c>
    /// is a one-line block; the next line in the same scope should be
    /// separated by a blank line).
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when the
        /// previous non-blank line contains a <c>:</c> outside of a
        /// string, comment, or slice, and ends with a non-empty code
        /// tail (i.e. it is a single-line block, not a block-start
        /// header that expects a body on the next line).
        /// </summary>
        /// <param name="entry">The current entry.</param>
        /// <param name="prevEntry">The previous non-blank entry.</param>
        /// <param name="lineStart">The start position of the previous
        /// line in the full text. The <paramref name="isCode"/> mask
        /// is indexed against the full text, so this offset is needed
        /// to translate a position within the previous line into the
        /// correct index into <paramref name="isCode"/>.</param>
        /// <param name="isCode">The code mask of the full text.</param>
        internal BlankLineRuleResult ApplyBlockEndRule(
            PythonNonBlankEntry entry, PythonNonBlankEntry prevEntry,
            int lineStart, bool[] isCode)
        {
            if (entry.PrevIndent < 0)
            {
                return BlankLineRuleResult.None;
            }

            if (entry.Indent != entry.PrevIndent)
            {
                return BlankLineRuleResult.None;
            }

            string prevTrimmed = prevEntry.Line.TrimStart();

            if (LineClassifier.Instance.IsDecoratorLine(prevTrimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (HasInlineBodyAfterColon(prevEntry.Line, lineStart, isCode))
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }

        private static bool HasInlineBodyAfterColon(string line, int lineStart,
            bool[] isCode)
        {
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }

            int i = 0;

            while (i < line.Length)
            {
                int textPos = lineStart + i;

                if (textPos < 0 || textPos >= isCode.Length || !isCode[textPos])
                {
                    i++;
                    continue;
                }

                if (line[i] == ':')
                {
                    int after = i + 1;

                    while (after < line.Length &&
                        (line[after] == ' ' || line[after] == '\t'))
                    {
                        after++;
                    }

                    if (after >= line.Length)
                    {
                        return false;
                    }

                    int afterTextPos = lineStart + after;

                    if (afterTextPos < 0 || afterTextPos >= isCode.Length ||
                        !isCode[afterTextPos])
                    {
                        return false;
                    }

                    if (line[after] == '#')
                    {
                        return false;
                    }

                    return true;
                }

                i++;
            }

            return false;
        }
    }
}
