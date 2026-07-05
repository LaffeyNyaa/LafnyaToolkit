using System.Collections.Generic;

using static GDScriptFormatter.DeclarationClassifier;

namespace GDScriptFormatter
{
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns 1 (or 2 if the doc comment is attached to a func/class) blank line
        /// before a doc comment block. No blank line is added when the previous line
        /// is already a comment, an opening brace, or a file header.
        /// </summary>
        private static int ApplyDocCommentBlankRule(string prevTrimmed,
            string curTrimmed, List<NonBlankEntry> nonBlank, int curIdx)
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

            bool prevIsFileHeader = IsFileHeaderLine(prevTrimmed);

            if (prevTrimmed.Length > 0 && !prevIsDocComment &&
                !prevIsRegularComment && !prevIsBlockOpenBrace &&
                !prevIsFileHeader)
            {
                return IsDocCommentAttachedToFuncOrClass(
                    nonBlank, curIdx) ? 2 : 1;
            }

            // If the previous line is a ## doc comment but the current ##
            // line had a blank line above it in the original, they belong to
            // separate doc comment blocks. Insert the appropriate spacing.

            if (prevIsDocComment && nonBlank[curIdx].HadBlankAbove)
            {
                return IsDocCommentAttachedToFuncOrClass(
                    nonBlank, curIdx) ? 2 : 1;
            }

            return 0;
        }

        /// <summary>
        /// Determines whether the doc-comment block starting at startIdx is attached
        /// to a func or class declaration (looking ahead past consecutive ## lines).
        /// </summary>
        private static bool IsDocCommentAttachedToFuncOrClass(
            List<NonBlankEntry> nonBlank, int startIdx)
        {
            for (int i = startIdx + 1; i < nonBlank.Count; i++)
            {
                string trimmed = nonBlank[i].Line.Trim();

                if (!trimmed.StartsWith("##"))
                {
                    // Standalone annotations (e.g. @rpc, @warning_ignore on
                    // their own line before func/class) are part of the
                    // declaration. Skip them to look for the func/class keyword.

                    if (IsStandaloneAnnotation(trimmed))
                    {
                        continue;
                    }

                    return IsFuncOrClassDecl(trimmed);
                }
            }

            return false;
        }
    }
}
