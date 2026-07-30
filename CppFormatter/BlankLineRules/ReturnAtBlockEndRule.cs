using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Return-at-block-end rule: returns a blank line above a
    /// <c>return</c> statement that sits just before a block
    /// ending <c>}</c> when the previous statement is at a
    /// different indent (a multi-line statement). Single-line
    /// preceding statements at the same indentation as the
    /// <c>return</c> do NOT get a blank line.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> (with a
        /// blank above) when a <c>return</c> line at a different
        /// indent from the previous line precedes a closing
        /// <c>}</c>.
        /// </summary>
        internal BlankLineRuleResult ApplyReturnAtBlockEndRule(
            List<CppNonBlankEntry> nonBlank, int i, List<string> result)
        {
            if (!nonBlank[i].Line.Trim().StartsWith("return"))
            {
                return BlankLineRuleResult.None;
            }

            if (i + 1 >= nonBlank.Count)
            {
                return BlankLineRuleResult.None;
            }

            if (nonBlank[i + 1].Line.Trim() != "}")
            {
                return BlankLineRuleResult.None;
            }

            if (result.Count == 0)
            {
                return BlankLineRuleResult.None;
            }

            string lastLine = result[result.Count - 1];
            string lastTrimmed = lastLine.Trim();

            if (lastTrimmed.Length == 0 ||
                LafnyaToolkit.Core.Text.TextUtils.EndsWithOpenBrace(lastTrimmed))
            {
                return BlankLineRuleResult.None;
            }

            int lastIndent = lastLine.Length - lastLine.TrimStart().Length;

            int returnIndent = nonBlank[i].Line.Length -
                nonBlank[i].Line.TrimStart().Length;

            if (lastIndent == returnIndent)
            {
                return BlankLineRuleResult.None;
            }

            return BlankLineRuleResult.Decided;
        }
    }
}
