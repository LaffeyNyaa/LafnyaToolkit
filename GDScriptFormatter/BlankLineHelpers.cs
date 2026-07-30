using System.Collections.Generic;
using LafnyaToolkit.Core.Text;

namespace GDScriptFormatter
{
    /// <summary>
    /// Helper predicates used by the blank-line rules. Lives in its
    /// own file to keep <c>BlankLineProcessor.cs</c> focused on the
    /// main pipeline.
    /// </summary>
    public sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Determines whether a trimmed line is a plain single-line
        /// GDScript statement: non-empty, not a comment, not a
        /// block-start, not an annotation, not a func/class
        /// declaration, and not a file header.
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>True if the line is a plain single-line statement.</returns>
        private static bool IsPlainSingleLineStatement(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed.StartsWith("#"))
            {
                return false;
            }

            if (GDScriptTextUtils.Instance.IsBlockStartLine(trimmed))
            {
                return false;
            }

            if (trimmed.StartsWith("@"))
            {
                return false;
            }

            if (DeclarationClassifier.Instance.IsFuncOrClassDecl(trimmed))
            {
                return false;
            }

            if (DeclarationClassifier.Instance.IsFileHeaderLine(trimmed))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether a trimmed line is a standalone annotation
        /// line: starts with @ but does NOT contain a declaration
        /// keyword (var, func, signal, const, enum, class, static) on
        /// the same line. For example,
        /// <c>@warning_ignore("unused_signal")</c> is standalone,
        /// <c>@export_storage var x := 0</c> is NOT standalone (it has
        /// "var").
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>True if the line is a standalone annotation.</returns>
        private static bool IsStandaloneAnnotation(string trimmed)
        {
            if (!trimmed.StartsWith("@"))
            {
                return false;
            }

            int spaceIdx = trimmed.IndexOf(' ');

            if (spaceIdx < 0)
            {
                return true;
            }

            string rest = trimmed.Substring(spaceIdx + 1).TrimStart();
            return !LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(rest, "var") &&
                !LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(rest, "func") &&
                !LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(rest, "signal") &&
                !LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(rest, "const") &&
                !LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(rest, "enum") &&
                !LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(rest, "class") &&
                !LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(rest, "static");
        }

        /// <summary>
        /// Determines whether a preceding comment line is attached to
        /// the current declaration. Doc comment lines (starting with
        /// ##) are always force-attached to a following declaration
        /// regardless of whether a blank line originally separated
        /// them. Single-# comments are attached only when no blank
        /// line originally separated them.
        /// </summary>
        /// <param name="prevTrimmed">The previous trimmed line.</param>
        /// <param name="curTrimmed">The current trimmed line.</param>
        /// <param name="nonBlank">The list of non-blank entries.</param>
        /// <param name="hadBlankAbove">Per-entry flag indicating whether a blank line existed above the entry in the original input.</param>
        /// <param name="curIdx">The current index in the non-blank list.</param>
        /// <returns>True if the previous line is an attached comment for the current declaration.</returns>
        private static bool IsAttachedComment(string prevTrimmed,
            string curTrimmed, List<NonBlankEntry> nonBlank,
            List<bool> hadBlankAbove, int curIdx)
        {
            if (!prevTrimmed.StartsWith("#"))
            {
                return false;
            }

            if (!DeclarationClassifier.Instance.IsDeclarationLine(curTrimmed))
            {
                return false;
            }

            if (prevTrimmed.StartsWith("##"))
            {
                return !IsFileLevelDocComment(nonBlank, hadBlankAbove, curIdx);
            }

            return !hadBlankAbove[curIdx];
        }

        /// <summary>
        /// Determines whether the current doc-comment block (ending at
        /// curIdx-1) is a file-level doc comment. A doc comment is
        /// file-level when the nearest preceding non-doc-comment line
        /// is a file header.
        /// </summary>
        /// <param name="nonBlank">The list of non-blank entries.</param>
        /// <param name="hadBlankAbove">Per-entry flag indicating whether a blank line existed above the entry in the original input.</param>
        /// <param name="curIdx">The current index in the non-blank list.</param>
        /// <returns>True if the doc-comment block ending just before curIdx is file-level.</returns>
        private static bool IsFileLevelDocComment(
            List<NonBlankEntry> nonBlank, List<bool> hadBlankAbove, int curIdx)
        {
            for (int j = curIdx - 1; j >= 0; j--)
            {
                string trimmed = nonBlank[j].Line.Trim();

                if (!trimmed.StartsWith("##"))
                {
                    return DeclarationClassifier.Instance.IsFileHeaderLine(trimmed);
                }

                if (hadBlankAbove[j])
                {
                    if (j > 0 &&
                        nonBlank[j - 1].Line.Trim().StartsWith("##"))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
