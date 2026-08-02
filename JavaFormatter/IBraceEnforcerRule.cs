using System.Collections.Generic;

using LafnyaToolkit.Core.Tokenization;

namespace JavaFormatter
{
    /// <summary>
    /// Strategy interface for per-keyword brace-enforcement rules.
    /// Each rule knows how to compute brace insertion points for one
    /// specific control-flow keyword (e.g. <c>if</c>, <c>for</c>,
    /// <c>while</c>, <c>do</c>, <c>synchronized</c>, <c>try</c>,
    /// <c>else</c>) when the keyword is found at a particular position
    /// in the source text.
    /// </summary>
    internal interface IBraceEnforcerRule
    {
        /// <summary>
        /// Inspects the source at the given keyword start position and,
        /// if appropriate, adds brace insertion points to
        /// <paramref name="insertions"/> to wrap the following
        /// single-statement body in a brace block.
        /// </summary>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">Boolean mask indicating code regions.</param>
        /// <param name="keywordPos">The position of the keyword start.</param>
        /// <param name="insertions">The insertion list to populate.</param>
        void Apply(
            string text,
            bool[] isCode,
            int keywordPos,
            List<Insertion> insertions
        );
    }
}
