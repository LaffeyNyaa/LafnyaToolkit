using System.Collections.Generic;

using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace GDScriptFormatter
{
    /// <summary>
    /// Per-line analysis for the indentation reindent step. Provides
    /// <see cref="ComputeLineInfo"/> which scans each line and
    /// records its bracket-depth transitions, colon/brace
    /// termination, and continuation status. This is the largest
    /// helper in the indentation pipeline; it lives in its own file
    /// to keep individual source files under 600 lines.
    /// </summary>
    public sealed partial class IndentationProcessor
    {
        /// <summary>
        /// Computes the starting offset of each line in the joined
        /// text representation.
        /// </summary>
        /// <param name="lines">The lines to compute starts for.</param>
        /// <returns>An array of starting offsets, one per line.</returns>
        public int[] ComputeLineStarts(List<string> lines)
        {
            var lineStarts = new int[lines.Count];
            int pos = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                lineStarts[i] = pos;
                pos += lines[i].Length;

                if (i < lines.Count - 1)
                {
                    pos++;
                }
            }

            return lineStarts;
        }

        /// <summary>
        /// Analyzes per-line properties: colon/brace termination,
        /// continuation, and original indentation depth. Continuation
        /// detection is based on parenthesis, square bracket, and
        /// brace depth. A line ending with a trailing {
        /// (<see cref="LineAnalysis.BraceTerminated"/>) does NOT
        /// increment the bracket depth — its body is indented via the
        /// stack — so that block-style dicts are not double-indented.
        /// Inline-open dicts/braces (e.g. "var m = {k: v,") DO
        /// increment the depth so that subsequent continuation lines
        /// are detected and preserved as continuations.
        /// </summary>
        /// <param name="lines">The line list.</param>
        /// <param name="text">The full text corresponding to the lines.</param>
        /// <param name="isCode">The code mask of text.</param>
        /// <param name="lineStarts">The starting offsets of each line in text.</param>
        /// <returns>The per-line analysis.</returns>
        public LineAnalysis[] ComputeLineInfo(
            List<string> lines,
            string text,
            bool[] isCode,
            int[] lineStarts
        )
        {
            var info = new LineAnalysis[lines.Count];
            int parenBracketDepth = 0;
            int suppressedBraceDepth = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                int leadingSpaces = line.Length - trimmed.Length;
                int origDepth = leadingSpaces / GDScriptTextUtils.IndentSize;

                info[i].OriginalDepth = origDepth;
                info[i].StartBracketDepth = parenBracketDepth;
                info[i].IsContinuation = parenBracketDepth > 0;

                if (i > 0 &&
                    LineContinuationAnalyzer.Instance.EndsWithBackslash(
                        text,
                        isCode,
                        lineStarts[i - 1],
                        lines[i - 1].Length
                    ))
                {
                    info[i].IsContinuation = true;
                }

                info[i].ColonTerminated = false;
                info[i].BraceTerminated = false;
                info[i].IsCloseBrace = false;

                int firstCodeIdx = -1;
                int lastCodeIdx = -1;

                if (trimmed.Length > 0)
                {
                    int lineEnd = lineStarts[i] + line.Length;

                    for (int ci = lineStarts[i]; ci < lineEnd &&
                        ci < isCode.Length; ci++)
                    {
                        if (isCode[ci])
                        {
                            if (firstCodeIdx < 0)
                            {
                                firstCodeIdx = ci;
                            }

                            lastCodeIdx = ci;
                        }
                    }

                    if (firstCodeIdx >= 0 && text[firstCodeIdx] == '}')
                    {
                        info[i].IsCloseBrace = true;
                    }

                    if (!info[i].IsCloseBrace && trimmed.Length > 0 &&
                        trimmed[0] == '}')
                    {
                        info[i].IsCloseBrace = true;
                    }

                    if (lastCodeIdx >= 0 && text[lastCodeIdx] == '{')
                    {
                        info[i].BraceTerminated = true;
                    }
                }

                for (int ci = lineStarts[i];
                    ci < lineStarts[i] + line.Length && ci < isCode.Length;

                    ci++)
                {
                    if (!isCode[ci])
                    {
                        continue;
                    }

                    char c = text[ci];

                    if (info[i].BraceTerminated && ci == lastCodeIdx)
                    {
                        if (!info[i].IsContinuation)
                        {
                            parenBracketDepth = 0;
                        }

                        suppressedBraceDepth++;
                        continue;
                    }

                    if (c == '(' || c == '[' || c == '{')
                    {
                        parenBracketDepth++;
                    }
                    else if (c == ')' || c == ']')
                    {
                        if (parenBracketDepth > 0)
                        {
                            parenBracketDepth--;
                        }
                    }
                    else if (c == '}')
                    {
                        if (suppressedBraceDepth > 0)
                        {
                            suppressedBraceDepth--;
                        }
                        else if (parenBracketDepth > 0)
                        {
                            parenBracketDepth--;
                        }
                    }
                }

                info[i].EndBracketDepth = parenBracketDepth;

                if (lastCodeIdx >= 0 && parenBracketDepth == 0)
                {
                    int actualLast = lastCodeIdx;

                    while (actualLast >= 0 && actualLast < isCode.Length &&
                        isCode[actualLast] &&
                        char.IsWhiteSpace(text[actualLast]))
                    {
                        actualLast--;
                    }

                    if (actualLast >= 0 && text[actualLast] == ':')
                    {
                        info[i].ColonTerminated = true;
                    }
                }

                if (!info[i].ColonTerminated)
                {
                    CheckColonUnderBrackets(
                        ref info[i],
                        text,
                        isCode,
                        firstCodeIdx,
                        lastCodeIdx
                    );
                }
            }

            return info;
        }

        /// <summary>
        /// Checks whether a colon-terminated line inside brackets
        /// should still be treated as block-starting
        /// (colon-terminated) based on the leading keyword.
        /// Block-starting keywords (func, if, for, while, match, elif,
        /// else) always open a block regardless of bracket context —
        /// especially inside anonymous function bodies.
        /// </summary>
        /// <param name="info">The line analysis to update.</param>
        /// <param name="text">The full text.</param>
        /// <param name="isCode">The code mask of text.</param>
        /// <param name="firstCodeIdx">Index of the first Code-region character on the line.</param>
        /// <param name="lastCodeIdx">Index of the last Code-region character on the line.</param>
        private static void CheckColonUnderBrackets(
            ref LineAnalysis info,
            string text,
            bool[] isCode,
            int firstCodeIdx,
            int lastCodeIdx)
        {
            if (lastCodeIdx < 0 || text[lastCodeIdx] != ':' || firstCodeIdx < 0)
            {
                return;
            }

            int wordStart = firstCodeIdx;

            while (wordStart < text.Length &&
                wordStart < isCode.Length &&
                isCode[wordStart] &&
                char.IsWhiteSpace(text[wordStart]))
            {
                wordStart++;
            }

            if (wordStart >= text.Length ||
                wordStart >= isCode.Length ||
                !isCode[wordStart])
            {
                info.ColonTerminated = true;
                return;
            }

            int wordEnd = wordStart;

            while (wordEnd < text.Length &&
                wordEnd < isCode.Length &&
                isCode[wordEnd] &&
                !char.IsWhiteSpace(text[wordEnd]) &&
                text[wordEnd] != '(' &&
                text[wordEnd] != ':')
            {
                wordEnd++;
            }

            string firstWord = text.Substring(wordStart,
                wordEnd - wordStart);

            if (firstWord == "func" || firstWord == "if" ||
                firstWord == "for" || firstWord == "while" ||
                firstWord == "match" || firstWord == "elif" ||
                firstWord == "else")
            {
                info.ColonTerminated = true;
                return;
            }

            int bracketDepthBeforeColon = 0;

            for (int ci = wordStart;
                ci < lastCodeIdx && ci < isCode.Length; ci++)
            {
                if (!isCode[ci])
                {
                    continue;
                }

                char c = text[ci];

                if (c == '(' || c == '[' || c == '{')
                {
                    bracketDepthBeforeColon++;
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (bracketDepthBeforeColon > 0)
                    {
                        bracketDepthBeforeColon--;
                    }
                }
            }

            if (bracketDepthBeforeColon == 0)
            {
                info.ColonTerminated = true;
            }
        }
    }
}
