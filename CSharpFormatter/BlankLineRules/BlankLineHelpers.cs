using System.Collections.Generic;
using LafnyaToolkit.Core.Text;

namespace CSharpFormatter
{
    /// <summary>
    /// Outcome of a per-rule blank-line decision. Each rule returns one
    /// of these verdicts to the central dispatcher.
    /// </summary>
    internal enum BlankLineVerdict
    {
        /// <summary>The rule abstained; the dispatcher should try the next rule.</summary>
        None,

        /// <summary>The rule wants a blank line above the current line.</summary>
        AddBlankAbove,

        /// <summary>The rule wants no blank line above the current line (overrides AddBlankAbove).</summary>
        SuppressBlank
    }

    /// <summary>
    /// Pre-computed predicates shared by all per-rule blank-line
    /// decisions. Computed once per non-blank line in the central
    /// orchestrator, then passed to each rule method.
    /// </summary>
    internal struct BlankLinePredicates
    {
        /// <summary>Current line, trimmed.</summary>
        public string Trimmed;

        /// <summary>Previous non-blank line, trimmed.</summary>
        public string PrevTrimmed;

        /// <summary>Whether the current line's first character is in a code region.</summary>
        public bool LineIsCode;

        /// <summary>Whether the previous line's first character is in a code region.</summary>
        public bool PrevIsCode;

        /// <summary>Whether the current line is a block-start keyword line.</summary>
        public bool IsBlockStart;

        /// <summary>Whether the previous line is a block-end line (<c>}</c> or <c>};</c>).</summary>
        public bool PrevIsBlockEnd;

        /// <summary>Whether the current line is a block-end line.</summary>
        public bool CurrentIsBlockEnd;

        /// <summary>Whether the previous line is a block-start brace (<c>{</c> or ends with <c>{</c>).</summary>
        public bool PrevIsBlockStartBrace;

        /// <summary>Whether the previous line is a comment line (<c>//</c>, <c>/*</c>, <c>*</c>).</summary>
        public bool PrevIsComment;

        /// <summary>Whether the previous line is a documentation comment (<c>///</c>).</summary>
        public bool PrevIsDocComment;

        /// <summary>Whether the previous line is a non-doc comment line.</summary>
        public bool PrevIsRegularComment;

        /// <summary>Whether the current line starts with <c>///</c>.</summary>
        public bool CurrentIsDocComment;

        /// <summary>Whether the current line starts with <c>catch</c> or <c>finally</c> in a code region.</summary>
        public bool CurrentIsCatchOrFinally;

        /// <summary>Whether the current line starts with <c>else</c> in a code region.</summary>
        public bool CurrentIsElse;

        /// <summary>Whether the current line ends with a continuation operator.</summary>
        public bool CurrentContinues;

        /// <summary>Whether the previous line ended with a continuation operator (so the current line continues it).</summary>
        public bool PrevLineContinuedIntoCurrent;

        /// <summary>Whether the current line is the start of a multi-line statement.</summary>
        public bool CurrentIsMultiLineStart;

        /// <summary>Whether the previous line is the end of a multi-line statement.</summary>
        public bool PrevIsMultiLineEnd;

        /// <summary>Whether the current line is a plain single-line statement.</summary>
        public bool CurrentIsPlainStmt;

        /// <summary>Whether the previous line is a plain single-line statement.</summary>
        public bool PrevIsPlainStmt;

        /// <summary>Whether the input had a blank line immediately above this entry.</summary>
        public bool EntryHadBlankAbove;
    }

    /// <summary>
    /// Records a non-blank line together with its original index in
    /// the input list and whether a blank line preceded it. Used by
    /// <see cref="BlankLineProcessor"/> to correctly index into the
    /// per-line flag arrays after blank lines have been collapsed.
    /// </summary>
    internal struct NonBlankEntry
    {
        /// <summary>The original index of this line in the input list.</summary>
        public int OriginalIndex;

        /// <summary>Whether a blank line immediately preceded this line in the input.</summary>
        public bool HadBlankAbove;

        /// <summary>The line text.</summary>
        public string Line;

        /// <summary>
        /// Creates a new non-blank entry record.
        /// </summary>
        /// <param name="originalIndex">The line's original index in the input list.</param>
        /// <param name="hadBlankAbove">Whether a blank line preceded this line in the input.</param>
        /// <param name="line">The line text.</param>
        public NonBlankEntry(int originalIndex, bool hadBlankAbove,
            string line)
        {
            OriginalIndex = originalIndex;
            HadBlankAbove = hadBlankAbove;
            Line = line;
        }
    }

    /// <summary>
    /// Shared predicate helpers used by the per-rule blank-line
    /// partial classes under <c>BlankLineRules/</c>. All methods are
    /// stateless static utilities.
    /// </summary>
    internal static class BlankLineHelpers
    {
        /// <summary>
        /// Determines whether a trimmed line is a comment line
        /// (single-line comment, XML doc comment, or block-comment
        /// continuation/end).
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a comment line.</returns>
        public static bool IsCommentLine(string trimmed)
        {
            return trimmed.StartsWith("//") || trimmed.StartsWith("/*") ||
                trimmed.StartsWith("*");
        }

        /// <summary>
        /// Determines whether a trimmed line is a plain single-line
        /// statement: a code line that ends a statement (<c>;</c> or
        /// <c>}</c>) and is neither a block-end, a block-start, nor a
        /// comment line. Used by the author-blank-preservation rule.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <param name="origIdx">The line's original index in the input list.</param>
        /// <param name="isCodeLine">Per-line code-region flag array.</param>
        /// <param name="lineEndsStatement">Per-line statement-end flag array.</param>
        /// <returns>True if the line is a plain single-line statement.</returns>
        public static bool IsPlainSingleLineStatement(string trimmed,
            int origIdx, bool[] isCodeLine, bool[] lineEndsStatement)
        {
            if (origIdx < 0 || origIdx >= isCodeLine.Length ||
                !isCodeLine[origIdx])
            {
                return false;
            }

            if (origIdx >= lineEndsStatement.Length ||
                !lineEndsStatement[origIdx])
            {
                return false;
            }

            if (LineClassifier.Instance.IsBlockEndLine(trimmed))
            {
                return false;
            }

            if (LineClassifier.Instance.IsBlockStartLine(trimmed))
            {
                return false;
            }

            if (IsCommentLine(trimmed))
            {
                return false;
            }

            return true;
        }
    }
}
