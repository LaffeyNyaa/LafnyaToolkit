using static GDScriptFormatter.MemberClassifier;

namespace GDScriptFormatter
{
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns 1 blank line when a var declaration ends with a colon (indicating
        /// a setter/getter block), even if it belongs to the same member group as the
        /// previous line. This ensures that properties with setters/getters are always
        /// visually separated from adjacent members.
        /// </summary>
        private static int ApplySetterGetterBlockRule(string prevTrimmed,
            string curTrimmed, bool sameIndent)
        {
            if (!sameIndent)
            {
                return 0;
            }

            // Current line is a block-start var declaration (has setter/getter)

            if (IsBlockStartVar(curTrimmed))
            {
                return 1;
            }

            // Previous line is a block-start var declaration

            if (IsBlockStartVar(prevTrimmed))
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Determines whether a trimmed line is a variable declaration that starts a
        /// setter/getter block (ends with a colon).
        /// </summary>
        private static bool IsBlockStartVar(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            // Must be a var/export declaration that ends with ':'

            if (!TextUtils.EndsWithColon(trimmed))
            {
                return false;
            }

            // Check if it's a var declaration (possibly with @export/@onready prefix)
            MemberGroup memberType = ClassifyMember(trimmed);

            if (memberType == MemberGroup.Export || memberType ==
                MemberGroup.RegularVar || memberType == MemberGroup.Onready ||
                memberType == MemberGroup.Private)
            {
                return true;
            }

            // Also handle explicit "var" keyword with colon (e.g. "var x:" as type annotation)

            if (TextUtils.StartsWithKeyword(trimmed, "var"))
            {
                return true;
            }

            return false;
        }
    }
}
