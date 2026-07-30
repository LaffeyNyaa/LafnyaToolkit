using System.Collections.Generic;
using LafnyaToolkit.Core.Text;

namespace GDScriptFormatter
{
    /// <summary>
    /// Top-level-member rule: returns 1 blank line between different
    /// groups of top-level members (signals, enums, consts, vars,
    /// etc.) at the same indent level. Also handles standalone
    /// annotation lines by resolving their member group from the
    /// following declaration line. When both lines belong to the same
    /// group but one is a bare declaration and the other is an
    /// annotated declaration (via standalone annotation), a blank
    /// line is also inserted to visually separate the two blocks.
    /// </summary>
    public sealed partial class BlankLineProcessor
    {
        private static int ApplyTopLevelMemberBlankRule(string prevTrimmed,
            string curTrimmed, bool sameIndent, List<NonBlankEntry> nonBlank,
            List<bool> contList, int curIdx)
        {
            if (!sameIndent)
            {
                return 0;
            }

            MemberGroup prevGroup = (MemberGroup)(-1);
            MemberGroup curGroup = (MemberGroup)(-1);

            if (MemberClassifier.Instance.IsTopLevelMember(prevTrimmed))
            {
                prevGroup = MemberClassifier.Instance.ClassifyMember(prevTrimmed);
            }
            else if (IsStandaloneAnnotation(prevTrimmed))
            {
                prevGroup = ResolveAnnotationGroup(prevTrimmed, nonBlank,
                    curIdx - 1);
            }

            if (MemberClassifier.Instance.IsTopLevelMember(curTrimmed))
            {
                curGroup = MemberClassifier.Instance.ClassifyMember(curTrimmed);
            }
            else if (IsStandaloneAnnotation(curTrimmed))
            {
                curGroup = ResolveAnnotationGroup(curTrimmed, nonBlank, curIdx);
            }

            if (prevGroup != (MemberGroup)(-1) && curGroup !=
                (MemberGroup)(-1) && prevGroup != curGroup)
            {
                return 1;
            }

            if (prevGroup == curGroup && prevGroup != (MemberGroup)(-1))
            {
                bool prevIsBare = MemberClassifier.Instance.IsTopLevelMember(prevTrimmed) &&
                    !IsStandaloneAnnotation(prevTrimmed);

                bool curIsBare = MemberClassifier.Instance.IsTopLevelMember(curTrimmed) &&
                    !IsStandaloneAnnotation(curTrimmed);

                bool prevIsAnnotated = IsStandaloneAnnotation(prevTrimmed);
                bool curIsAnnotated = IsStandaloneAnnotation(curTrimmed);

                if ((prevIsBare && curIsAnnotated) ||
                    (prevIsAnnotated && curIsBare))
                {
                    return 1;
                }
            }

            return 0;
        }

        private static MemberGroup ResolveAnnotationGroup(string trimmed,
            List<NonBlankEntry> nonBlank, int curIdx)
        {
            if (!IsStandaloneAnnotation(trimmed))
            {
                return (MemberGroup)(-1);
            }

            for (int i = curIdx + 1; i < nonBlank.Count; i++)
            {
                string nextTrimmed = nonBlank[i].Line.Trim();

                if (DeclarationClassifier.Instance.IsDeclarationLine(nextTrimmed))
                {
                    return MemberClassifier.Instance.ClassifyMember(nextTrimmed);
                }

                if (!nextTrimmed.StartsWith("@"))
                {
                    break;
                }
            }

            return (MemberGroup)(-1);
        }
    }
}
