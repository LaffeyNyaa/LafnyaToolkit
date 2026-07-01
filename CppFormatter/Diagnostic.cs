using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CppFormatter
{
    /// <summary>
    /// Diagnostic program that runs the formatter pipeline step by step,
    /// saving intermediate results to understand indentation differences
    /// between pass1 and pass2.
    /// </summary>
    public class Diagnostic
    {
        public static void Main(string[] args)
        {
            string samplesDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "samples");
            string srcFile = Path.Combine(samplesDir, "repository.cpp.bak");
            string source = File.ReadAllText(srcFile, Encoding.UTF8);

            var outputs = new List<string>();
            // ========== PASS 1 ==========
            outputs.Add("=" + new string('=', 79));
            outputs.Add("  PASS 1: First Format() pass on original source");
            outputs.Add("=" + new string('=', 79));
            outputs.Add("");
            // Run the full Format() for reference
            string pass1Final = Formatter.Format(source);
            // Now replicate the pipeline step by step to capture intermediates
            RunDiagnosticPipeline(source, outputs, "(pass1)");
            // ========== PASS 2 ==========
            outputs.Add("");
            outputs.Add("=" + new string('=', 79));
            outputs.Add("  PASS 2: Second Format() pass on pass1 output");
            outputs.Add("=" + new string('=', 79));
            outputs.Add("");
            // Run the full Format() for reference
            string pass2Final = Formatter.Format(pass1Final);
            // Replicate pipeline on pass1 output
            RunDiagnosticPipeline(pass1Final, outputs, "(pass2)");
            // ========== SAVE ALL OUTPUT ==========
            string outFile = Path.Combine(samplesDir, "debug_diagnostic.txt");

            File.WriteAllText(outFile, string.Join("\n", outputs),
                new UTF8Encoding(false));

            // Also extract just the key section around the "FROM qq_user" lines
            ExtractKeySection(pass1Final, pass2Final, samplesDir);

            Console.WriteLine("Diagnostic output written to: " + outFile);
            Console.WriteLine("Done.");
        }

        /// <summary>
        /// Runs the formatting pipeline step by step, saving intermediate
        /// results at each stage.
        /// </summary>
        static void RunDiagnosticPipeline(string source, List<string> outputs,
            string tag)
        {
            // ---- STAGE 0: Pre-processing (same as Formatter.Format) ----
            var tokens = Tokenizer.Tokenize(source);
            tokens = BraceEnforcer.ApplyMandatoryBraces(tokens);
            string text = Tokenizer.Reconstruct(tokens);
            text = EnumFormatter.FormatEnums(text);
            text = IncludeSorter.Sort(text);
            text = text.Replace("\t", "    ");
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = TextUtils.MoveOpenBraceToPreviousLine(text);
            text = TextUtils.MergeDoWhileCloseBrace(text);
            text = EndifCommentProcessor.AppendEndifComments(text);

            SaveIntermediate(outputs, text, tag, "01_after_preprocessing");
            // ---- STAGE 1: Reindent ----
            tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            var lines = TextUtils.SplitLines(text);
            // Snapshot before Reindent
            SaveLines(outputs, lines, tag, "02_before_reindent");

            lines = IndentationProcessor.Reindent(lines, text, tokens, isCode);

            SaveLines(outputs, lines, tag, "03_after_reindent");
            // ---- STAGE 2: Trim namespace blank lines ----
            lines = IndentationProcessor.TrimNamespaceBodyBlankLines(lines,
                text, tokens, isCode);

            SaveLines(outputs, lines, tag, "04_after_trim_namespace");
            // ---- STAGE 3: Continuation flags ----
            string textForLimit = string.Join("\n", lines);
            var tokensForLimit = Tokenizer.Tokenize(textForLimit);

            bool[] isCodeForLimit = Tokenizer.BuildCodeMask(textForLimit,
                tokensForLimit);

            int[] lineStartsForLimit = Tokenizer.ComputeLineStarts(lines);
            var preSplitContinues = new bool[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                preSplitContinues[i] = IndentationProcessor

                .IsContinuationIndicator(lines[i],
                    lineStartsForLimit[i], textForLimit, isCodeForLimit);
            }

            SaveLines(outputs, lines, tag, "05_before_linelength");
            // ---- STAGE 4: LineLengthProcessor ----
            lines = LineLengthProcessor.ApplyLineLengthLimit(lines,
                textForLimit, preSplitContinues);

            SaveLines(outputs, lines, tag, "06_after_linelength");
            // ---- STAGE 5: BlankLineProcessor.ApplyBlankLineRules ----
            string textForBlank = string.Join("\n", lines);
            lines = BlankLineProcessor.ApplyBlankLineRules(lines, textForBlank);

            SaveLines(outputs, lines, tag,
                "07_after_ApplyBlankLineRules");
            // ---- STAGE 6: Collapse blank lines ----
            string textForCollapse = string.Join("\n", lines);

            lines = BlankLineProcessor.CollapseBlankLines(lines,
                textForCollapse);

            SaveLines(outputs, lines, tag, "08_after_CollapseBlankLines");
            // ---- STAGE 7: Trim trailing whitespace ----
            string textForTrim = string.Join("\n", lines);

            lines = BlankLineProcessor.TrimTrailingWhitespace(lines,
                textForTrim);

            SaveLines(outputs, lines, tag,
                "09_after_TrimTrailingWhitespace");
            // ---- Final ----
            string result = string.Join("\n", lines);
            result = TextUtils.EnsureSingleTrailingNewline(result);

            SaveIntermediate(outputs, result, tag, "10_final");
        }

        static void SaveIntermediate(List<string> outputs, string text,
            string tag, string label)
        {
            outputs.Add("");
            outputs.Add($"--- {tag} {label} ---");
            outputs.Add("");
            string[] lines = text.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                outputs.Add($"  L{i + 1,3}|{lines[i]}");
            }
        }

        static void SaveLines(List<string> outputs, List<string> lines,
            string tag, string label)
        {
            outputs.Add("");
            outputs.Add($"--- {tag} {label} ---");
            outputs.Add("");

            for (int i = 0; i < lines.Count; i++)
            {
                string display = lines[i]
                .Replace(" ", "\u00B7")
                .Replace("\t", "\\t");
                outputs.Add($"  L{i + 1,3}|{display}  (len={lines[i].Length})");
            }
        }

        /// <summary>
        /// Extracts the key section around "FROM qq_user" from both pass1 and pass2.
        /// </summary>
        static void ExtractKeySection(string pass1Result, string pass2Result,
            string samplesDir)
        {
            string outFile = Path.Combine(samplesDir, "debug_key_section.txt");
            var sb = new StringBuilder();

            sb.AppendLine("=== KEY SECTION COMPARISON ===");
            sb.AppendLine();
            sb.AppendLine("--- PASS 1 ---");
            sb.AppendLine(ExtractAround(pass1Result, "FROM qq_user", 10));
            sb.AppendLine();
            sb.AppendLine("--- PASS 2 ---");
            sb.AppendLine(ExtractAround(pass2Result, "FROM qq_user", 10));
            sb.AppendLine();
            sb.AppendLine("=== DIFF (character by character around FROM line) ===");
            sb.AppendLine();
            // Also extract the specific FROM lines
            string pass1FromLine = ExtractLineStartingWith(pass1Result,
                "FROM qq_user");

            string pass2FromLine = ExtractLineStartingWith(pass2Result,
                "FROM qq_user");
            sb.AppendLine("pass1 FROM line: |" + pass1FromLine + "|");
            sb.AppendLine("pass2 FROM line: |" + pass2FromLine + "|");
            sb.AppendLine();

            if (pass1FromLine != null && pass2FromLine != null)
            {
                int indent1 = pass1FromLine.Length -
                    pass1FromLine.TrimStart().Length;

                int indent2 = pass2FromLine.Length -
                    pass2FromLine.TrimStart().Length;

                sb.AppendLine($"pass1 indent: {indent1} spaces (= {indent1 / 4} levels)");
                sb.AppendLine($"pass2 indent: {indent2} spaces (= {indent2 / 4} levels)");
            }

            File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
        }

        static string ExtractAround(string text, string search,
            int contextLines)
        {
            string[] lines = text.Split('\n');
            var sb = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(search))
                {
                    int start = Math.Max(0, i - contextLines);
                    int end = Math.Min(lines.Length - 1, i + contextLines);

                    if (start > 0)
                    {
                        int prevSectionEnd = Math.Max(0,
                            start - contextLines - 1);

                        sb.AppendLine($"  ... (snip {start - prevSectionEnd} lines) ...");
                    }

                    for (int j = start; j <= end; j++)
                    {
                        string marker = (j == i) ? " >>>" : "";
                        string display = lines[j]
                        .Replace("\t", "\\t");
                        sb.AppendLine($"  L{j + 1,3}|{display}{marker}");
                    }

                    break;
                }
            }

            return sb.ToString();
        }

        static string ExtractLineStartingWith(string text, string search)
        {
            string[] lines = text.Split('\n');

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith(search))
                {
                    return line;
                }
            }

            return null;
        }
    }
}
