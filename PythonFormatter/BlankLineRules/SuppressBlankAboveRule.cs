namespace PythonFormatter
{
    /// <summary>
    /// Suppress-blank-above rule: cancels a previously decided "want
    /// blank above" decision when the current line is a decorator or
    /// a docstring that is logically attached to a surrounding
    /// statement, or when the previous non-blank line is a function
    /// docstring — either a single-line docstring or the closing
    /// line of a multi-line docstring (a docstring at indent &gt; 0
    /// that immediately follows a <c>def</c> line; the docstring is
    /// the first statement of the function body and must not be
    /// separated from it by a blank line). Run last so it can veto
    /// the decisions of the earlier rules.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when
        /// (a) the current line is a decorator or a single-line
        /// docstring line; or (b) the previous non-blank line is a
        /// function docstring (a docstring at indent &gt; 0 that
        /// immediately follows a <c>def</c> line, i.e. the docstring
        /// is the first statement of the function body). In both
        /// cases the dispatcher uses this to clear the
        /// previously-decided "want blank above" flag.
        /// </summary>
        /// <param name="entry">The current non-blank entry.</param>
        /// <param name="prevEntry">The previous non-blank entry, or
        /// null if this is the first non-blank line.</param>
        internal BlankLineRuleResult ApplySuppressBlankAboveRule(
            PythonNonBlankEntry entry, PythonNonBlankEntry? prevEntry)
        {
            string trimmed = entry.Line.TrimStart();

            if (LineClassifier.Instance.IsDecoratorLine(trimmed))
            {
                return BlankLineRuleResult.Decided;
            }

            if (LineClassifier.Instance.IsDocstringLine(trimmed))
            {
                return BlankLineRuleResult.Decided;
            }

            // Suppress the blank line above the first statement of a
            // function body when the previous non-blank line is that
            // function's docstring. A function docstring is a
            // docstring at indent > 0 that immediately follows a
            // `def` line at a strictly smaller indent.
            if (prevEntry.HasValue && IsFunctionDocstringFollowup(
                prevEntry.Value, entry))
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }

        /// <summary>
        /// Returns true when <paramref name="prevEntry"/> is a
        /// function docstring — either a single-line docstring
        /// (e.g. <c>"""hello"""</c>) or the closing line of a
        /// multi-line docstring (a line that is just
        /// <c>"""</c> or <c>'''</c>) — at indent &gt; 0, and
        /// <paramref name="entry"/> is the next non-blank statement
        /// in the function body at the same or deeper indent.
        /// </summary>
        private static bool IsFunctionDocstringFollowup(
            PythonNonBlankEntry prevEntry, PythonNonBlankEntry entry)
        {
            string prevTrimmed = prevEntry.Line.TrimStart();

            bool isDocstring = IsDocstringOrMultilineClose(prevTrimmed);

            if (!isDocstring)
            {
                return false;
            }

            // The docstring must be at a deeper indent than the def
            // line that precedes it. We don't have the def line here,
            // so we use the rule that a function docstring is a
            // docstring at indent > 0 that has no non-blank line
            // between it and the preceding def header. Since
            // `prevEntry` is the immediately preceding non-blank
            // line, a function docstring is simply a docstring at
            // indent > 0 (the def line itself is at a smaller indent
            // and would not be `prevEntry` — the docstring IS
            // `prevEntry`).
            if (prevEntry.Indent <= 0)
            {
                return false;
            }

            // The current line must be at the same or deeper indent
            // than the docstring (i.e. still inside the function
            // body). If the current line dedents out, the docstring
            // was the last statement and there's no following
            // statement to suppress.
            if (entry.Indent < prevEntry.Indent)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true when <paramref name="trimmed"/> is a
        /// single-line docstring (a triple-quoted string that fits
        /// on one line) OR the closing line of a multi-line
        /// docstring (a line that is just <c>"""</c> or
        /// <c>'''</c> with no other content).
        /// </summary>
        private static bool IsDocstringOrMultilineClose(string trimmed)
        {
            if (LineClassifier.Instance.IsDocstringLine(trimmed))
            {
                return true;
            }

            // Multi-line docstring closing line: a line whose only
            // non-whitespace content is the triple-quote characters.
            string stripped = trimmed.Trim();

            if (stripped.Length != 3)
            {
                return false;
            }

            return stripped == "\"\"\"" || stripped == "'''";
        }
    }
}
