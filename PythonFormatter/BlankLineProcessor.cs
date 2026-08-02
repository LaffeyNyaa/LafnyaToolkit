using System.Collections.Generic;

using LafnyaToolkit.Core.Text;

namespace PythonFormatter
{
    /// <summary>
    /// Applies blank-line spacing rules and trims trailing whitespace.
    /// The orchestrator is split into the primary dispatcher
    /// (<see cref="BlankLineProcessor"/>) and per-rule partial methods
    /// under <c>BlankLineRules/</c>. Decision dispatch order is
    /// declared in <see cref="BlankLineMainRules.RunOrder"/>.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly BlankLineProcessor Instance =
            new BlankLineProcessor();

        private BlankLineProcessor()
        {
        }

        /// <summary>
        /// Builds the list of <see cref="PythonNonBlankEntry"/> objects
        /// from the input lines, preserving the original index of each
        /// line and the leading whitespace (in spaces). A blank line is
        /// recorded in <see cref="PythonNonBlankEntry.HadBlankAbove"/>
        /// but is not itself emitted.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <returns>The list of non-blank entries.</returns>
        public List<PythonNonBlankEntry> BuildEntries(List<string> lines)
        {
            var entries = new List<PythonNonBlankEntry>(lines.Count);
            bool prevWasBlank = false;
            bool isFirst = true;
            int lastIndent = -1;
            int currentDefIndent = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                if (line.Trim().Length == 0)
                {
                    prevWasBlank = true;
                    continue;
                }

                int indent = BlankLineHelpers.ComputeIndentWidth(line);
                bool hadBlankAbove = !isFirst && prevWasBlank;

                if (LineClassifier.Instance.IsTopLevelDefClass(line.TrimStart())

                    ||
                    LineClassifier.Instance.IsDefLine(line.TrimStart()))
                {
                    currentDefIndent = indent;
                }
                else if (indent < currentDefIndent)
                {
                    currentDefIndent = 0;
                }

                entries.Add(new PythonNonBlankEntry(
                    hadBlankAbove,
                    line,
                    i,
                    indent,
                    lastIndent,
                    currentDefIndent
                ));

                lastIndent = indent;
                prevWasBlank = false;
                isFirst = false;
            }

            return entries;
        }

        /// <summary>
        /// Applies all blank-line rules to the lines and returns the
        /// new list of lines with the right number of blank lines
        /// between non-blank lines. Uses the dispatch order in
        /// <see cref="BlankLineMainRules.RunOrder"/>: each
        /// add-blank rule may decide a blank line above the current
        /// entry; the suppress rule can veto that decision.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <returns>The lines with blank-line rules applied.</returns>
        public List<string> ApplyBlankLineRules(List<string> lines)
        {
            var entries = BuildEntries(lines);

            if (entries.Count == 0)
            {
                return new List<string>();
            }

            var text = string.Join("\n", lines);
            var tokens = PythonTokenizer.Instance.Tokenize(text);

            bool[] isCode = PythonTokenizer.Instance.BuildCodeMask(text,
                tokens);

            var lineStarts = PythonTextUtils.Instance.ComputeLineStarts(lines);

            var lineEndDepths = PythonTextUtils.Instance.ComputeLineEndDepths(
                lines, isCode, lineStarts);

            var blanksAbove = new int[entries.Count];
            var result = new List<string>(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var prevEntry = i > 0
                ? (PythonNonBlankEntry?)entries[i - 1]
                : null;
                PythonNonBlankEntry? nextEntry = i + 1 < entries.Count
                ? (PythonNonBlankEntry?)entries[i + 1]
                : null;
                // Default: preserve the input's existing blank line (if any).
                // This makes the pipeline idempotent: blank lines placed by
                // earlier stages (e.g. the import sorter, or the previous
                // pass of the formatter) survive if no rule overrides.
                bool wantBlankAbove = entry.HadBlankAbove;
                int blankCount = entry.HadBlankAbove ? 1 : 0;

                if (prevEntry.HasValue)
                {
                    if (ApplyTopLevelDefClassBlankRule(entry,
                        prevEntry.Value) ==
                        BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                        blankCount = 2;
                    }
                    else if (ApplyMethodBlankRule(
                        entry,
                        prevEntry.Value,
                        entries,
                        i
                    )== BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                        blankCount = 1;
                    }
                    else if (ApplyFirstMethodInClassBlankRule(entry,
                        prevEntry.Value) == BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                        blankCount = 1;
                    }
                    else if (ApplyMultiLineStatementBlankRule(
                        entry,
                        prevEntry.Value,
                        lineEndDepths,
                        entries,
                        i

                    )==
                        BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                        blankCount = 1;
                    }
                    else if (ApplyBlockStartRule(entry, prevEntry.Value) ==
                        BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                        blankCount = 1;
                    }
                    else if (ApplyBlockEndRule(
                        entry,
                        prevEntry.Value,
                        lineStarts[prevEntry.Value.OriginalIndex],
                        isCode

                    )==
                        BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                        blankCount = 1;
                    }
                    else if (ApplyAfterBlockEndRule(entry, prevEntry.Value) ==
                        BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                        blankCount = 1;
                    }
                    else if (ApplyImportAfterCodeBlankRule(entry,
                        prevEntry.Value) == BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                        blankCount = 1;
                    }
                    else if (ApplyCodeAfterImportBlankRule(entry,
                        prevEntry.Value) == BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                        blankCount = 1;
                    }
                    else if (ApplyPreserveAuthorBlankRule(entry,
                        prevEntry.Value) == BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = true;
                        blankCount = 1;
                    }

                    if (wantBlankAbove &&
                        BlankLineHelpers.IsAtTopOfIndentLevel(entry))
                    {
                        wantBlankAbove = false;
                        blankCount = 0;
                    }

                    if (wantBlankAbove &&
                        ApplySuppressBlankAboveRule(entry, prevEntry) ==
                        BlankLineRuleResult.Decided)
                    {
                        wantBlankAbove = false;
                        blankCount = 0;
                    }
                }

                blanksAbove[i] = blankCount;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                for (int b = 0; b < blanksAbove[i]; b++)
                {
                    result.Add(string.Empty);
                }

                result.Add(entries[i].Line);
            }

            return result;
        }

        /// <summary>
        /// Collapses runs of 3 or more consecutive blank lines down to a
        /// single blank line. Runs of 1 or 2 blank lines are preserved
        /// as-is (PEP 8 strict: 2 blank lines between top-level
        /// definitions).
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

                    if (blankRun <= 2)
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
    }
}
