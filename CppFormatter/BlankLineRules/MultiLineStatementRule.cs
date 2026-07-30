using System.Collections.Generic;
using LafnyaToolkit.Core.Text;

namespace CppFormatter
{
    /// <summary>
    /// Multi-line statement rules: detect the start and end of a
    /// statement that spans multiple lines via continuation
    /// indentation. Inserts a blank line above the first segment
    /// and around the final <c>;</c> segment as appropriate.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Multi-line statement start rule: returns a blank line
        /// above the first segment of a multi-line statement when
        /// the previous non-blank line is not a block opener,
        /// access specifier, or doc comment.
        /// </summary>
        internal BlankLineRuleResult ApplyMultiLineStatementStartRule(
            List<CppNonBlankEntry> nonBlank,
            int i,
            string trimmed,
            string prevTrimmed,
            bool[] isContinuation,
            bool isFunctionParamListEnd,
            bool prevWasDocComment,
            bool isProtected)
        {
            if (i + 1 >= nonBlank.Count)
            {
                return BlankLineRuleResult.None;
            }

            if (isContinuation[i])
            {
                return BlankLineRuleResult.None;
            }

            if (!isContinuation[i + 1])
            {
                return BlankLineRuleResult.None;
            }

            if (trimmed.EndsWith("{") ||
                trimmed.EndsWith("}") ||
                trimmed.StartsWith(":") ||
                CppLineClassifier.Instance.IsBlockStartLine(trimmed) ||
                CppLineClassifier.Instance.IsBlockEndLine(trimmed) ||
                trimmed.StartsWith("#") ||
                TextUtils.IsCommentLine(trimmed) ||
                isProtected ||
                prevTrimmed.Length == 0 ||
                prevTrimmed == "{" ||
                TextUtils.EndsWithOpenBrace(prevTrimmed) ||
                CppTextUtils.Instance.IsAccessSpecifier(prevTrimmed) ||
                prevWasDocComment)
            {
                return BlankLineRuleResult.None;
            }

            return BlankLineRuleResult.Decided;
        }

        /// <summary>
        /// Multi-line statement end rule: returns a decision
        /// (blank above OR blank below) when a new statement
        /// follows a multi-line statement. When the current line
        /// is the last segment of the multi-line statement itself
        /// (continuation indent and ends with <c>;</c>), defers
        /// the blank to after this line. When the current line is
        /// at base indent, inserts a blank above (skipping
        /// <c>)</c>-prefixed lines that close a function
        /// parameter list).
        /// </summary>
        internal BlankLineRuleResult ApplyMultiLineStatementEndRule(
            List<CppNonBlankEntry> nonBlank,
            int i,
            string trimmed,
            string prevTrimmed,
            bool[] isContinuation,
            bool isFunctionParamListEnd,
            bool prevWasDocComment,
            out bool wantBlankBelow)
        {
            wantBlankBelow = false;

            if (i <= 0)
            {
                return BlankLineRuleResult.None;
            }

            if (isContinuation[i])
            {
                return BlankLineRuleResult.None;
            }

            if (!isContinuation[i - 1])
            {
                return BlankLineRuleResult.None;
            }

            if (prevTrimmed.Length == 0 ||
                prevTrimmed == "{" ||
                TextUtils.EndsWithOpenBrace(prevTrimmed) ||
                CppLineClassifier.Instance.IsBlockEndLine(trimmed) ||
                trimmed.StartsWith(":") ||
                trimmed.StartsWith("#") ||
                isFunctionParamListEnd)
            {
                return BlankLineRuleResult.None;
            }

            if (trimmed.EndsWith(";"))
            {
                int curIndent = nonBlank[i].Line.Length - trimmed.Length;
                int prevContinuationIndent = nonBlank[i - 1].Line.Length - nonBlank[i - 1].Line.TrimStart().Length;

                if (curIndent >= prevContinuationIndent)
                {
                    wantBlankBelow = true;
                    return BlankLineRuleResult.Decided;
                }

                if (!trimmed.StartsWith(")"))
                {
                    return BlankLineRuleResult.Decided;
                }

                return BlankLineRuleResult.None;
            }

            if (!trimmed.StartsWith(")"))
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }
    }
}
