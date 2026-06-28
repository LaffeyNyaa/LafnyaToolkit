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
            lines = BlankLineProcessor.ApplyBlankLineRules(lines);
            lines = BlankLineProcessor.CollapseBlankLines(lines);
            lines = BlankLineProcessor.TrimTrailingWhitespace(lines);
            lines = LineLengthProcessor.ApplyLineLengthLimit(lines);
            string result = string.Join("\n", lines);
            result = TextUtils.EnsureSingleTrailingNewline(result);
            return result;
        }
    }
}
