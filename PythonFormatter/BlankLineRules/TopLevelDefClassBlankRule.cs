namespace PythonFormatter
{
    /// <summary>
    /// Top-level def/class blank rule: returns a blank line above a
    /// top-level <c>def</c> or <c>class</c> statement when the
    /// previous non-blank line is also a top-level <c>def</c> or
    /// <c>class</c> statement, a module-level docstring, or a
    /// strictly deeper-indented line. Combined with the per-pass
    /// normalization, this yields exactly two blank lines between
    /// consecutive top-level definitions and after a module docstring
    /// (PEP 8 strict).
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when the
        /// current line is a top-level <c>def</c> or <c>class</c>
        /// statement and either (a) the previous non-blank line is
        /// also a top-level <c>def</c> or <c>class</c> statement, (b)
        /// the previous non-blank line is a module-level docstring
        /// (a single-line <c>"""..."""</c> at indent 0), or (c) the
        /// previous non-blank line is at a strictly deeper indent
        /// (i.e. we just closed a function body and a new top-level
        /// definition is starting). All three cases yield exactly
        /// two blank lines above (PEP 8 strict).
        /// </summary>
        internal BlankLineRuleResult ApplyTopLevelDefClassBlankRule(
            PythonNonBlankEntry entry, PythonNonBlankEntry prevEntry)
        {
            string trimmed = entry.Line.TrimStart();
            string prevTrimmed = prevEntry.Line.TrimStart();

            if (entry.Indent != 0)
            {
                return BlankLineRuleResult.None;
            }

            if (!LineClassifier.Instance.IsTopLevelDefClass(trimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (prevEntry.Indent == 0 &&
                LineClassifier.Instance.IsTopLevelDefClass(prevTrimmed))
            {
                return BlankLineRuleResult.Decided;
            }

            // PEP 8: top-level definitions following a module
            // docstring are still separated by two blank lines, even
            // though the docstring is not itself a def/class.
            if (prevEntry.Indent == 0 &&
                LineClassifier.Instance.IsDocstringLine(prevTrimmed))
            {
                return BlankLineRuleResult.Decided;
            }

            if (prevEntry.Indent > entry.Indent)
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }
    }
}
