using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Doc-comment rule: returns a blank line above a doc-comment
    /// line (<c>///</c> or <c>/**</c>) when the previous non-blank
    /// line is not itself a doc comment, regular comment, opening
    /// brace, or access specifier. Doc comments visually separate a
    /// declaration from preceding code.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> (with a
        /// blank above) when the current line starts a doc comment
        /// and the previous non-blank line is unrelated to the
        /// comment block.
        /// </summary>
        internal BlankLineRuleResult ApplyDocCommentBlankRule(
            string trimmed, string prevTrimmed)
        {
            if (!trimmed.StartsWith("///") && !trimmed.StartsWith("/**"))
            {
                return BlankLineRuleResult.None;
            }

            bool prevIsDocComment =
                prevTrimmed.StartsWith("///") ||
                prevTrimmed.StartsWith("/**") ||
                prevTrimmed.StartsWith("*");

            bool prevIsRegularComment =
                prevTrimmed.StartsWith("//") ||
                prevTrimmed.StartsWith("/*");

            bool prevIsBlockOpenBraceOrAccessSpec =
                prevTrimmed == "{" ||
                LafnyaToolkit.Core.Text.TextUtils.EndsWithOpenBrace(prevTrimmed) ||
                CppTextUtils.Instance.IsAccessSpecifier(prevTrimmed);

            if (prevTrimmed.Length > 0 && !prevIsDocComment &&
                !prevIsRegularComment &&
                !prevIsBlockOpenBraceOrAccessSpec)
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }

        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> (with a
        /// blank BELOW) when the current single-line statement
        /// (ending in <c>;</c>) is preceded by a doc comment and is
        /// not followed by a block-end line. Treats the doc comment
        /// plus single-line statement as a unit.
        /// </summary>
        internal BlankLineRuleResult ApplyDocCommentSingleLineRule(
            List<CppNonBlankEntry> nonBlank,
            int i,
            string trimmed,
            bool prevWasDocComment,
            bool isProtected,
            out bool wantBlankBelow)
        {
            wantBlankBelow = false;

            if (!trimmed.EndsWith(";") || !prevWasDocComment || isProtected)
            {
                return BlankLineRuleResult.None;
            }

            if (i + 1 >= nonBlank.Count)
            {
                return BlankLineRuleResult.None;
            }

            if (CppLineClassifier.Instance.IsBlockEndLine(nonBlank[i + 1].Line.Trim()))
            {
                return BlankLineRuleResult.None;
            }

            wantBlankBelow = true;
            return BlankLineRuleResult.Decided;
        }
    }
}
