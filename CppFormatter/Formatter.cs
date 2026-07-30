using System.Collections.Generic;

using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace CppFormatter
{
    /// <summary>
    /// Core orchestration that applies all C++ formatting rules in
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
        /// returns the result. The pipeline is split into two
        /// tokenization passes: the first runs brace enforcement
        /// and the preprocessor-aware transformations; the second
        /// runs indentation/blank-line/line-length passes on the
        /// resulting structured text.
        /// </summary>
        /// <param name="source">The original source string.</param>
        /// <returns>The formatted source string.</returns>
        public string Format(string source)
        {
            var tokens = CppTokenizer.Instance.Tokenize(source);
            tokens = BraceEnforcer.Instance.ApplyMandatoryBraces(tokens);
            string text = CppTokenizer.Instance.Reconstruct(tokens);
            text = EnumFormatter.Instance.FormatEnums(text);
            text = IncludeSorter.Instance.Sort(text);
            text = text.Replace("\t", "    ");
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = BraceMerger.Instance.MoveOpenBraceToPreviousLine(text);
            text = DoWhileMerger.Instance.MergeDoWhileCloseBrace(text);
            text = EndifCommentProcessor.Instance.AppendEndifComments(text);

            tokens = CppTokenizer.Instance.Tokenize(text);
            bool[] isCode = CppTokenizer.Instance.BuildCodeMask(text, tokens);
            var lines = TextUtils.SplitLines(text);
            string currentText = text;

            lines = IndentationProcessor.Instance.Reindent(lines, currentText,
                tokens, isCode);

            lines = ConstructorInitializerProcessor.Instance.Format(lines);

            lines =
                NamespaceBodyTrimmer.Instance.TrimNamespaceBodyBlankLines(lines,
                currentText, tokens, isCode);

            currentText = string.Join("\n", lines);
            var tokensForLimit = CppTokenizer.Instance.Tokenize(currentText);

            bool[] isCodeForLimit =
                CppTokenizer.Instance.BuildCodeMask(currentText,
                tokensForLimit);

            int[] lineStartsForLimit =
                CppTokenizer.Instance.ComputeLineStarts(lines);

            var preSplitContinues = new bool[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                preSplitContinues[i] =
                    ContinuationScanner.Instance.IsContinuationIndicator(
                    lines[i], lineStartsForLimit[i], currentText,
                    isCodeForLimit);
            }

            lines = LineLengthProcessor.Instance.ApplyLineLengthLimit(
                lines, currentText, preSplitContinues, tokensForLimit,
                isCodeForLimit);

            currentText = string.Join("\n", lines);
            tokens = CppTokenizer.Instance.Tokenize(currentText);
            isCode = CppTokenizer.Instance.BuildCodeMask(currentText, tokens);

            lines = IndentationProcessor.Instance.Reindent(lines, currentText,
                tokens, isCode);

            currentText = string.Join("\n", lines);

            lines = BlankLineProcessor.Instance.ApplyBlankLineRules(lines,
                currentText);

            currentText = string.Join("\n", lines);

            lines = BlankLineProcessor.Instance.CollapseBlankLines(lines,
                currentText);

            currentText = string.Join("\n", lines);

            lines = BlankLineProcessor.Instance.TrimTrailingWhitespace(lines,
                currentText);

            string result = string.Join("\n", lines);
            result = TextUtils.EnsureSingleTrailingNewline(result);
            return result;
        }
    }
}
