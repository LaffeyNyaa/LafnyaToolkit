using System.Collections.Generic;

namespace PythonFormatter
{
    /// <summary>
    /// Multi-line statement blank rule: returns a blank line above a
    /// statement that follows a multi-line statement, OR above the
    /// first line of a multi-line statement that follows a single-line
    /// statement. A statement is considered multi-line when it uses
    /// either explicit backslash continuation (the previous non-blank
    /// line ends with <c>\\</c>) or implicit continuation (the
    /// previous non-blank line is the closing line of a parenthesized/
    /// bracketed/braced expression whose opening bracket is on an
    /// earlier line, OR the current line is the first line of such an
    /// expression and is not itself a continuation). The rule does
    /// not fire for the current line if it is itself at the top of its
    /// indent level or is a decorator.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when the
        /// previous non-blank line ends with a backslash continuation
        /// or is the closing line of a parenthesized/bracketed/braced
        /// expression that started on an earlier line. Dedents are
        /// allowed (the current line may be at a shallower indent than
        /// the previous line, which is the common case when a multi-
        /// line statement closes inside an <c>if</c> body).
        /// </summary>
        /// <param name="entry">The current non-blank entry.</param>
        /// <param name="prevEntry">The previous non-blank entry.</param>
        /// <param name="lineEndDepths">Per-line running bracket depth
        /// at end-of-line. Indexed by the original line index in the
        /// pre-blank-rule line list. A value of <c>0</c> means brackets
        /// are balanced at the end of the line; a positive value means
        /// the line ends with unclosed opening brackets.</param>
        /// <param name="entries">The full list of non-blank entries,
        /// used to walk back to the second-to-previous non-blank
        /// entry.</param>
        /// <param name="currentIndex">The index of <paramref name="entry"/>
        /// within <paramref name="entries"/>.</param>
        internal BlankLineRuleResult ApplyMultiLineStatementBlankRule(
            PythonNonBlankEntry entry, PythonNonBlankEntry prevEntry,
            int[] lineEndDepths, List<PythonNonBlankEntry> entries,
            int currentIndex)
        {
            if (entry.PrevIndent < 0)
            {
                return BlankLineRuleResult.None;
            }

            string trimmed = entry.Line.TrimStart();
            string prevTrimmed = prevEntry.Line.TrimStart();

            if (LineClassifier.Instance.IsDecoratorLine(trimmed))
            {
                return BlankLineRuleResult.None;
            }

            if (LineClassifier.Instance.IsBlockContinuationLine(trimmed))
            {
                return BlankLineRuleResult.None;
            }

            // Explicit backslash continuation: the current line is
            // a continuation of the previous line. Do NOT add a
            // blank line above a continuation (it would break the
            // multi-line statement).

            if (prevTrimmed.EndsWith("\\"))
            {
                return BlankLineRuleResult.None;
            }

            // Implicit continuation: the previous non-blank line is
            // the closing line of a multi-line parenthesized/
            // bracketed/braced expression. Detected by checking that
            // the running bracket depth at the end of the previous
            // non-blank line is back to 0 (or below) while the depth
            // at the end of the second-to-previous non-blank line
            // was positive (an unclosed opening bracket). This must
            // be checked BEFORE the operator-continuation check
            // below, because a wrapped bracketed call is also
            // "indented more than the previous line" but the
            // closing line is the natural end of the multi-line
            // statement and SHOULD be followed by a blank line.

            if (ClosesMultiLineStatement(prevEntry, lineEndDepths, entries,
                currentIndex))
            {
                return BlankLineRuleResult.Decided;
            }

            // Operator-style continuation: the prev line is a
            // continuation of the prevPrev line (the prevPrev line
            // ends with an operator like <c>+</c> or <c>,</c>, or
            // the prev line is indented more than the prevPrev
            // line — e.g. a wrapped long line). The current line is
            // the start of a NEW statement, but the prev-prev
            // line's multi-line statement is still "in progress"
            // from a visual standpoint, so we should not separate
            // it from the next statement with a blank line.

            if (currentIndex >= 2 &&
                IsContinuationOfPreviousMultilineStatement(entries,
                currentIndex))
            {
                return BlankLineRuleResult.None;
            }

            // The current line is the first line of a multi-line
            // statement: it opens a bracket that does not close on
            // the same line, and it is not itself a continuation
            // (no enclosing brackets from previous lines). A blank
            // line is added above such lines so that a single
            // statement split across multiple lines is visually
            // grouped.

            if (IsStartOfMultiLineStatement(entry, lineEndDepths))
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }

