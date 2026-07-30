namespace GDScriptFormatter
{
    /// <summary>
    /// Setter/getter block rule: returns 1 blank line when a var
    /// declaration ends with a colon (indicating a setter/getter
    /// block), even if it belongs to the same member group as the
    /// previous line. This ensures that properties with
    /// setters/getters are always visually separated from adjacent
    /// members.
    /// </summary>
    public sealed partial class BlankLineProcessor
    {
        private static int ApplySetterGetterBlockRule(string prevTrimmed,
            string curTrimmed, bool sameIndent)
        {
            if (!sameIndent)
            {
                return 0;
            }

            if (IsBlockStartVar(curTrimmed))
            {
                return 1;
            }

            if (IsBlockStartVar(prevTrimmed))
            {
                return 1;
            }

            return 0;
        }

        private static bool IsBlockStartVar(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (!GDScriptTextUtils.Instance.EndsWithColon(trimmed))
            {
                return false;
            }

            MemberGroup memberType =
                MemberClassifier.Instance.ClassifyMember(trimmed);

            if (memberType == MemberGroup.Export || memberType ==
                MemberGroup.RegularVar || memberType == MemberGroup.Onready ||
                memberType == MemberGroup.Private)
            {
                return true;
            }

            if (LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(trimmed,
                "var"))
            {
                return true;
            }

            return false;
        }
    }
}
