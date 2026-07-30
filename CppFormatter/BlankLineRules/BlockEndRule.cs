using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Block-end rule: returns a blank line above the first
    /// statement that follows a block-ending line. Both
    /// brace-delimited block endings (<c>}</c>) and preprocessor
    /// <c>#endif</c> directives trigger the rule. Skipped when the
    /// current line is itself a block-end (chained <c>}</c>), when
    /// the previous line is a continuation (ends with <c>,</c>), or
    /// when there is no preceding non-blank line yet.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> (with a
        /// blank above) when the previous non-blank line ends a
        /// block (closing brace or <c>#endif</c>) and the current
        /// line is a new, non-continuation statement.
        /// </summary>
        internal BlankLineRuleResult ApplyBlockEndRule(
            string trimmed, string prevTrimmed, List<string> result)
        {
            bool prevIsContinuation = prevTrimmed.EndsWith(",");

            if ((CppLineClassifier.Instance.IsBlockEndLine(prevTrimmed) ||
                prevTrimmed.StartsWith("#endif")) &&
                trimmed.Length > 0 && trimmed != "}" &&
                !trimmed.StartsWith("}") &&
                !prevIsContinuation)
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }
    }
}
