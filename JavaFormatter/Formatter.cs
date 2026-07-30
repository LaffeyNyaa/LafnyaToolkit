namespace JavaFormatter
{
    /// <summary>
    /// Applies all Java formatting rules to source code by orchestrating the
    /// focused processor modules in a fixed pipeline. The pipeline is
    /// idempotent: running it twice yields the same output as running it
    /// once. Stateless; the shared instance is exposed via
    /// <see cref="Instance"/>.
    /// </summary>
    internal sealed class Formatter
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly Formatter Instance = new Formatter();

        private Formatter()
        {
        }

        /// <summary>Each indent level uses 4 spaces.</summary>
        internal const int IndentSize = 4;

        /// <summary>Maximum length of a single line.</summary>
        internal const int MaxLineLength = 80;

        /// <summary>
        /// Applies all formatting rules to the source string and returns the result.
        /// The pipeline tokenizes and applies mandatory braces, expands single-line
        /// enums, sorts the import block, normalizes tabs (preserving tabs inside
        /// string and comment tokens), moves standalone open braces to the end of
        /// the previous line, recomputes per-line indentation, splits long lines,
        /// inserts blank lines according to the per-rule blank-line policy, and
        /// finally collapses excess blank lines and trims trailing whitespace.
        /// Long lines are split BEFORE applying blank-line rules so the
        /// continuation flags stay aligned with the line list. Splitting after
        /// blank-line insertion would shift the line indices that
        /// <see cref="LineLengthProcessor"/> consumes, causing it to read the
        /// wrong continuation flag for each line.
        /// </summary>
        /// <param name="source">The raw source code string.</param>
        /// <param name="targetRoot">The target root directory path (used by ImportSorter).</param>
        /// <returns>The formatted source code string.</returns>
        public string Format(string source, string targetRoot)
        {
            var tokens = JavaTokenizer.Instance.Tokenize(source);
            tokens = BraceEnforcer.Instance.ApplyMandatoryBraces(tokens);
            string text = JavaTokenizer.Instance.Reconstruct(tokens);
            text = EnumFormatter.Instance.FormatEnums(text);
            text = ImportSorter.Instance.Sort(text, targetRoot);
            text = JavaTextUtils.Instance.NormalizeTabs(text);
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = JavaTextUtils.Instance.EnsureOpenBraceOnSameLine(text);
            var lines = JavaTextUtils.Instance.SplitLines(text);
            lines = IndentationProcessor.Instance.Reindent(lines, text);
            string textForLimit = string.Join("\n", lines);
            var tokensForLimit = JavaTokenizer.Instance.Tokenize(textForLimit);
            bool[] isCodeForLimit = JavaTokenizer.Instance.BuildCodeMask(
                textForLimit, tokensForLimit);
            int[] lineStartsForLimit = JavaTextUtils.Instance.ComputeLineStarts(lines);
            var preSplitContinues = new bool[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                preSplitContinues[i] = LineClassifier.Instance.IsContinuationIndicator(
                    lines[i], lineStartsForLimit[i], textForLimit,
                    isCodeForLimit);
            }

            lines = LineLengthProcessor.Instance.ApplyLineLengthLimit(lines,
                preSplitContinues);

            lines = BlankLineProcessor.Instance.ApplyBlankLineRules(lines);
            lines = BlankLineProcessor.Instance.CollapseBlankLines(lines);
            lines = BlankLineProcessor.Instance.TrimTrailingWhitespace(lines);
            string result = string.Join("\n", lines);
            result = JavaTextUtils.Instance.EnsureSingleTrailingNewline(result);
            return result;
        }
    }
}
