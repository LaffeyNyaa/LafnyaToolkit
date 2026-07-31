using LafnyaToolkit.Core.Text;

namespace PythonFormatter
{
    /// <summary>
    /// First-method-in-class blank rule: returns a blank line above the
    /// first <c>def</c> inside a class body (a <c>def</c> at indent
    /// &gt; 0) when the previous non-blank line is the <c>class</c>
    /// header at a strictly smaller indent. This separates the
    /// <c>class</c> header from the first method by exactly one blank
    /// line (PEP 8).
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when the
        /// current line is the first <c>def</c> inside a class body
        /// and the previous non-blank line is the <c>class</c> header
        /// at a smaller indent.
        /// </summary>
        internal BlankLineRuleResult ApplyFirstMethodInClassBlankRule(
            PythonNonBlankEntry entry, PythonNonBlankEntry prevEntry)
        {
            if (entry.Indent <= 0)
            {
                return BlankLineRuleResult.None;
            }

            if (prevEntry.Indent >= entry.Indent)
            {
                return BlankLineRuleResult.None;
            }

            string trimmed = entry.Line.TrimStart();
            string prevTrimmed = prevEntry.Line.TrimStart();

            if (!LineClassifier.Instance.IsDefLine(trimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (!TextUtils.StartsWithKeyword(prevTrimmed, "class"))
            {
                return BlankLineRuleResult.None;
            }

            return BlankLineRuleResult.Decided;
        }
    }
}
