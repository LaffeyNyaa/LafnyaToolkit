using System.Collections.Generic;

using LafnyaToolkit.Core.Text;

namespace CppFormatter
{
    /// <summary>
    /// Outcome of a per-rule blank-line decision.
    /// </summary>
    internal enum BlankLineRuleResult
    {
        /// <summary>The rule abstained; the dispatcher should try the next rule.</summary>
        None,

        /// <summary>The rule decided; consult <c>wantBlankAbove</c> and <c>wantBlankBelow</c>.</summary>
        Decided
    }

    /// <summary>
    /// Central dispatcher for the per-rule blank-line checks. Each
    /// rule method under <c>BlankLineRules/</c> is a partial method on
    /// <see cref="BlankLineProcessor"/>; this file owns the ordered
    /// chain of calls. The first non-<see cref="BlankLineRuleResult.None"/>
    /// decision wins. Adding a new rule is a matter of appending a
    /// call here and supplying the corresponding partial method.
    /// </summary>
    internal static class BlankLineMainRules
    {
        /// <summary>
        /// Runs each per-rule method in order. The first one that
        /// returns <see cref="BlankLineRuleResult.Decided"/> sets the
        /// <paramref name="wantBlankAbove"/> / <paramref name="wantBlankBelow"/>
        /// outputs and the function returns.
        /// </summary>
        public static BlankLineRuleResult Dispatch(
            List<CppNonBlankEntry> nonBlank,
            int i,
            List<string> result,
            bool[] isContinuation,
            bool prevWasDocComment,
            bool isFunctionParamListEnd,
            out bool wantBlankAbove,
            out bool wantBlankBelow)
        {
            wantBlankAbove = false;
            wantBlankBelow = false;

            if (result.Count <= 0)
            {
                return BlankLineRuleResult.None;
            }

            string trimmed = nonBlank[i].Line.Trim();
            string prevTrimmed = result[result.Count - 1].Trim();
            var processor = BlankLineProcessor.Instance;

            BlankLineRuleResult r;

            r = processor.ApplyConsecutiveNamespacesRule(trimmed, prevTrimmed);
            if (r != BlankLineRuleResult.None) { wantBlankAbove = true;
                return r; }

            r = processor.ApplyBlockStartRule(trimmed, prevTrimmed);
            if (r != BlankLineRuleResult.None) { wantBlankAbove = true;
                return r; }

            r = processor.ApplyBlockEndRule(trimmed, prevTrimmed, result);
            if (r != BlankLineRuleResult.None) { wantBlankAbove = true;
                return r; }

            r = processor.ApplyFunctionDefinitionRule(nonBlank, i, trimmed,
                prevTrimmed, isContinuation, isFunctionParamListEnd,
                prevWasDocComment);

            if (r != BlankLineRuleResult.None) { wantBlankAbove = true;
                return r; }

            r = processor.ApplyConsecutiveIncludesRule(nonBlank[i], trimmed,
                prevTrimmed);

            if (r != BlankLineRuleResult.None) { wantBlankAbove = true;
                return r; }

            r = processor.ApplyIncludeGuardDefineRule(trimmed, prevTrimmed,
                result);

            if (r != BlankLineRuleResult.None) { wantBlankAbove = true;
                return r; }

            r = processor.ApplyPreprocessorBeforeIncludeRule(trimmed,
                prevTrimmed);

            if (r != BlankLineRuleResult.None) { wantBlankAbove = true;
                return r; }

            r = processor.ApplyCommentBeforeIncludeRule(trimmed, prevTrimmed);
            if (r != BlankLineRuleResult.None) { wantBlankAbove = true;
                return r; }

            r = processor.ApplyDocCommentBlankRule(trimmed, prevTrimmed);
            if (r != BlankLineRuleResult.None) { wantBlankAbove = true;
                return r; }

            r = processor.ApplyPreserveAuthorBlankRule(nonBlank[i], nonBlank, i,
                trimmed, prevTrimmed);

            if (r != BlankLineRuleResult.None) { wantBlankAbove = true;
                return r; }

            r = processor.ApplyReturnAtBlockEndRule(nonBlank, i, result);
            if (r != BlankLineRuleResult.None) { wantBlankAbove = true;
                return r; }

            r = processor.ApplyMultiLineStatementStartRule(nonBlank, i, trimmed,
                prevTrimmed, isContinuation, isFunctionParamListEnd,
                prevWasDocComment, nonBlank[i].IsProtected);

            if (r != BlankLineRuleResult.None) { wantBlankAbove = true;
                return r; }

            r = processor.ApplyMultiLineStatementEndRule(nonBlank, i, trimmed,
                prevTrimmed, isContinuation, isFunctionParamListEnd,
                prevWasDocComment, out wantBlankBelow);

            if (r != BlankLineRuleResult.None)
            {
                if (wantBlankBelow) { wantBlankAbove = false; }
                return r;
            }

            r = processor.ApplyDocCommentSingleLineRule(nonBlank, i, trimmed,
                prevWasDocComment, nonBlank[i].IsProtected, out wantBlankBelow);

            if (r != BlankLineRuleResult.None)
            {
                if (wantBlankBelow) { wantBlankAbove = false; }
                return r;
            }

            r = processor.ApplyAccessSpecifierBlankRule(trimmed, prevTrimmed);
            if (r != BlankLineRuleResult.None) { wantBlankAbove = true;
                return r; }

            return BlankLineRuleResult.None;
        }
    }
}
