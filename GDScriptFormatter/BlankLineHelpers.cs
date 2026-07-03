using System.Collections.Generic;

using static GDScriptFormatter.DeclarationClassifier;

namespace GDScriptFormatter
{
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// Determines whether a trimmed line is a plain single-line GDScript
        /// statement: non-empty, not a comment, not a block-start, not an
        /// annotation, not a func/class declaration, and not a file header.
        /// </summary>
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

            if (TextUtils.IsBlockStartLine(trimmed))
            {
                return false;
            }

            if (trimmed.StartsWith("@"))
            {
                return false;
            }

            if (IsFuncOrClassDecl(trimmed))
            {
                return false;
            }

            if (IsFileHeaderLine(trimmed))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether a trimmed line is a standalone annotation line:
        /// starts with @ but does NOT contain a declaration keyword (var, func, signal,
        /// const, enum, class, static) on the same line.
        /// For example, "@warning_ignore("unused_signal")" is standalone,
        /// "@export_storage var x := 0" is NOT standalone (it has "var").
        /// </summary>
        private static bool IsStandaloneAnnotation(string trimmed)
        {
            if (!trimmed.StartsWith("@"))
            {
                return false;
            }

            // Find the first space after the annotation prefix
            int spaceIdx = trimmed.IndexOf(' ');

            if (spaceIdx < 0)
            {
                return true; // Just @something without any keyword
            }

            string rest = trimmed.Substring(spaceIdx + 1).TrimStart();
            // If after the @ annotation there's a declaration keyword, it's combined
            return !TextUtils.StartsWithKeyword(rest, "var") &&
                !TextUtils.StartsWithKeyword(rest, "func") &&
                !TextUtils.StartsWithKeyword(rest, "signal") &&
                !TextUtils.StartsWithKeyword(rest, "const") &&
                !TextUtils.StartsWithKeyword(rest, "enum") &&
                !TextUtils.StartsWithKeyword(rest, "class") &&
                !TextUtils.StartsWithKeyword(rest, "static");
        }

        /// <summary>
        /// Determines whether a preceding comment line is attached to the current declaration.
        /// Doc comment lines (starting with ##) are always force-attached to a following declaration
        /// regardless of whether a blank line originally separated them. Single-# comments are
        /// attached only when no blank line originally separated them.
        /// </summary>
        private static bool IsAttachedComment(string prevTrimmed,
            string curTrimmed, List<NonBlankEntry> nonBlank, int curIdx)
        {
            if (!prevTrimmed.StartsWith("#"))
            {
                return false;
            }

            if (!IsDeclarationLine(curTrimmed))
            {
                return false;
            }

            // Doc comments (##) are force-attached unless they're file-level.

            if (prevTrimmed.StartsWith("##"))
            {
                return !IsFileLevelDocComment(nonBlank, curIdx);
            }

            // Single-# comments are attached only when no blank line
            // originally separated them.
            return !nonBlank[curIdx].HadBlankAbove;
        }

        /// <summary>
        /// Determines whether the current doc-comment block (ending at curIdx-1)
        /// is a file-level doc comment. A doc comment is file-level when the
        /// nearest preceding non-doc-comment line is a file header.
        /// </summary>
        private static bool IsFileLevelDocComment(
            List<NonBlankEntry> nonBlank, int curIdx)
        {
            // Scan backwards from the line just before curIdx (which is
            // the last line of the doc-comment block) to find the first
            // non-doc-comment line.

            for (int j = curIdx - 1; j >= 0; j--)
            {
                string trimmed = nonBlank[j].Line.Trim();

                if (!trimmed.StartsWith("##"))
                {
                    return IsFileHeaderLine(trimmed);
                }

                // If this ## line had a blank line above it in the
                // original, it may mark the start of a new doc comment
                // block. Only treat it as non-file-level when the
                // blank separates two ## blocks; if it separates a
                // file header from this ##, the doc comment is still
                // file-level.

                if (nonBlank[j].HadBlankAbove)
                {
                    if (j > 0 &&
                        nonBlank[j - 1].Line.Trim().StartsWith("##"))
                    {
                        return false;
                    }

                    // Blank above was from a file header — this
                    // doc comment is still file-level.
                    continue;
                }
            }

            // Entire file up to curIdx consists of doc comments
            // (no file header found). Treat as file-level.
            return true;
        }
    }
}
