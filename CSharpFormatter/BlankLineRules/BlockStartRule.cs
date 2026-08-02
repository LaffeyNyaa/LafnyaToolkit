using LafnyaToolkit.Core.Text;

namespace CSharpFormatter
{
    /// <summary>
    /// Block-start rule: returns a blank line above a block-start
    /// keyword line (e.g. <c>namespace</c>, <c>class</c>, <c>if</c>,
    /// <c>for</c>, ...) when the previous non-blank line is not
    /// itself a block-opening brace.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineVerdict.AddBlankAbove"/> when
        /// the current trimmed line is a block-start line and the
        /// previous non-blank line is not a block-opening brace.
        /// </summary>
        /// <param name="p">Pre-computed predicates for the current line.</param>
        /// <returns>The rule verdict.</returns>
        internal BlankLineVerdict ApplyBlockStartRule(
            in BlankLinePredicates p)
        {
            if (p.IsBlockStart && p.PrevTrimmed.Length > 0 &&
                !p.PrevIsBlockStartBrace)
            {
                // Do not add blank line before `while` in `do-while`
                // construct. A `while` keyword after a block end
                // (`}`) is always part of `do-while` when no blank
                // line separates them in the input.
                if (p.LineIsCode &&
                    TextUtils.StartsWithKeyword(p.Trimmed, "while") &&
                    p.PrevIsBlockEnd &&
                    !p.EntryHadBlankAbove)
                {
                    return BlankLineVerdict.SuppressBlank;
                }

                return BlankLineVerdict.AddBlankAbove;
            }

            return BlankLineVerdict.None;
        }
    }
}
