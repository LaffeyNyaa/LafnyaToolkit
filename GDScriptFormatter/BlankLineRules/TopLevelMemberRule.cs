using System.Collections.Generic;

using static GDScriptFormatter.DeclarationClassifier;
using static GDScriptFormatter.MemberClassifier;

namespace GDScriptFormatter
{
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns 1 blank line between different groups of top-level members
        /// (signals, enums, consts, vars, etc.) at the same indent level.
        /// Also handles standalone annotation lines by resolving their member
        /// group from the following declaration line.
        /// When both lines belong to the same group but one is a bare declaration
        /// and the other is an annotated declaration (via standalone annotation),
        /// a blank line is also inserted to visually separate the two blocks.
        /// </summary>
        private static int ApplyTopLevelMemberBlankRule(string prevTrimmed,
            string curTrimmed, bool sameIndent, List<NonBlankEntry> nonBlank,
            int curIdx)
        {
            if (!sameIndent)
            {
                return 0;
            }

            MemberGroup prevGroup = (MemberGroup)(-1);
            MemberGroup curGroup = (MemberGroup)(-1);

            if (IsTopLevelMember(prevTrimmed))
            {
                prevGroup = ClassifyMember(prevTrimmed);
            }
            else if (IsStandaloneAnnotation(prevTrimmed))
            {
                prevGroup = ResolveAnnotationGroup(prevTrimmed, nonBlank,
                    curIdx - 1);
            }

            if (IsTopLevelMember(curTrimmed))
            {
                curGroup = ClassifyMember(curTrimmed);
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

            // Same group: add blank if transitioning between bare declaration
            // and annotated declaration (one has a standalone annotation,
            // the other doesn't).

            if (prevGroup == curGroup && prevGroup != (MemberGroup)(-1))
            {
                bool prevIsBare = IsTopLevelMember(prevTrimmed) &&
                    !IsStandaloneAnnotation(prevTrimmed);

                bool curIsBare = IsTopLevelMember(curTrimmed) &&
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

        /// <summary>
        /// For a standalone annotation line, resolves the member group by looking ahead
        /// in the nonBlank list to find the next declaration line. This lets annotation
        /// lines inherit the group of their following declaration (e.g., @warning_ignore
        /// + signal → signal group).
        /// </summary>
        private static MemberGroup ResolveAnnotationGroup(string trimmed, List<
            NonBlankEntry> nonBlank, int curIdx)
        {
            if (!IsStandaloneAnnotation(trimmed))
            {
                return (MemberGroup)(-1);
            }

            // Look ahead for the next declaration line

            for (int i = curIdx + 1; i < nonBlank.Count; i++)
            {
                string nextTrimmed = nonBlank[i].Line.Trim();

                if (IsDeclarationLine(nextTrimmed))
                {
                    return ClassifyMember(nextTrimmed);
                }

                // Stop if we encounter another non-annotation, non-blank line

                if (!nextTrimmed.StartsWith("@"))
                {
                    break;
                }
            }

            return (MemberGroup)(-1);
        }
    }
}
