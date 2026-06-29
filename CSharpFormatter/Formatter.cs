using System.Collections.Generic;

namespace CSharpFormatter
{
    /// <summary>
    /// Core orchestration that applies all C# formatting rules in
    /// sequence. Each transformation pass delegates to a specialised
    /// module; the pipeline is designed to be idempotent.
    /// </summary>
    internal static class Formatter
    {
        /// <summary>
        /// Applies all formatting rules to the source string and returns
        /// the result.
        /// </summary>
        /// <param name="source">The original source code string.</param>
        /// <param name="rootNamespace">The root namespace of the current
        /// module.</param>
        /// <returns>The formatted source code string.</returns>
        public static string Format(string source, string rootNamespace)
        {
            var tokens = Tokenizer.Tokenize(source);
            tokens = BraceEnforcer.ApplyMandatoryBraces(tokens);
            string text = Tokenizer.Reconstruct(tokens);
            text = EnumFormatter.FormatEnums(text);
            text = PropertyFormatter.FormatPropertyAccessors(text);
            text = UsingSorter.Sort(text, rootNamespace);
            text = TextUtils.ReplaceTabsInCode(text);
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = TextUtils.MoveOpenBraceToOwnLine(text);
            var lines = TextUtils.SplitLines(text);
            // Tokenise once for Reindent (which needs the pre-Reindent text
            // and code mask to compute brace depths and continuation flags).
            var tokenized = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokenized);
            bool[] isCodeLine = TextUtils.ComputeIsCodeLine(lines, isCode);
            lines = IndentationProcessor.Reindent(lines, text, tokenized,
                isCode, isCodeLine);
            // Compute continuation flags from the post-Reindent (pre-split)
            // line structure so that LineLengthProcessor can detect
            // continuation lines and avoid cascading indents when splitting
            // them (Bug E: a continuation line split at parent+4 must keep
            // its segments at parent+4, not parent+8).
            text = string.Join("\n", lines);
            tokenized = Tokenizer.Tokenize(text);
            isCode = Tokenizer.BuildCodeMask(text, tokenized);
            int[] lineStarts = TextUtils.ComputeLineStarts(lines);
            var preSplitContinues = new bool[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                preSplitContinues[i] = TextUtils.IsContinuationIndicator(
                    lines[i], lineStarts[i], text, isCode);
            }
            // Split long lines BEFORE applying blank-line rules so that
            // multi-line statements produced by line-length splitting are
            // visible to the blank-line rules on the first pass. This is
            // required for idempotency: on a second pass the lines are
            // already split, and the blank-line rules would otherwise insert
            // a new blank line above the first segment.
            lines = LineLengthProcessor.ApplyLineLengthLimit(lines,
                preSplitContinues);
            // Recompute text, tokens, code mask, and per-line flags from the
            // post-split lines so that continuation/statement-end detection
            // reflects the actual (possibly split) line structure.
            text = string.Join("\n", lines);
            tokenized = Tokenizer.Tokenize(text);
            isCode = Tokenizer.BuildCodeMask(text, tokenized);
            isCodeLine = TextUtils.ComputeIsCodeLine(lines, isCode);
            lineStarts = TextUtils.ComputeLineStarts(lines);
            var lineContinuesNext = new bool[lines.Count];
            var lineEndsStatement = new bool[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                lineContinuesNext[i] = TextUtils.IsContinuationIndicator(
                    lines[i], lineStarts[i], text, isCode);
                lineEndsStatement[i] = TextUtils.EndsStatement(
                    lines[i], lineStarts[i], text, isCode);
            }
            lines = BlankLineProcessor.ApplyBlankLineRules(lines, isCodeLine,
                lineContinuesNext, lineEndsStatement);
            lines = BlankLineProcessor.CollapseBlankLines(lines);
            lines = BlankLineProcessor.TrimTrailingWhitespace(lines);
            string result = string.Join("\n", lines);
            result = TextUtils.EnsureSingleTrailingNewline(result);
            return result;
        }
    }
}
