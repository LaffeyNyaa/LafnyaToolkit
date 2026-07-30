using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Function-definition rule: returns a blank line above a line
    /// that ends with an open brace (<c>{</c>) but is neither a
    /// block-start keyword line nor a continuation of a previous
    /// statement nor attached to a doc comment, access specifier,
    /// or a multi-line function parameter list.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> (with a
        /// blank above) when the current trimmed line ends with an
        /// open brace and is a fresh function/method definition
        /// rather than a block-start, case label, or a continuation.
        /// </summary>
        internal BlankLineRuleResult ApplyFunctionDefinitionRule(
            List<CppNonBlankEntry> nonBlank,
            int i,
            string trimmed,
            string prevTrimmed,
            bool[] isContinuation,
            bool isFunctionParamListEnd,
            bool prevWasDocComment)
        {
            if (!LafnyaToolkit.Core.Text.TextUtils.EndsWithOpenBrace(trimmed))
            {
                return BlankLineRuleResult.None;
            }

            bool isBlockStart =
                CppLineClassifier.Instance.IsBlockStartLine(trimmed);

            if (isBlockStart ||
                trimmed.StartsWith("}") ||
                trimmed.StartsWith(":") ||
                i <= 0 ||
                isContinuation[i] ||
                prevTrimmed.Length == 0 ||
                prevTrimmed == "{" ||
                LafnyaToolkit.Core.Text.TextUtils.EndsWithOpenBrace(prevTrimmed) ||
                CppTextUtils.Instance.IsAccessSpecifier(prevTrimmed) ||
                isFunctionParamListEnd ||
                BlankLineHelpers.IsDocCommentLine(prevTrimmed))
            {
                return BlankLineRuleResult.None;
            }

            return BlankLineRuleResult.Decided;
        }
    }
}
