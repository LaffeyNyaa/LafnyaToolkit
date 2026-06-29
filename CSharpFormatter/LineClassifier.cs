using System.Collections.Generic;

namespace CSharpFormatter
{
    /// <summary>
    /// Line classification and judgement helpers used across the
    /// formatting modules. These routines classify trimmed lines
    /// (block-start, block-end, case label, using directive) and
    /// inspect code-region characters to detect statement terminators
    /// and continuation indicators.
    /// </summary>
    internal static class LineClassifier
    {
        /// <summary>
        /// Determines whether a trimmed line is a using directive.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>true if the line is a using directive.</returns>
        internal static bool IsUsingDirective(string trimmed)
        {
            if (trimmed.StartsWith("using "))
            {
                return true;
            }

            if (trimmed.StartsWith("using\t"))
            {
                return true;
            }

            return trimmed == "using";
        }

        /// <summary>
        /// Determines whether a trimmed line is a block-start line (starts
        /// with a declaration or control-flow keyword). This is a text-only
        /// check; callers should also verify the line is in a code region.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>true if the line starts a block.</returns>
        internal static bool IsBlockStartLine(string trimmed)
        {
            if (trimmed.Length == 0 || trimmed == "{")
            {
                return false;
            }

            if (trimmed.EndsWith(";"))
            {
                return false;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "using") &&
                !trimmed.Contains("("))
            {
                return false;
            }

            string[] keywords =
                {
                "namespace", "interface", "unchecked", "finally",
                    "foreach", "checked", "struct", "switch", "catch",
                    "class", "while", "unsafe", "using", "enum", "else",
                    "for", "try", "do", "if", "lock", "fixed"
                };

            foreach (var kw in keywords)
            {
                if (TextUtils.StartsWithKeyword(trimmed, kw))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a trimmed line is a block-end line: exactly
        /// <c>}</c> or <c>};</c>.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>true if the line is a block end.</returns>
        internal static bool IsBlockEndLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            return trimmed == "}" || trimmed == "};";
        }

        /// <summary>
        /// Determines whether a trimmed line is a switch case/default label
        /// line. This is a text-only check; callers should also verify the
        /// line is in a code region.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>true if the line is a case label.</returns>
        internal static bool IsCaseLabelLine(string trimmed)
        {
            if (trimmed.Length == 0 || !trimmed.EndsWith(":"))
            {
                return false;
            }

            return TextUtils.StartsWithKeyword(trimmed, "case") ||
                TextUtils.StartsWithKeyword(trimmed, "default");
        }

        /// <summary>
        /// Computes a per-line flag indicating whether the first
        /// non-whitespace character of each line falls within a code
        /// region. Used by token-aware blank-line and case-scope rules.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="isCode">The code mask of the full text.</param>
        /// <returns>A boolean array; true means the line's first
        /// non-whitespace character is in a code region.</returns>
        internal static bool[] ComputeIsCodeLine(List<string> lines,
            bool[] isCode)
        {
            var isCodeLine = new bool[lines.Count];
            int[] lineStarts = TextUtils.ComputeLineStarts(lines);

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                int firstNonWs = 0;

                while (firstNonWs < line.Length &&
                    (line[firstNonWs] == ' ' || line[firstNonWs] == '\t'))
                {
                    firstNonWs++;
                }

                if (firstNonWs < line.Length)
                {
                    int textPos = lineStarts[i] + firstNonWs;

                    isCodeLine[i] = textPos < isCode.Length &&
                        isCode[textPos];
                }
            }

            return isCodeLine;
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
        /// Determines whether the line ends with a statement terminator
        /// (<c>;</c> or <c>}</c>) within a code region.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The starting offset of this line in
        /// <paramref name="text"/>.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>true if the last code-region character is <c>;</c> or
        /// <c>}</c>; otherwise false.</returns>
        internal static bool EndsStatement(string line, int lineStart,
            string text, bool[] isCode)
        {
            int idx = LastCodeCharIndex(line, lineStart, text, isCode);

            if (idx < 0)
            {
                return false;
            }

            char last = line[idx];
            return last == ';' || last == '}';
        }

        /// <summary>
        /// Determines whether the specified line ends with a continuation
        /// indicator within a code region. Recognized operators: <c>,</c>,
        /// <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>, <c>%</c>, <c>(</c>,
        /// <c>=</c>, <c>?</c>, <c>&lt;</c>, <c>&gt;</c> (covers <c>=&gt;</c>),
        /// <c>&amp;&amp;</c>, <c>||</c>. Compound assignment operators
        /// (<c>==</c>, <c>!=</c>, <c>&lt;=</c>, <c>&gt;=</c>, <c>+=</c>,
        /// <c>-=</c>) end with <c>=</c> and are thus covered.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The starting offset of this line in
        /// <paramref name="text"/>.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>true if the line ends with a continuation indicator;
        /// otherwise false.</returns>
        internal static bool IsContinuationIndicator(string line,
            int lineStart, string text, bool[] isCode)
        {
            int lastCodeIdx = LastCodeCharIndex(line, lineStart, text,
                isCode);

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
