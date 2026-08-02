using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace CSharpFormatter
{
    /// <summary>
    /// Core orchestration that applies all C# formatting rules in
    /// sequence. Each transformation pass delegates to a specialised
    /// instance class; the pipeline is designed to be idempotent.
    /// </summary>
    internal sealed class Formatter
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly Formatter Instance = new Formatter();

        private Formatter()
        {
        }

        /// <summary>
        /// Applies all formatting rules to the source string and
        /// returns the result.
        /// </summary>
        /// <param name="source">The original source code string.</param>
        /// <param name="rootNamespace">The root namespace of the current module.</param>
        /// <returns>The formatted source code string.</returns>
        public string Format(
            string source,
            string rootNamespace
        )
        {
            var tokens = CSharpTokenizer.Instance.Tokenize(source);
            tokens = BraceEnforcer.Instance.ApplyMandatoryBraces(tokens);
            string text = CSharpTokenizer.Instance.Reconstruct(tokens);
            text = EnumFormatter.Instance.FormatEnums(text);
            text = PropertyFormatter.Instance.FormatPropertyAccessors(text);
            text = UsingSorter.Instance.Sort(text, rootNamespace);
            text = CSharpTextUtils.Instance.ReplaceTabsInCode(text);
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = CSharpTextUtils.Instance.MoveOpenBraceToOwnLine(text);
            var lines = TextUtils.SplitLines(text);
            var tokenized = CSharpTokenizer.Instance.Tokenize(text);

            bool[] isCode = CSharpTokenizer.Instance.BuildCodeMask(text,
                tokenized);

            bool[] isCodeLine = LineClassifier.Instance.ComputeIsCodeLine(lines,
                isCode);

            lines = IndentationProcessor.Instance.Reindent(lines, text,
                tokenized, isCode, isCodeLine);

            text = string.Join("\n", lines);
            tokenized = CSharpTokenizer.Instance.Tokenize(text);
            isCode = CSharpTokenizer.Instance.BuildCodeMask(text, tokenized);

            int[] lineStarts =
                CSharpTokenizer.Instance.ComputeLineStarts(lines);

            var preSplitContinues = new bool[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                preSplitContinues[i] =
                    LineClassifier.Instance.IsContinuationIndicator(
                        lines[i], lineStarts[i], text, isCode);
            }

            lines = LineLengthProcessor.Instance.ApplyLineLengthLimit(lines,
                preSplitContinues);

            text = string.Join("\n", lines);
            tokenized = CSharpTokenizer.Instance.Tokenize(text);
            isCode = CSharpTokenizer.Instance.BuildCodeMask(text, tokenized);

            isCodeLine = LineClassifier.Instance.ComputeIsCodeLine(lines,
                isCode);

            // Re-run indentation after line-length splitting so that
            // continuation fragments emitted by the splitter are
            // normalised to the canonical paren-based indentation.
            // Without this, a split can emit fragments whose indent
            // differs from what Reindent computes on the next pass
            // (e.g. a fragment after a paren-close), breaking
            // idempotency.
            lines = IndentationProcessor.Instance.Reindent(lines, text,
                tokenized, isCode, isCodeLine);

            text = string.Join("\n", lines);
            tokenized = CSharpTokenizer.Instance.Tokenize(text);
            isCode = CSharpTokenizer.Instance.BuildCodeMask(text, tokenized);

            isCodeLine = LineClassifier.Instance.ComputeIsCodeLine(lines,
                isCode);

            lineStarts = CSharpTokenizer.Instance.ComputeLineStarts(lines);
            var lineContinuesNext = new bool[lines.Count];
            var lineEndsStatement = new bool[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                lineContinuesNext[i] =
                    LineClassifier.Instance.IsContinuationIndicator(
                        lines[i], lineStarts[i], text, isCode);

                lineEndsStatement[i] = LineClassifier.Instance.EndsStatement(
                    lines[i], lineStarts[i], text, isCode);
            }

            lines = BlankLineProcessor.Instance.ApplyBlankLineRules(lines,
                isCodeLine, lineContinuesNext, lineEndsStatement);

            lines = BlankLineProcessor.Instance.CollapseBlankLines(lines);
            lines = BlankLineProcessor.Instance.TrimTrailingWhitespace(lines);
            string result = string.Join("\n", lines);
            result = TextUtils.EnsureSingleTrailingNewline(result);
            return result;
        }
    }
}
