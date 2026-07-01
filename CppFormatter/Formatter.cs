namespace CppFormatter
{
    /// <summary>
    /// Core orchestration that applies all C++ formatting rules in
    /// sequence. Each transformation pass delegates to a specialised
    /// module; the pipeline is designed to be idempotent.
    /// </summary>
    internal static class Formatter
    {
        /// <summary>
        /// Applies all formatting rules to the source string and returns the result.
        /// </summary>
        /// <param name="source">The original source string.</param>
        /// <returns>The formatted source string.</returns>
        public static string Format(string source)
        {
            var tokens = Tokenizer.Tokenize(source);
            tokens = BraceEnforcer.ApplyMandatoryBraces(tokens);
            string text = Tokenizer.Reconstruct(tokens);
            text = EnumFormatter.FormatEnums(text);
            text = IncludeSorter.Sort(text);
            text = text.Replace("\t", "    ");
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = BraceMerger.MoveOpenBraceToPreviousLine(text);
            text = DoWhileMerger.MergeDoWhileCloseBrace(text);
            text = EndifCommentProcessor.AppendEndifComments(text);
            tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            var lines = TextUtils.SplitLines(text);
            string currentText = text;

            lines = IndentationProcessor.Reindent(lines, currentText, tokens,
                isCode);

            lines = NamespaceBodyTrimmer.TrimNamespaceBodyBlankLines(lines,
                currentText, tokens, isCode);

            // Compute continuation flags from the post-Reindent (pre-split)
            // line structure so that LineLengthProcessor can detect
            // continuation lines and avoid cascading indents when splitting
            // them (a continuation line split at parent+4 must keep its
            // segments at parent+4, not parent+8).
            currentText = string.Join("\n", lines);
            var tokensForLimit = Tokenizer.Tokenize(currentText);

            bool[] isCodeForLimit = Tokenizer.BuildCodeMask(currentText,
                tokensForLimit);

            int[] lineStartsForLimit = Tokenizer.ComputeLineStarts(lines);
            var preSplitContinues = new bool[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                preSplitContinues[i] = IndentationProcessor

                .IsContinuationIndicator(lines[i],
                    lineStartsForLimit[i], currentText, isCodeForLimit);
            }

            // Split long lines BEFORE applying blank-line rules so that the
            // preSplitContinues flags (computed above) stay aligned with the
            // line list. Running BlankLineProcessor first would insert blank
            // lines and shift indices, causing LineLengthProcessor to read
            // the wrong continuation flag for each line.
            lines = LineLengthProcessor.ApplyLineLengthLimit(lines,
                currentText, preSplitContinues);

            // Only join lines when they have been modified by a processor
            currentText = string.Join("\n", lines);

            lines = BlankLineProcessor.ApplyBlankLineRules(lines,
                currentText);

            currentText = string.Join("\n", lines);

            lines = BlankLineProcessor.CollapseBlankLines(lines,
                currentText);

            currentText = string.Join("\n", lines);

            lines = BlankLineProcessor.TrimTrailingWhitespace(lines,
                currentText);

            string result = string.Join("\n", lines);
            result = TextUtils.EnsureSingleTrailingNewline(result);
            return result;
        }
    }
}
