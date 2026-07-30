using System.Collections.Generic;
using LafnyaToolkit.Core.Text;

namespace JavaFormatter
{
    /// <summary>
    /// Applies blank-line spacing rules and trims trailing whitespace.
    /// All keyword/brace detection is token-aware so that comment and
    /// string content is never mistaken for structural code. Split into
    /// this primary orchestrator plus per-rule files under
    /// <c>BlankLineRules/</c> and shared helpers in
    /// <c>BlankLineRules/BlankLineHelpers.cs</c>. Stateless; the
    /// shared instance is exposed via <see cref="Instance"/>.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly BlankLineProcessor Instance = new BlankLineProcessor();

        private BlankLineProcessor()
        {
        }

        /// <summary>
        /// Ensures exactly one blank line above blocks, multi-line statements, and
        /// declarations (with exceptions for the beginning/end of file). Annotation
        /// lines (starting with @) do not get blank lines inserted above them.
        /// Consecutive import lines also do not get blank lines inserted between them
        /// unless they were already separated by a blank line. The per-rule
        /// decisions are made by the partial method files under
        /// <c>BlankLineRules/</c>; this orchestrator computes the
        /// predicates and dispatches them in the order declared by
        /// <see cref="BlankLineMainRules.RunOrder"/>.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <returns>The lines with blank-line rules applied.</returns>
        public List<string> ApplyBlankLineRules(List<string> lines)
        {
            string text = string.Join("\n", lines);
            var tokens = JavaTokenizer.Instance.Tokenize(text);
            bool[] isCode = JavaTokenizer.Instance.BuildCodeMask(text, tokens);

            var lineStarts = new int[lines.Count];
            int pos = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                lineStarts[i] = pos;
                pos += lines[i].Length + 1;
            }

            var nonBlank = new List<JavaNonBlankEntry>(lines.Count);
            bool prevWasBlank = false;
            bool isFirst = true;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                if (line.Trim().Length == 0)
                {
                    prevWasBlank = true;
                    continue;
                }

                bool hadBlankAbove = !isFirst && prevWasBlank;
                nonBlank.Add(new JavaNonBlankEntry(hadBlankAbove, line, i));
                prevWasBlank = false;
                isFirst = false;
            }

            var result = new List<string>(nonBlank.Count);

            for (int i = 0; i < nonBlank.Count; i++)
            {
                var entry = nonBlank[i];
                string line = entry.Line;
                int lineStart = lineStarts[entry.OriginalIndex];
                string trimmed = line.Trim();

                bool lineStartsInCode = BlankLineHelpers.FirstNonWsInCode(line,
                    lineStart, isCode);

                bool isBlockStart = lineStartsInCode &&
                    LineClassifier.Instance.IsBlockStartLine(trimmed);

                bool currentIsImport = lineStartsInCode &&
                    LineClassifier.Instance.IsImportDirective(trimmed);

                bool currentIsDoWhileTail = lineStartsInCode &&
                    LineClassifier.Instance.IsDoWhileTail(trimmed);

                bool currentIsBlockCont = lineStartsInCode &&
                    LineClassifier.Instance.IsBlockContinuation(trimmed);

                bool currentIsAnnotation = lineStartsInCode &&
                    trimmed.StartsWith("@");

                bool currentStartsWithCloseBrace = lineStartsInCode &&
                    trimmed.StartsWith("}");

                bool wantBlankAbove = false;

                if (i > 0)
                {
                    var prevEntry = nonBlank[i - 1];
                    string prevLine = prevEntry.Line;
                    int prevLineStart = lineStarts[prevEntry.OriginalIndex];
                    string prevTrimmed = prevLine.Trim();

                    bool prevStartsInCode = BlankLineHelpers.FirstNonWsInCode(
                        prevLine, prevLineStart, isCode);

                    bool prevEndsInCode = BlankLineHelpers.LastNonWsInCode(
                        prevLine, prevLineStart, isCode);

                    bool prevIsOpenBraceOnly = prevStartsInCode &&
                        prevTrimmed == "{";

                    bool prevEndsWithOpenBrace = prevEndsInCode &&
                        TextUtils.EndsWithOpenBrace(prevTrimmed);

                    bool prevIsBlockEnd = prevStartsInCode &&
                        LineClassifier.Instance.IsBlockEndLine(prevTrimmed);

                    bool prevIsImport = prevStartsInCode &&
                        LineClassifier.Instance.IsImportDirective(prevTrimmed);

                    bool prevIsPackage = prevStartsInCode &&
                        prevTrimmed.StartsWith("package ");

                    if (ApplyBlockStartRule(trimmed, prevTrimmed, isBlockStart,
                        prevIsOpenBraceOnly, prevEndsWithOpenBrace) ==
                        BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                    }

                    if (!wantBlankAbove && ApplyBlockEndRule(trimmed,
                        prevTrimmed, prevIsBlockEnd, currentStartsWithCloseBrace) ==
                        BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                    }

                    if (ApplyConsecutiveImportsRule(currentIsImport, prevIsImport,
                        entry.HadBlankAbove) == BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                    }

                    if (ApplyImportAfterPackageRule(currentIsImport, prevIsPackage) ==
                        BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                    }

                    if (ApplyDocCommentRule(trimmed, prevTrimmed) ==
                        BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                    }

                    if (ApplyPlainStatementBlankRule(trimmed, prevTrimmed,
                        entry.HadBlankAbove, lineStartsInCode, prevStartsInCode) ==
                        BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                    }

                    if (ApplySuppressBlankAboveRule(currentIsAnnotation,
                        currentIsDoWhileTail, currentIsBlockCont) ==
                        BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = false;
                    }
                }

                if (wantBlankAbove)
                {
                    result.Add(string.Empty);
                }

                result.Add(line);
            }

            return result;
        }

        /// <summary>
        /// Collapses 3 or more consecutive blank lines into 1.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <returns>The lines with blank runs collapsed.</returns>
        public List<string> CollapseBlankLines(List<string> lines)
        {
            var result = new List<string>(lines.Count);
            int blankRun = 0;

            foreach (var line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    blankRun++;

                    if (blankRun <= 1)
                    {
                        result.Add(string.Empty);
                    }
                }
                else
                {
                    blankRun = 0;
                    result.Add(line);
                }
            }

            return result;
        }

        /// <summary>
        /// Removes trailing whitespace from each line.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <returns>The lines with trailing whitespace removed.</returns>
        public List<string> TrimTrailingWhitespace(List<string> lines)
        {
            var result = new List<string>(lines.Count);

            foreach (var line in lines)
            {
                result.Add(line.TrimEnd());
            }

            return result;
        }
    }
}
