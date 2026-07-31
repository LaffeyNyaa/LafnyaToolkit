namespace PythonFormatter
{
    /// <summary>
    /// After-block-end rule: returns a blank line above a regular
    /// statement that follows the last line of a block. The pattern
    /// is: the previous non-blank line is at a strictly deeper indent
    /// than the current line (i.e. we have just dedented out of an
    /// <c>if</c>/<c>for</c>/<c>while</c>/<c>try</c>/<c>with</c>/
    /// <c>def</c> body) and the previous line is a block-ending
    /// statement (<c>return</c>, <c>pass</c>, <c>break</c>,
    /// <c>continue</c>, or a closing brace). The rule only fires
    /// when the current line is at a non-top-level indent, so it
    /// does not interfere with the top-level def/class rule that
    /// already produces two blank lines between top-level
    /// definitions.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when the
        /// previous non-blank line is at a strictly deeper indent than
        /// the current line and the previous line is a block-ending
        /// statement. Skips the first non-blank line and any case
        /// where the current line is at the top level (indent 0),
        /// since PEP 8 spacing for top-level definitions is handled
        /// by <see cref="ApplyTopLevelDefClassBlankRule"/>. Also
        /// skips block-continuation keywords (<c>elif</c>,
        /// <c>else</c>, <c>except</c>, <c>finally</c>, <c>case</c>)
        /// which are attached to the preceding block and must not be
        /// separated by a blank line.
        /// </summary>
        /// <param name="entry">The current non-blank entry.</param>
        /// <param name="prevEntry">The previous non-blank entry.</param>
        internal BlankLineRuleResult ApplyAfterBlockEndRule(
            PythonNonBlankEntry entry, PythonNonBlankEntry prevEntry)
        {
            if (entry.PrevIndent < 0)
            {
                return BlankLineRuleResult.None;
            }

            // The current line must be at a shallower indent than the
            // previous line (a dedent out of a block). Skip top-level
            // dedents so we don't fight the top-level def/class rule.

            if (entry.Indent >= prevEntry.Indent)
            {
                return BlankLineRuleResult.None;
            }

            if (entry.Indent <= 0)
            {
                return BlankLineRuleResult.None;
            }

            // Block-continuation keywords (elif, else, except,
            // finally, case) are attached to the preceding block and
            // must not be separated by a blank line.
            string trimmed = entry.Line.TrimStart();

            if (LineClassifier.Instance.IsBlockContinuationLine(trimmed))
            {
                return BlankLineRuleResult.None;
            }

            string prevTrimmed = prevEntry.Line.TrimStart();

            if (IsBlockEndingStatement(prevTrimmed))
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }

        /// <summary>
        /// Returns true when the trimmed line is a block-ending
        /// statement: <c>return</c>, <c>pass</c>, <c>break</c>,
        /// <c>continue</c>, or a line whose last non-whitespace code
        /// character is a closing brace.
        /// </summary>
        private static bool IsBlockEndingStatement(string trimmed)
        {
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            if (LineClassifier.Instance.IsPassReturnBreakContinue(trimmed))
            {
                return true;
            }

            // Find the last non-whitespace character.
            int last = trimmed.Length - 1;

            while (last >= 0 && (trimmed[last] == ' ' ||
                trimmed[last] == '\t'))
            {
                last--;
            }

            if (last < 0)
            {
                return false;
            }

            return trimmed[last] == '}';
        }
    }
}
