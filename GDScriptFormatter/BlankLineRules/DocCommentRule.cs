using System.Collections.Generic;
using LafnyaToolkit.Core.Text;

namespace GDScriptFormatter
{
    /// <summary>
    /// Doc-comment rule: returns 1 (or 2 if the doc comment is
    /// attached to a func/class) blank line before a doc comment
    /// block. No blank line is added when the previous line is
    /// already a comment, an opening brace, or a file header. If the
    /// previous line is also a ## doc comment but the current ##
    /// line had a blank line above it in the original, they belong to
    /// separate doc comment blocks and the appropriate spacing is
    /// inserted.
    /// </summary>
    public sealed partial class BlankLineProcessor
    {
        private static int ApplyDocCommentBlankRule(string prevTrimmed,
            string curTrimmed, List<NonBlankEntry> nonBlank,
            List<bool> hadBlankAbove, int curIdx)
        {
            if (!curTrimmed.StartsWith("##"))
            {
                return 0;
            }

            bool prevIsDocComment = prevTrimmed.StartsWith("##");

            bool prevIsRegularComment = prevTrimmed.StartsWith("#") &&
                !prevIsDocComment;

            bool prevIsBlockOpenBrace = prevTrimmed == "{" ||
                prevTrimmed.EndsWith("{");

            bool prevIsFileHeader = DeclarationClassifier.Instance.IsFileHeaderLine(prevTrimmed);

            if (prevTrimmed.Length > 0 && !prevIsDocComment &&
                !prevIsRegularComment && !prevIsBlockOpenBrace &&
                !prevIsFileHeader)
            {
                return IsDocCommentAttachedToFuncOrClass(
                    nonBlank, curIdx) ? 2 : 1;
            }

            if (prevIsDocComment && hadBlankAbove[curIdx])
            {
                return IsDocCommentAttachedToFuncOrClass(
                    nonBlank, curIdx) ? 2 : 1;
            }

            return 0;
        }

        private static bool IsDocCommentAttachedToFuncOrClass(
            List<NonBlankEntry> nonBlank, int startIdx)
        {
            for (int i = startIdx + 1; i < nonBlank.Count; i++)
            {
                string trimmed = nonBlank[i].Line.Trim();

                if (!trimmed.StartsWith("##"))
                {
                    if (IsStandaloneAnnotation(trimmed))
                    {
                        continue;
                    }

                    return DeclarationClassifier.Instance.IsFuncOrClassDecl(trimmed);
                }
            }

            return false;
        }
    }
}
