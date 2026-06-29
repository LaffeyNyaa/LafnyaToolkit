namespace JavaFormatter
{
    /// <summary>
    /// Classifies source lines by structural role (block start/end, import
    /// directive, continuation, etc.) and inspects the last code-region
    /// character of a line. All inspections are token-aware via the code mask
    /// so that comment and string content is never mistaken for code.
    /// </summary>
    internal static class LineClassifier
    {
        /// <summary>Keywords that introduce a block-start line.</summary>
        private static readonly string[] BlockStartKeywords =
            {
            "package", "interface", "synchronized", "finally", "abstract",
                "implements", "extends", "throws", "class", "switch", "catch",
                "enum", "while", "else", "for", "try", "do", "if"
            };

        /// <summary>
        /// Determines whether the trimmed line is a block start line: a non-empty,
        /// non-brace-only, non-annotation line that starts with a block keyword.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a block start; otherwise false.</returns>
        internal static bool IsBlockStartLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed == "{")
            {
                return false;
            }

            if (trimmed.EndsWith(";"))
            {
                return false;
            }

            if (trimmed.StartsWith("@"))
            {
                return false;
            }

            foreach (var kw in BlockStartKeywords)
            {
                if (TextUtils.StartsWithKeyword(trimmed, kw))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the trimmed line is a block end line: exactly "}" or "};".
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a block end; otherwise false.</returns>
        internal static bool IsBlockEndLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed == "}")
            {
                return true;
            }

            if (trimmed == "};")
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the trimmed line is an import directive.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is an import directive; otherwise false.</returns>
        internal static bool IsImportDirective(string trimmed)
        {
            if (trimmed.StartsWith("import "))
            {
                return true;
            }

            if (trimmed.StartsWith("import\t"))
            {
                return true;
            }

            return trimmed == "import";
        }

        /// <summary>
        /// Determines whether the trimmed line is a do-while tail: starts with the
        /// "while" keyword and ends with ");".
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a do-while tail; otherwise false.</returns>
        internal static bool IsDoWhileTail(string trimmed)
        {
            if (!TextUtils.StartsWithKeyword(trimmed, "while"))
            {
                return false;
            }

            return trimmed.EndsWith(");");
        }

        /// <summary>
        /// Determines whether the trimmed line is a block continuation keyword:
        /// catch, finally, or else.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a block continuation; otherwise false.</returns>
        internal static bool IsBlockContinuation(string trimmed)
        {
            return TextUtils.StartsWithKeyword(trimmed, "catch") ||
                TextUtils.StartsWithKeyword(trimmed, "finally") ||
                TextUtils.StartsWithKeyword(trimmed, "else");
        }

        /// <summary>
        /// Finds the index of the last non-whitespace code-region character in
        /// the line. Scans backward from the end of <paramref name="line"/>,
        /// skipping positions whose corresponding <paramref name="isCode"/>
        /// entry is false and skipping space/tab characters. Correctly handles
        /// trailing comments (e.g., <c>code, // comment</c>).
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The starting offset of this line in
        /// <paramref name="text"/>.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask of <paramref name="text"/>.</param>
        /// <returns>The index in <paramref name="line"/> of the last code-region
        /// non-whitespace character, or -1 if none exists.</returns>
        internal static int LastCodeCharIndex(string line, int lineStart,
            string text, bool[] isCode)
        {
            for (int i = line.Length - 1; i >= 0; i--)
            {
                int textPos = lineStart + i;

                if (textPos < 0 || textPos >= isCode.Length ||
                    !isCode[textPos])
                {
                    continue;
                }

                char c = line[i];

                if (c == ' ' || c == '\t')
                {
                    continue;
                }

                return i;
            }

            return -1;
        }

        /// <summary>
        /// Determines whether the specified line ends with a continuation
        /// indicator within a code region. Recognized operators: <c>,</c>,
        /// <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>, <c>%</c>, <c>(</c>,
        /// <c>=</c>, <c>?</c>, <c>&lt;</c>, <c>&gt;</c> (covers generics and
        /// binary comparisons), <c>&amp;&amp;</c>, <c>||</c>. Compound
        /// assignment operators (<c>==</c>, <c>!=</c>, <c>&lt;=</c>,
        /// <c>&gt;=</c>, <c>+=</c>, <c>-=</c>) end with <c>=</c> and are thus
        /// covered. Enum member lines (inside an enum block) are excluded by
        /// the caller via <c>ComputeInEnumBlock</c>.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The starting offset of this line in
        /// <paramref name="text"/>.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>true if the line ends with a continuation indicator;
        /// otherwise false.</returns>
        internal static bool IsContinuationIndicator(string line, int lineStart,
            string text, bool[] isCode)
        {
            int lastCodeIdx = LastCodeCharIndex(line, lineStart, text, isCode);

            if (lastCodeIdx < 0)
            {
                return false;
            }

            char last = line[lastCodeIdx];

            if (last == ',' || last == '+' || last == '-' || last == '*' ||
                last == '/' || last == '%' || last == '(' || last == '=' ||
                last == '?' || last == '<' || last == '>')
            {
                return true;
            }

            if (lastCodeIdx < 1)
            {
                return false;
            }

            int prevTextPos = lineStart + lastCodeIdx - 1;

            if (prevTextPos < 0 || prevTextPos >= isCode.Length ||
                !isCode[prevTextPos])
            {
                return false;
            }

            string last2 = line.Substring(lastCodeIdx - 1, 2);
            return last2 == "&&" || last2 == "||";
        }
    }
}
