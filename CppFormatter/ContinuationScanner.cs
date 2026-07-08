using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Continuation scanning helpers for indentation processing.
    /// Detects whether a line ends with a continuation indicator, scans for
    /// code-region characters, and distinguishes label lines from ternary
    /// continuations.
    /// </summary>
    internal static class ContinuationScanner
    {
        /// <summary>
        /// Determines whether the given line ends with a continuation indicator.
        /// Scans backward for the last code-region non-whitespace character so
        /// that trailing comments do not mask the real indicator. Recognized
        /// operators: <c>,</c>, <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>,
        /// <c>%</c>, <c>(</c>, <c>=</c>, <c>?</c>, <c>&lt;</c>, <c>&gt;</c>,
        /// <c>:</c> (unless a label), <c>&amp;&amp;</c>, <c>||</c>.
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

            if (last == ':')
            {
                return !IsLabelLine(line.Substring(0, lastCodeIdx + 1));
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

        /// <summary>
        /// Determines whether a line contains at least one code-region
        /// character (excluding whitespace). Useful for checking whether a
        /// line is a pure string/comment continuation that transparently
        /// passes the continuation chain through to the preceding line.
        /// </summary>
        internal static bool HasCodeChar(string line, int lineStart,
            string text, bool[] isCode)
        {
            for (int i = 0; i < line.Length; i++)
            {
                int textPos = lineStart + i;

                if (textPos < 0 || textPos >= isCode.Length ||
                    !isCode[textPos])
                {
                    continue;
                }

                char c = line[i];

                if (c != ' ' && c != '\t')
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the index of the last non-whitespace code-region character in
        /// the line. Scans backward from the end of <paramref name="line"/>,
        /// skipping positions whose corresponding <paramref name="isCode"/>
        /// entry is false and skipping space/tab characters. Correctly handles
        /// trailing comments (e.g., <c>code, // comment</c>).
        /// </summary>
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
        /// Determines whether a line that ends with ':' is a label line
        /// (access specifier, default label, case label, or plain identifier
        /// label) rather than a ternary-operator continuation.
        /// Also detects constructor initializer list lines starting with ':'
        /// followed by member initializer content.
        /// The input is fully trimmed (both leading and trailing) to handle
        /// re-indented lines that carry leading whitespace.
        /// </summary>
        internal static bool IsLabelLine(string line)
        {
            string trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed == "public:" || trimmed == "private:" ||
                trimmed == "protected:")
            {
                return true;
            }

            if (trimmed == "default:")
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "case"))
            {
                return true;
            }

            // Check for constructor initializer list colon:
            // Lines starting with ':' followed by member initializer content
            // Pattern: ": member_(args)" or ": member_{args}" etc.

            if (trimmed.StartsWith(":") && trimmed.Length > 1)
            {
                string afterColon = trimmed.Substring(1).TrimStart();

                if (TextUtils.LooksLikeMemberInitializer(afterColon))
                {
                    return true;
                }
            }

            if (trimmed.EndsWith(":") && trimmed.Length > 1)
            {
                string label = trimmed.Substring(0, trimmed.Length - 1);

                if (TextUtils.IsPureIdentifier(label))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
