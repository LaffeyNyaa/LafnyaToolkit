using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Computes the brace-nesting depth for each line of source text.
    /// Adjusts depth based on namespace declarations (no extra indent for
    /// namespace bodies) and handles closing braces that reduce depth below
    /// the line-start depth.
    ///
    /// Include-guard #ifndef blocks (#ifndef NAME immediately followed by
    /// #define NAME) are detected and do NOT add an indentation level; code
    /// inside an include guard stays at the enclosing scope depth.
    /// </summary>
    internal static class IndentationDepthComputer
    {
        /// <summary>
        /// Computes the base brace-nesting depth for each line. The first pass
        /// walks every character in <paramref name="text"/>, incrementing depth
        /// on <c>{</c> and decrementing on <c>}</c> (only in code regions),
        /// and recording the depth at the start of each line.
        ///
        /// Namespace bodies do not increase depth (they match the enclosing
        /// scope's indentation level).
        ///
        /// A second pass scans preprocessor conditional directives
        /// (#if/#ifdef/#ifndef/#elif/#else/#endif) and adjusts depths so that
        /// code inside a conditional block receives an extra indentation level.
        /// Directive lines themselves retain the enclosing scope depth.
        /// Include-guard #ifndef blocks are detected and skipped (they do not
        /// contribute an indentation level).
        /// </summary>
        internal static int[] ComputeDepths(List<string> lines, string text,
            bool[] isCode)
        {
            int[] depths = new int[lines.Count];
            int depth = 0;
            int lineIdx = 0;
            bool pendingNamespace = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\n')
                {
                    lineIdx++;

                    if (lineIdx < depths.Length)
                    {
                        depths[lineIdx] = depth;
                    }

                    continue;
                }

                if (isCode[i] && c == '{')
                {
                    if (pendingNamespace)
                    {
                        pendingNamespace = false;
                    }
                    else
                    {
                        depth++;
                    }
                }
                else if (isCode[i] && c == '}')
                {
                    depth--;

                    if (depth < 0)
                    {
                        depth = 0;
                    }

                    // Only update depths[lineIdx] when the closing brace
                    // reduces depth below what was recorded at the start of
                    // this line.  This prevents a `}` that merely closes a
                    // `{` _on the same line_ (e.g. inside {{"x", y}}) from
                    // overwriting the line-start depth that was correctly
                    // set by the preceding `\n` handler.

                    if (lineIdx < depths.Length)
                    {
                        int startDepth = depths[lineIdx];

                        if (depth < startDepth)
                        {
                            depths[lineIdx] = depth;
                        }
                    }
                }

                if (isCode[i] && c == 'n' &&
                    (i == 0 || !TextUtils.IsWordChar(text[i - 1])) &&
                    TextUtils.MatchesWord(text, i, "namespace"))
                {
                    pendingNamespace = true;
                }

                // Reset pendingNamespace if we encounter characters that
                // terminate a namespace declaration (; for alias, = for
                // assignment, or non-identifier chars that are not { or :).

                if (pendingNamespace && c == ';')
                {
                    pendingNamespace = false;
                }

                if (pendingNamespace && c == '=')
                {
                    pendingNamespace = false;
                }

                if (pendingNamespace && c == '(')
                {
                    pendingNamespace = false;
                }
            }

            // Second pass: adjust depths for preprocessor conditional
            // directives (#if/#ifdef/#ifndef/#elif/#else/#endif).
            // Directive lines keep the enclosing scope depth; code lines
            // inside the conditional get an extra indent per nesting level.
            // Include-guard #ifndef blocks are detected and skipped so they
            // do not contribute an indentation level (the #define guard-name
            // that follows immediately is not "code" that needs indenting).
            int preprocDepth = 0;
            // Stack tracks whether each nested preprocessor conditional level
            // is an include-guard. true = this level is a header guard and
            // should not contribute to preprocDepth.
            var isHeaderGuardLevel = new List<bool>();

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].TrimStart();
                string keyword = GetPreprocessorKeyword(trimmed);

                if (keyword == "ifndef" && IsHeaderGuard(lines, i))
                {
                    depths[i] += preprocDepth;
                    isHeaderGuardLevel.Add(true);
                }
                else if (keyword == "if" || keyword == "ifdef" ||
                    keyword == "ifndef")
                {
                    depths[i] += preprocDepth;
                    isHeaderGuardLevel.Add(false);
                    preprocDepth++;
                }
                else if (keyword == "elif" || keyword == "else")
                {
                    // Use the enclosing level's depth. If the enclosing
                    // #if/#ifdef/#ifndef is a header guard, preprocDepth was
                    // not incremented, so use preprocDepth directly.
                    // Otherwise use (preprocDepth - 1).
                    bool enclosingIsHeaderGuard =
                        isHeaderGuardLevel.Count > 0 &&
                        isHeaderGuardLevel[isHeaderGuardLevel.Count - 1];

                    if (enclosingIsHeaderGuard)
                    {
                        depths[i] += preprocDepth;
                    }
                    else if (preprocDepth > 0)
                    {
                        depths[i] += (preprocDepth - 1);
                    }
                }
                else if (keyword == "endif")
                {
                    bool wasHeaderGuard = false;

                    if (isHeaderGuardLevel.Count > 0)
                    {
                        int lastIdx = isHeaderGuardLevel.Count - 1;
                        wasHeaderGuard = isHeaderGuardLevel[lastIdx];
                        isHeaderGuardLevel.RemoveAt(lastIdx);
                    }

                    if (wasHeaderGuard)
                    {
                        depths[i] += preprocDepth;
                    }
                    else if (preprocDepth > 0)
                    {
                        depths[i] += (preprocDepth - 1);
                        preprocDepth--;
                    }
                }
                else
                {
                    depths[i] += preprocDepth;
                }
            }

            return depths;
        }

        /// <summary>
        /// Determines whether the #ifndef at the given index is an include
        /// guard. An include guard has the pattern:
        ///
        ///     #ifndef NAME
        ///     #define NAME
        ///
        /// where NAME is the same identifier on both directives, and the
        /// #ifndef is at file scope (brace depth 0 before adjustment).
        /// </summary>
        private static bool IsHeaderGuard(List<string> lines, int ifndefIndex)
        {
            // Only detect header guards at file scope (brace depth 0).
            // depths[ifndefIndex] is the brace depth from pass 1, which
            // has not yet been adjusted by the preprocessor pass.

            if (ifndefIndex >= lines.Count)
            {
                return false;
            }

            string trimmed = lines[ifndefIndex].TrimStart();

            string afterIfndef =
                trimmed.Substring("#ifndef".Length).TrimStart();

            if (afterIfndef.Length == 0)
            {
                return false;
            }

            string guardName = ExtractPreprocessorIdentifier(afterIfndef);

            if (guardName.Length == 0)
            {
                return false;
            }

            // Scan forward for the next non-blank, non-comment line.

            for (int j = ifndefIndex + 1; j < lines.Count; j++)
            {
                string nextTrimmed = lines[j].TrimStart();

                if (nextTrimmed.Length == 0)
                {
                    continue;
                }

                // Skip single-line comments.

                if (nextTrimmed.StartsWith("//"))
                {
                    continue;
                }

                // Skip block-comment start lines.

                if (nextTrimmed.StartsWith("/*"))
                {
                    continue;
                }

                string nextKeyword = GetPreprocessorKeyword(nextTrimmed);

                if (nextKeyword == "define")
                {
                    string afterDefine = nextTrimmed.Substring(
                        "#define".Length).TrimStart();

                    string defineName = ExtractPreprocessorIdentifier(
                        afterDefine);

                    return defineName == guardName;
                }

                // Any other non-blank, non-comment line means this is not
                // an include guard.
                break;
            }

            return false;
        }

        /// <summary>
        /// Extracts the first identifier-like token from a string
        /// (sequence of letters, digits, and underscores).
        /// </summary>
        private static string ExtractPreprocessorIdentifier(string s)
        {
            int end = 0;

            while (end < s.Length &&
                (char.IsLetterOrDigit(s[end]) || s[end] == '_'))
            {
                end++;
            }

            return s.Substring(0, end);
        }

        /// <summary>
        /// Extracts the preprocessor directive keyword from a trimmed line.
        /// Returns <c>null</c> if the line does not start with <c>#</c>
        /// followed by a letter-based keyword.
        /// </summary>
        private static string GetPreprocessorKeyword(string trimmedLine)
        {
            if (trimmedLine.Length == 0 || trimmedLine[0] != '#')
            {
                return null;
            }

            string afterHash = trimmedLine.Substring(1).TrimStart();

            if (afterHash.Length == 0)
            {
                return null;
            }

            int kwEnd = 0;

            while (kwEnd < afterHash.Length &&
                char.IsLetter(afterHash[kwEnd]))
            {
                kwEnd++;
            }

            if (kwEnd == 0)
            {
                return null;
            }

            return afterHash.Substring(0, kwEnd);
        }
    }
}
