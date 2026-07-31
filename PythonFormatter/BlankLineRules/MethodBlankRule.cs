using System.Collections.Generic;

namespace PythonFormatter
{
    /// <summary>
    /// Method blank rule: returns a blank line above a method
    /// <c>def</c> statement (a <c>def</c> at indent &gt; 0) when the
    /// previous non-blank line is also a <c>def</c> at the same indent
    /// level. Combined with the per-pass normalization, this yields
    /// exactly one blank line between consecutive methods (PEP 8
    /// strict).
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when the
        /// current line is a <c>def</c> at indent &gt; 0 and the
        /// previous non-blank line is a <c>def</c> at the same indent
        /// level.
        /// </summary>
        internal BlankLineRuleResult ApplyMethodBlankRule(
            PythonNonBlankEntry entry, PythonNonBlankEntry prevEntry,
            List<PythonNonBlankEntry> entries, int currentIndex)
        {
            if (!LineClassifier.Instance.IsDefLine(entry.Line.TrimStart()))
            {
                return BlankLineRuleResult.None;
            }

            if (entry.DefIndent <= 0)
            {
                return BlankLineRuleResult.None;
            }

            // Walk back through entries (skipping body lines) to find
            // the previous def. Two methods at the same indent level
            // should be separated by exactly one blank line, even if
            // their bodies are interleaved between them.
            int prevDefIndex = FindPreviousDefIndex(entries, currentIndex);

            if (prevDefIndex < 0)
            {
                return BlankLineRuleResult.None;
            }

            PythonNonBlankEntry prevDef = entries[prevDefIndex];

            if (!LineClassifier.Instance.IsDefLine(prevDef.Line.TrimStart()))
            {
                return BlankLineRuleResult.None;
            }

            if (prevDef.DefIndent != entry.DefIndent)
            {
                return BlankLineRuleResult.None;
            }

            return BlankLineRuleResult.Decided;
        }

        /// <summary>
        /// Walks back through the entries list (starting from
        /// <paramref name="currentIndex"/> - 1) to find the index of
        /// the most recent <c>def</c> line at the same or smaller
        /// indent. Returns -1 if no such line exists.
        /// </summary>
        private static int FindPreviousDefIndex(
            List<PythonNonBlankEntry> entries, int currentIndex)
        {
            for (int j = currentIndex - 1; j >= 0; j--)
            {
                var candidate = entries[j];

                if (LineClassifier.Instance.IsDefLine(candidate.Line.TrimStart()))
                {
                    return j;
                }
            }

            return -1;
        }
    }
}
