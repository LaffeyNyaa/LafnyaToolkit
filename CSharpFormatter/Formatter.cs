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
            // Tokenise once for all downstream line-based processing.
            var tokenized = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokenized);
            bool[] isCodeLine = TextUtils.ComputeIsCodeLine(lines, isCode);
            lines = IndentationProcessor.Reindent(lines, text, tokenized,
                isCode, isCodeLine);
            lines = BlankLineProcessor.ApplyBlankLineRules(lines,
                isCodeLine);
            lines = BlankLineProcessor.CollapseBlankLines(lines);
            lines = BlankLineProcessor.TrimTrailingWhitespace(lines);
            lines = LineLengthProcessor.ApplyLineLengthLimit(lines);
            string result = string.Join("\n", lines);
            result = TextUtils.EnsureSingleTrailingNewline(result);
            return result;
        }
    }
}
