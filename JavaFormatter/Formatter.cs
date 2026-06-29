using System.Collections.Generic;

namespace JavaFormatter
{
    /// <summary>
    /// Applies all Java formatting rules to source code by orchestrating the
    /// focused processor modules in a fixed pipeline.
    /// </summary>
    internal static class Formatter
    {
        /// <summary>Each indent level uses 4 spaces.</summary>
        internal const int IndentSize = 4;

        /// <summary>Maximum length of a single line.</summary>
        internal const int MaxLineLength = 80;

        /// <summary>
        /// Applies all formatting rules to the source string and returns the result.
        /// The pipeline is idempotent: running it twice yields the same output as
        /// running it once.
        /// </summary>
        /// <param name="source">The raw source code string.</param>
        /// <param name="targetRoot">The target root directory path (used by ImportSorter).</param>
        /// <returns>The formatted source code string.</returns>
        public static string Format(string source, string targetRoot)
        {
            var tokens = Tokenizer.Tokenize(source);
            tokens = BraceEnforcer.ApplyMandatoryBraces(tokens);
            string text = Tokenizer.Reconstruct(tokens);
            text = EnumFormatter.FormatEnums(text);
            text = ImportSorter.Sort(text, targetRoot);
            text = TextUtils.NormalizeTabs(text);
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = TextUtils.EnsureOpenBraceOnSameLine(text);
            var lines = TextUtils.SplitLines(text);
            lines = IndentationProcessor.Reindent(lines, text);
            // Compute continuation flags from the post-Reindent (pre-split)
            // line structure so that LineLengthProcessor can detect
            // continuation lines and avoid cascading indents when splitting
            // them (a continuation line split at parent+4 must keep its
            // segments at parent+4, not parent+8).
            string textForLimit = string.Join("\n", lines);
            var tokensForLimit = Tokenizer.Tokenize(textForLimit);

            bool[] isCodeForLimit = Tokenizer.BuildCodeMask(textForLimit,
                tokensForLimit);

            int[] lineStartsForLimit = TextUtils.ComputeLineStarts(lines);
            var preSplitContinues = new bool[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                preSplitContinues[i] = LineClassifier.IsContinuationIndicator(
                    lines[i], lineStartsForLimit[i], textForLimit,
                    isCodeForLimit);
            }

            // Split long lines BEFORE applying blank-line rules so that the
            // preSplitContinues flags (computed above) stay aligned with the
            // line list. Running BlankLineProcessor first would insert blank
            // lines and shift indices, causing LineLengthProcessor to read
            // the wrong continuation flag for each line.
            lines = LineLengthProcessor.ApplyLineLengthLimit(lines,
                preSplitContinues);

            lines = BlankLineProcessor.ApplyBlankLineRules(lines);
            lines = BlankLineProcessor.CollapseBlankLines(lines);
            lines = BlankLineProcessor.TrimTrailingWhitespace(lines);
            string result = string.Join("\n", lines);
            result = TextUtils.EnsureSingleTrailingNewline(result);
            return result;
        }
    }
}