        /// <summary>
        /// Returns true when <paramref name="prevEntry"/> is the
        /// closing line of a multi-line parenthesized, bracketed, or
        /// braced expression. The check compares the running bracket
        /// depth at the end of <paramref name="prevEntry"/>'s line
        /// against the depth at the end of the second-to-previous
        /// non-blank entry's line: if the earlier depth was positive
        /// and the later depth is non-positive, the bracket that was
        /// open at the end of the earlier line was closed on
        /// <paramref name="prevEntry"/>'s line, making it the closing
        /// line of a multi-line statement.
        /// </summary>
        private static bool ClosesMultiLineStatement(
            PythonNonBlankEntry prevEntry, int[] lineEndDepths,
            List<PythonNonBlankEntry> entries, int currentIndex)
        {
            int prevDepth = GetLineEndDepth(prevEntry, lineEndDepths);
            // The previous line must end with brackets balanced (or
            // net-closing, which shouldn't happen for a well-formed
            // statement but we accept it).

            if (prevDepth > 0)
            {
                return false;
            }

            // Find the second-to-previous non-blank entry.

            if (currentIndex < 2)
            {
                return false;
            }

            var prevPrevEntry = entries[currentIndex - 2];
            int prevPrevDepth = GetLineEndDepth(prevPrevEntry, lineEndDepths);
            // The earlier line must have ended with at least one
            // unclosed opening bracket, and the gap between the two
            // lines must be a single step (currentIndex - 1 == prevPrev
            // index + 1 is guaranteed by the entries list being
            // dense).
            return prevPrevDepth > 0;
        }

        /// <summary>
        /// Returns the running bracket depth at the end of the given
        /// entry's original line, as precomputed by
        /// <c>ComputeLineEndDepths</c>.
        /// </summary>
        private static int GetLineEndDepth(PythonNonBlankEntry entry,
            int[] lineEndDepths)
        {
            if (entry.OriginalIndex < 0 ||
                entry.OriginalIndex >= lineEndDepths.Length)
            {
                return 0;
            }

            return lineEndDepths[entry.OriginalIndex];
        }

        /// <summary>
        /// Returns true when <paramref name="entry"/> is the first
        /// line of a multi-line statement: the line opens a bracket
        /// (round, square, or curly) that does not close on the same
        /// line (its end-of-line running bracket depth is positive),
        /// and the line is not itself a continuation of a previous
        /// multi-line statement (the running depth at the start of
        /// the line — i.e. the end-of-line depth of the previous
        /// line — is zero).
        /// </summary>
        /// <param name="entry">The current non-blank entry.</param>
        /// <param name="lineEndDepths">Per-line running bracket
        /// depth at end-of-line, as precomputed by
        /// <c>ComputeLineEndDepths</c>.</param>
        private static bool IsStartOfMultiLineStatement(
            PythonNonBlankEntry entry, int[] lineEndDepths)
        {
            int origIdx = entry.OriginalIndex;

            if (origIdx < 0 || origIdx >= lineEndDepths.Length)
            {
                return false;
            }

            // The current line must end with an unclosed opening
            // bracket (i.e. it starts a new bracket that continues
            // onto later lines).
            int endDepth = lineEndDepths[origIdx];

            if (endDepth <= 0)
            {
                return false;
            }

            // The current line must not be a continuation of a
            // previous multi-line statement: the running depth at
            // the start of this line (the end-of-line depth of the
            // previous line) must be zero.
            int startDepth = origIdx > 0
            ? lineEndDepths[origIdx - 1]
            : 0;

            return startDepth == 0;
        }

        /// <summary>
        /// Returns true when the previous non-blank line is a
        /// continuation of a multi-line statement whose first line
        /// was the second-to-previous non-blank line. This happens
        /// when the line-length processor splits a long line —
        /// either via backslash continuation (the second-to-previous
        /// line ends with <c>\</c>) or via operator continuation
        /// (the second-to-previous line ends with an operator like
        /// <c>+</c> or <c>,</c>). The previous non-blank line is the
        /// continuation, and the current line is a new statement
        /// that should not be separated from the still-in-progress
        /// multi-line statement with a blank line. The same is
        /// indicated by the previous line being indented more than
        /// the second-to-previous line.
        /// </summary>
        /// <param name="entries">The full list of non-blank
        /// entries.</param>
        /// <param name="currentIndex">The index of the current
        /// entry within <paramref name="entries"/>.</param>
        private static bool IsContinuationOfPreviousMultilineStatement(
            List<PythonNonBlankEntry> entries, int currentIndex)
        {
            if (currentIndex < 2)
            {
                return false;
            }

            var prevEntry = entries[currentIndex - 1];
            var prevPrevEntry = entries[currentIndex - 2];
            // If the previous line is indented more than the
            // second-to-previous line, it is a continuation
            // (a wrapped line from a long statement).

            if (prevEntry.Indent > prevPrevEntry.Indent)
            {
                return true;
            }

            // The second-to-previous line may also end with a
            // backslash (explicit continuation inserted by the
            // line-length processor).
            string prevPrevTrimmed = prevPrevEntry.Line.TrimStart();

            return prevPrevTrimmed.EndsWith("\\");
        }
    }
}
