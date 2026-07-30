using LafnyaToolkit.Core.Text;

namespace CppFormatter
{
    /// <summary>
    /// Block-start rule: returns a blank line above a block-start
    /// keyword (if/else/for/while/.../struct/class/...) and above
    /// preprocessor conditional directives (#if, #ifdef, #ifndef)
    /// when the previous non-blank line is not a block-opening
    /// brace or a doc-comment continuation.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> (with a
        /// blank above) when the current trimmed line starts a block
        /// (block-start keyword or <c>#if</c>/<c>#ifdef</c>/
        /// <c>#ifndef</c>) and the previous non-blank line is not a
        /// continuation that should remain attached.
        /// </summary>
        internal BlankLineRuleResult ApplyBlockStartRule(
            string trimmed, string prevTrimmed)
        {
            bool isBlockStart = CppLineClassifier.Instance.IsBlockStartLine(trimmed) ||
                trimmed.StartsWith("#ifdef") ||
                trimmed.StartsWith("#ifndef") ||
                trimmed.StartsWith("#if");

            if (isBlockStart && prevTrimmed.Length > 0 &&
                prevTrimmed != "{" && !TextUtils.EndsWithOpenBrace(prevTrimmed) &&
                prevTrimmed != "*/" &&
                !prevTrimmed.StartsWith("/**") &&
                !prevTrimmed.StartsWith("///"))
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }
    }
}
