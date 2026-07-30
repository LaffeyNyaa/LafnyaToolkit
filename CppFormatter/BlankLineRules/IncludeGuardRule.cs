using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Include-guard family of rules covering the four ways a blank
    /// line should appear around <c>#include</c> directives:
    /// consecutive-include preservation, include-guard
    /// <c>#define</c> separation, standalone-preprocessor-to-include
    /// separation, and comment-to-include separation.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Consecutive-include rule: preserves an existing blank
        /// line between two <c>#include</c> directives when the
        /// author had inserted one.
        /// </summary>
        internal BlankLineRuleResult ApplyConsecutiveIncludesRule(
            CppNonBlankEntry entry, string trimmed, string prevTrimmed)
        {
            if (CppTextUtils.Instance.IsIncludeDirective(trimmed) &&
                CppTextUtils.Instance.IsIncludeDirective(prevTrimmed) &&
                entry.HadBlankAbove)
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }

        /// <summary>
        /// Include-guard <c>#define</c> rule: ensures a blank line
        /// between an empty <c>#define</c> that follows an
        /// <c>#ifndef</c> with the same name and the first
        /// <c>#include</c> directive. Keeps the classic header-guard
        /// pattern visually separated from the actual includes.
        /// </summary>
        internal BlankLineRuleResult ApplyIncludeGuardDefineRule(
            string trimmed, string prevTrimmed, List<string> result)
        {
            if (!CppTextUtils.Instance.IsIncludeDirective(trimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (!prevTrimmed.StartsWith("#define") ||
                BlankLineHelpers.IsDefineWithValue(prevTrimmed) ||
                result.Count < 2)
            {
                return BlankLineRuleResult.None;
            }

            string prevPrevTrimmed = result[result.Count - 2].Trim();

            if (prevPrevTrimmed.StartsWith("#ifndef"))
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }

        /// <summary>
        /// Standalone-preprocessor-to-include rule: ensures a blank
        /// line between a non-include preprocessor directive
        /// (e.g. <c>#pragma once</c>) and an <c>#include</c> directive
        /// when the previous directive is not part of a preprocessor
        /// conditional block.
        /// </summary>
        internal BlankLineRuleResult ApplyPreprocessorBeforeIncludeRule(
            string trimmed, string prevTrimmed)
        {
            if (!CppTextUtils.Instance.IsIncludeDirective(trimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (prevTrimmed.Length == 0 || prevTrimmed[0] != '#')
            {
                return BlankLineRuleResult.None;
            }

            if (CppTextUtils.Instance.IsIncludeDirective(prevTrimmed) ||
                prevTrimmed.StartsWith("#if") ||
                prevTrimmed.StartsWith("#elif") ||
                prevTrimmed.StartsWith("#else") ||
                prevTrimmed.StartsWith("#endif") ||
                prevTrimmed.StartsWith("#define"))
            {
                return BlankLineRuleResult.None;
            }

            return BlankLineRuleResult.Decided;
        }

        /// <summary>
        /// Comment-to-include rule: ensures a blank line between a
        /// comment and an <c>#include</c> directive.
        /// </summary>
        internal BlankLineRuleResult ApplyCommentBeforeIncludeRule(
            string trimmed, string prevTrimmed)
        {
            if (CppTextUtils.Instance.IsIncludeDirective(trimmed) &&
                LafnyaToolkit.Core.Text.TextUtils.IsCommentLine(prevTrimmed))
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }
    }
}
