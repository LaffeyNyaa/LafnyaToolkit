using System.Collections.Generic;

using LafnyaToolkit.Core.Text;

namespace PythonFormatter
{
    /// <summary>
    /// Applies all Python formatting rules to source code by
    /// orchestrating the focused processor modules in a fixed
    /// pipeline. The pipeline is idempotent: running it twice yields
    /// the same output as running it once. Stateless; the shared
    /// instance is exposed via <see cref="Instance"/>.
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
        /// Applies all formatting rules to the source string and
        /// returns the result. The pipeline normalizes tabs in code
        /// regions, re-indents lines to multiples of 4 spaces, splits
        /// long lines, applies import sorting, applies the blank-line
        /// rules, collapses excess blank lines, trims trailing
        /// whitespace, and ensures a single trailing newline.
        /// </summary>
        /// <param name="source">The raw source code string.</param>
        /// <returns>The formatted source code string.</returns>
        public string Format(string source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            string text = source;

            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = PythonTextUtils.Instance.NormalizeTabs(text);

            string sorted = ImportSorter.Instance.Sort(text);

            if (!ReferenceEquals(sorted, text) && sorted.Length > 0)
            {
                text = sorted;
            }

            var lines = PythonTextUtils.Instance.SplitLines(text);
            lines = IndentationProcessor.Instance.Reindent(lines, text);
            text = PythonTextUtils.Instance.JoinLines(lines);

            lines = PythonTextUtils.Instance.SplitLines(text);
            lines = LineLengthProcessor.Instance.ApplyLineLengthLimit(lines);
            text = PythonTextUtils.Instance.JoinLines(lines);

            lines = PythonTextUtils.Instance.SplitLines(text);
            lines = BlankLineProcessor.Instance.ApplyBlankLineRules(lines);
            lines = BlankLineProcessor.Instance.CollapseBlankLines(lines);
            text = PythonTextUtils.Instance.JoinLines(lines);
            var lineEndsInsideToken = PythonTextUtils.Instance
            .BuildLineEndsInsideToken(text);

            text = PythonTextUtils.Instance.TrimTrailingWhitespace(text,
                lineEndsInsideToken);

            return LafnyaToolkit.Core.Text.TextUtils
            .EnsureSingleTrailingNewline(text);
        }
    }
}
