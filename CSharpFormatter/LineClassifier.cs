using System.Collections.Generic;

using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace CSharpFormatter
{
    /// <summary>
    /// Line classification and judgement helpers used across the
    /// formatting modules. These routines classify trimmed lines
    /// (block-start, block-end, case label, using directive) and
    /// inspect code-region characters to detect statement terminators
    /// and continuation indicators.
    /// </summary>
    internal sealed class LineClassifier
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly LineClassifier Instance = new LineClassifier();

        private LineClassifier()
        {
        }

        /// <summary>
        /// Determines whether a trimmed line is a <c>using</c>
        /// directive.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a using directive.</returns>
        internal bool IsUsingDirective(string trimmed)
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
        /// Determines whether a trimmed line is a block-start line
        /// (starts with a declaration or control-flow keyword). This
        /// is a text-only check; callers should also verify the line
        /// is in a code region.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line starts a block.</returns>
        internal bool IsBlockStartLine(string trimmed)
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
        /// Determines whether a trimmed line is a block-end line:
        /// exactly <c>}</c> or <c>};</c>.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a block end.</returns>
        internal bool IsBlockEndLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            return trimmed == "}" || trimmed == "};";
        }

        /// <summary>
        /// Determines whether a trimmed line is a switch
        /// <c>case</c>/<c>default</c> label line. This is a text-only
        /// check; callers should also verify the line is in a code
        /// region. Recognises both the standalone form (<c>case X:</c>,
        /// <c>default:</c>) and the form where the case body is on
        /// the same line (<c>case X: body;</c>, <c>default: body;</c>).
        /// The latter recognition is required so that the case body
        /// indent is re-applied on subsequent format passes after
        /// <see cref="LineLengthProcessor"/> has split a long
        /// <c>case</c> label at the <c>;</c>.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a case label.</returns>
        internal bool IsCaseLabelLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed.EndsWith(":"))
            {
                return TextUtils.StartsWithKeyword(trimmed, "case") ||
                    TextUtils.StartsWithKeyword(trimmed, "default");
            }

            if (TextUtils.StartsWithKeyword(trimmed, "case"))
            {
                return FindCaseLabelColon(trimmed, "case".Length) > 0;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "default"))
            {
                return StartsWithDefaultColon(trimmed);
            }

            return false;
        }

        /// <summary>
        /// Scans the line for the first <c>:</c> at paren/bracket
        /// depth 0 starting from the position after the case label
        /// keyword. Returns the index of the colon, or -1 if none is
        /// found before the end of the line.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <param name="startAfter">The index after the case keyword.</param>
        /// <returns>The colon index, or -1.</returns>
        private static int FindCaseLabelColon(
            string trimmed,
            int startAfter
        )
        {
            int depth = 0;
            int i = startAfter;

            while (i < trimmed.Length)
            {
                char c = trimmed[i];

                if (c == '(' || c == '[' || c == '{')
                {
                    depth++;
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                }
                else if (c == ':' && depth == 0)
                {
                    return i;
                }

                i++;
            }

            return -1;
        }

        /// <summary>
        /// Determines whether the line, after the <c>default</c>
        /// keyword and any whitespace, starts with a colon (i.e. the
        /// default case label form). Lines like
        /// <c>defaultAction()</c> or <c>default(T)</c> are not case
        /// labels and return false.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a default case label.</returns>
        private static bool StartsWithDefaultColon(string trimmed)
        {
            int i = "default".Length;

            while (i < trimmed.Length && (trimmed[i] == ' ' ||
                trimmed[i] == '\t'))
            {
                i++;
            }

            return i < trimmed.Length && trimmed[i] == ':';
        }

        /// <summary>
        /// Computes a per-line flag indicating whether the first
        /// non-whitespace character of each line falls within a code
        /// region. Used by token-aware blank-line and case-scope
        /// rules.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="isCode">The code mask of the full text.</param>
        /// <returns>A boolean array; true means the line's first non-whitespace character is in a code region.</returns>
        internal bool[] ComputeIsCodeLine(
            List<string> lines,
            bool[] isCode
        )
        {
            var isCodeLine = new bool[lines.Count];

            int[] lineStarts =
                CSharpTokenizer.Instance.ComputeLineStarts(lines);

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
        /// Finds the index of the last non-whitespace code-region
        /// character in the line. Scans backward from the end of
        /// <paramref name="line"/>, skipping positions whose
        /// corresponding <paramref name="isCode"/> entry is false and
        /// skipping space/tab characters. Correctly handles trailing
        /// comments (e.g., <c>code, // comment</c>).
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The starting offset of this line in <paramref name="text"/>.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask of <paramref name="text"/>.</param>
        /// <returns>The index in <paramref name="line"/> of the last code-region non-whitespace character, or -1 if none exists.</returns>
        internal int LastCodeCharIndex(
            string line,
            int lineStart,
            string text,
            bool[] isCode
        )
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
        /// Determines whether the line ends with a statement
        /// terminator (<c>;</c> or <c>}</c>) within a code region.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The starting offset of this line in <paramref name="text"/>.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>True if the last code-region character is <c>;</c> or <c>}</c>; otherwise false.</returns>
        internal bool EndsStatement(
            string line,
            int lineStart,
            string text,
            bool[] isCode
        )
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
        /// Determines whether the specified line ends with a
        /// continuation indicator within a code region. Recognized
        /// operators: <c>,</c>, <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>,
        /// <c>%</c>, <c>(</c>, <c>=</c>, <c>?</c>, <c>&lt;</c>,
        /// <c>&gt;</c> (covers <c>=&gt;</c>), <c>&amp;&amp;</c>,
        /// <c>||</c>. Compound assignment operators (<c>==</c>,
        /// <c>!=</c>, <c>&lt;=</c>, <c>&gt;=</c>, <c>+=</c>, <c>-=</c>)
        /// end with <c>=</c> and are thus covered.
        ///
        /// A trailing <c>;</c> is also treated as a continuation
        /// indicator when the line has more opening than closing
        /// parentheses in its code region. This covers the
        /// separator-style <c>;</c> inside the header of a multi-line
        /// <c>for</c> statement (e.g. <c>for (a; b;</c>) where the
        /// terminating <c>)</c> lives on the following line, ensuring
        /// that the continuation indent is re-applied on every
        /// re-format pass.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The starting offset of this line in <paramref name="text"/>.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>True if the line ends with a continuation indicator; otherwise false.</returns>
        internal bool IsContinuationIndicator(
            string line,
            int lineStart,
            string text,
            bool[] isCode
        )
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

            if (last2 == "&&" || last2 == "||")
            {
                return true;
            }

            if (last == ';' &&
                CountUnbalancedParens(
                    line,
                    lineStart,
                    text,
                    isCode
                )> 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Counts the net number of unclosed parentheses on the
        /// line: +1 for each code-region <c>(</c> and -1 for each
        /// code-region <c>)</c>. A positive result means the line
        /// leaves at least one paren open for a later line to close,
        /// which signals that the line is itself a continuation.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="lineStart">The starting offset of this line in <paramref name="text"/>.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <returns>The net open-paren count on the line.</returns>
        private static int CountUnbalancedParens(
            string line,
            int lineStart,
            string text,
            bool[] isCode
        )
        {
            int count = 0;

            for (int i = 0; i < line.Length; i++)
            {
                int textPos = lineStart + i;

                if (textPos < 0 || textPos >= isCode.Length ||
                    !isCode[textPos])
                {
                    continue;
                }

                char c = line[i];

                if (c == '(')
                {
                    count++;
                }
                else if (c == ')')
                {
                    count--;
                }
            }

            return count;
        }
    }
}
