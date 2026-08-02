using System.Collections.Generic;

namespace CSharpFormatter
{
    /// <summary>
    /// Central dispatcher for the per-rule blank-line checks. Each
    /// rule method under <c>BlankLineRules/</c> is a partial method
    /// on <see cref="BlankLineProcessor"/>; this file owns the
    /// ordered chain of calls. The first non-<see cref="BlankLineVerdict.None"/>
    /// decision wins. Adding a new rule is a matter of appending a
    /// call here and supplying the corresponding partial method.
    /// </summary>
    internal static class BlankLineMainRules
    {
        /// <summary>
        /// Runs each per-rule method in order. The first one that
        /// returns a non-<see cref="BlankLineVerdict.None"/> verdict
        /// determines whether a blank line is added above the current
        /// line.
        /// </summary>
        /// <param name="p">Pre-computed predicates for the current line.</param>
        /// <param name="result">The result list being built (used to detect the first non-blank line).</param>
        /// <returns>True if a blank line should be added above the current line; false otherwise.</returns>
        public static bool Dispatch(
            in BlankLinePredicates p,
            List<string> result
        )
        {
            if (result.Count <= 0)
            {
                return false;
            }

            var processor = BlankLineProcessor.Instance;
            BlankLineVerdict v;

            v = processor.ApplyIfElseRule(p);

            if (v == BlankLineVerdict.SuppressBlank) { return false; }

            if (v == BlankLineVerdict.AddBlankAbove) { return true; }
            v = processor.ApplyCatchFinallyRule(p);

            if (v == BlankLineVerdict.SuppressBlank) { return false; }

            if (v == BlankLineVerdict.AddBlankAbove) { return true; }
            v = processor.ApplyBlockStartRule(p);

            if (v == BlankLineVerdict.AddBlankAbove) { return true; }

            if (v == BlankLineVerdict.SuppressBlank) { return false; }
            v = processor.ApplyBlockEndRule(p);

            if (v == BlankLineVerdict.AddBlankAbove) { return true; }

            if (v == BlankLineVerdict.SuppressBlank) { return false; }
            v = processor.ApplyBlockBodyDeclarationRule(p);

            if (v == BlankLineVerdict.AddBlankAbove) { return true; }

            if (v == BlankLineVerdict.SuppressBlank) { return false; }
            v = processor.ApplyUsingDirectiveRule(p);

            if (v == BlankLineVerdict.AddBlankAbove) { return true; }

            if (v == BlankLineVerdict.SuppressBlank) { return false; }
            v = processor.ApplyMultiLineEndRule(p);

            if (v == BlankLineVerdict.AddBlankAbove) { return true; }

            if (v == BlankLineVerdict.SuppressBlank) { return false; }
            v = processor.ApplyMultiLineStartRule(p);

            if (v == BlankLineVerdict.AddBlankAbove) { return true; }

            if (v == BlankLineVerdict.SuppressBlank) { return false; }
            v = processor.ApplyDocCommentRule(p);

            if (v == BlankLineVerdict.AddBlankAbove) { return true; }

            if (v == BlankLineVerdict.SuppressBlank) { return false; }
            v = processor.ApplyAuthorBlankRule(p);

            if (v == BlankLineVerdict.AddBlankAbove) { return true; }

            if (v == BlankLineVerdict.SuppressBlank) { return false; }
            return false;
        }
    }
}
