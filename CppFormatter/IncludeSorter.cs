using System;
using System.Collections.Generic;
using System.Text;

using LafnyaToolkit.Core.Text;

namespace CppFormatter
{
    /// <summary>
    /// Collects top-level #include directives and sorts them into
    /// four groups: System Libraries / Third-party Libraries / Other
    /// Project Modules / Current Module. Preprocessor conditional
    /// blocks (#if/#ifdef/#ifndef ... #endif) that contain at least
    /// one #include directive are treated as a single include unit.
    /// Stateless; the shared instance is exposed via
    /// <see cref="Instance"/>.
    /// </summary>
    internal sealed class IncludeSorter
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly IncludeSorter Instance = new IncludeSorter();

        private IncludeSorter()
        {
        }

        /// <summary>
        /// Scans the entire source for all top-level #include directives,
        /// collects each include together with any preceding non-include
        /// lines (preprocessor directives, blank lines, comments) into a
        /// unit, sorts units by category (System / Third-party / Other
        /// Project / Current Module), then rebuilds the source with the
        /// sorted include region.
        /// Preprocessor conditional blocks (#if/#ifdef/#ifndef ... #endif)
        /// that contain at least one #include directive are treated as a
        /// single include unit. The block is classified and sorted by
        /// the first #include encountered inside the block.
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <returns>The source string with sorted #include directives.</returns>
        public string Sort(string source)
        {
            string unified = source.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = unified.Split('\n');
            int firstInclude = -1;
            int lastInclude = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                if (CppTextUtils.Instance.IsIncludeDirective(lines[i].Trim()))
                {
                    if (firstInclude == -1)
                    {
                        firstInclude = i;
                    }

                    lastInclude = i;
                }
            }

            if (firstInclude == -1)
            {
                return source;
            }

            int openBlocks = 0;

            for (int i = firstInclude; i < lines.Length; i++)
            {
                if (i > lastInclude && openBlocks == 0)
                {
                    break;
                }

                string trimmed = lines[i].Trim();

                if (IsPreprocessorConditionalStart(trimmed))
                {
                    openBlocks++;
                }
                else if (trimmed.StartsWith("#endif"))
                {
                    if (openBlocks > 0)
                    {
                        openBlocks--;
                    }
                }

                if (i > lastInclude)
                {
                    lastInclude = i;
                }
            }

            BuildIncludeUnits(
                lines,
                firstInclude,
                lastInclude,
                out var units,
                out var preprocessorLines
            );

            var sortedBlock = BuildSortedIncludeBlock(units, preprocessorLines);

            var result = new StringBuilder();

            for (int i = 0; i < firstInclude; i++)
            {
                if (result.Length > 0)
                {
                    result.Append('\n');
                }

                result.Append(lines[i]);
            }

            if (firstInclude > 0 && sortedBlock.Count > 0)
            {
                int scanIdx = firstInclude - 1;

                while (scanIdx >= 0 && lines[scanIdx].Trim().Length == 0)
                {
                    scanIdx--;
                }

                if (scanIdx >= 0)
                {
                    string lastBeforeInclude = lines[scanIdx].Trim();

                    bool firstIsInclude =
                        CppTextUtils.Instance.IsIncludeDirective(sortedBlock[0]);

                    if (firstIsInclude &&
                        !CppTextUtils.Instance.IsIncludeDirective(lastBeforeInclude)

                        &&
                        lastBeforeInclude.Length > 0 && lastBeforeInclude[0] ==
                        '#')
                    {
                        result.Append('\n');
                    }

                    if (firstIsInclude &&
                        TextUtils.IsCommentLine(lastBeforeInclude))
                    {
                        result.Append('\n');
                    }
                }
            }

            foreach (var line in sortedBlock)
            {
                if (result.Length > 0)
                {
                    result.Append('\n');
                }

                result.Append(line);
            }

            int after = lastInclude + 1;

            while (after < lines.Length && lines[after].Trim().Length == 0)
            {
                after++;
            }

            for (int i = after; i < lines.Length; i++)
            {
                if (result.Length > 0)
                {
                    result.Append('\n');
                }

                result.Append(lines[i]);
            }

            return result.ToString();
        }

        /// <summary>
        /// Appends a group of include units to the block, with a blank
        /// line separator if the block is non-empty.
        /// </summary>
        private static void AppendUnitGroup(List<string> block, List<
            IncludeUnit> group)
        {
            if (group.Count == 0)
            {
                return;
            }

            if (block.Count > 0)
            {
                block.Add(string.Empty);
            }

            foreach (var unit in group)
            {
                if (unit.IsBlock)
                {
                    block.AddRange(unit.PrecedingLines);
                    block.AddRange(unit.BlockLines);
                }
                else
                {
                    block.AddRange(unit.PrecedingLines);
                    block.Add(unit.IncludeLine);
                }
            }
        }

        /// <summary>
        /// Builds a sorted include block from the collected units and
        /// preprocessor lines. Units are sorted by category, then by
        /// include path. Preprocessor directives (#ifndef, #define,
        /// #endif, etc.) are placed at the very top of the file,
        /// before any #include.
        /// </summary>
        private static List<string> BuildSortedIncludeBlock(List<IncludeUnit>
            units, List<string> preprocessorLines)
        {
            var systemGroup = new List<IncludeUnit>();
            var thirdPartyGroup = new List<IncludeUnit>();
            var projectModuleGroup = new List<IncludeUnit>();
            var currentModuleGroup = new List<IncludeUnit>();

            foreach (var unit in units)
            {
                int bucket = ClassifyInclude(unit.IncludeLine);

                if (bucket == 0)
                {
                    systemGroup.Add(unit);
                }
                else if (bucket == 1)
                {
                    thirdPartyGroup.Add(unit);
                }
                else if (bucket == 2)
                {
                    projectModuleGroup.Add(unit);
                }
                else
                {
                    currentModuleGroup.Add(unit);
                }
            }

            systemGroup.Sort(CompareUnitByPath);
            thirdPartyGroup.Sort(CompareUnitByPath);
            projectModuleGroup.Sort(CompareUnitByPath);
            currentModuleGroup.Sort(CompareUnitByPath);

            var newBlock = new List<string>();

            if (preprocessorLines.Count > 0)
            {
                while (preprocessorLines.Count > 0 &&
                    preprocessorLines[preprocessorLines.Count -
                    1].Trim().Length == 0)
                {
                    preprocessorLines.RemoveAt(preprocessorLines.Count - 1);
                }

                newBlock.AddRange(preprocessorLines);
                newBlock.Add(string.Empty);
            }

            AppendUnitGroup(newBlock, systemGroup);
            AppendUnitGroup(newBlock, thirdPartyGroup);
            AppendUnitGroup(newBlock, projectModuleGroup);
            AppendUnitGroup(newBlock, currentModuleGroup);

            return newBlock;
        }

        /// <summary>
        /// Builds include units and collects preprocessor directives
        /// within the include range.
        /// </summary>
        private static void BuildIncludeUnits(
            string[] lines,
            int firstInclude,
            int lastInclude,
            out List<IncludeUnit> units,
            out List<string> preprocessorLines
        )
        {
            units = new List<IncludeUnit>();
            preprocessorLines = new List<string>();
            bool inPreprocessorBlock = false;

            for (int i = firstInclude; i <= lastInclude; i++)
            {
                string trimmed = lines[i].Trim();

                if (IsPreprocessorConditionalStart(trimmed))
                {
                    var blockLines = new List<string>();
                    int depth = 1;
                    bool hasInclude = false;
                    string firstIncludeInBlock = null;
                    blockLines.Add(lines[i]);

                    int j = i + 1;

                    while (j <= lastInclude && depth > 0)
                    {
                        string jTrimmed = lines[j].Trim();
                        blockLines.Add(lines[j]);

                        if (IsPreprocessorConditionalStart(jTrimmed))
                        {
                            depth++;
                        }
                        else if (jTrimmed.StartsWith("#endif"))
                        {
                            depth--;
                        }

                        if (depth > 0 &&
                            CppTextUtils.Instance.IsIncludeDirective(jTrimmed)

                            &&
                            firstIncludeInBlock == null)
                        {
                            firstIncludeInBlock = lines[j];
                            hasInclude = true;
                        }

                        j++;
                    }

                    i = j - 1;

                    if (hasInclude)
                    {
                        units.Add(new IncludeUnit(
                            new List<string>(),
                            firstIncludeInBlock,
                            blockLines
                        ));

                        inPreprocessorBlock = false;
                    }
                    else
                    {
                        foreach (var bl in blockLines)
                        {
                            preprocessorLines.Add(bl);
                        }

                        inPreprocessorBlock = true;
                    }
                }
                else if (CppTextUtils.Instance.IsIncludeDirective(trimmed))
                {
                    units.Add(new IncludeUnit(new List<string>(), lines[i]));
                    inPreprocessorBlock = false;
                }
                else if (trimmed.Length > 0 && trimmed[0] == '#')
                {
                    preprocessorLines.Add(lines[i]);
                    inPreprocessorBlock = true;
                }
                else if (trimmed.Length == 0)
                {
                    if (inPreprocessorBlock)
                    {
                        preprocessorLines.Add(string.Empty);
                    }
                }
                else
                {
                    inPreprocessorBlock = false;
                }
            }
        }

        /// <summary>
        /// Determines whether a trimmed line starts a preprocessor
        /// conditional block: #if, #ifdef, or #ifndef. Note: #if checks
        /// must come after #ifdef and #ifndef since those also start
        /// with "#if".
        /// </summary>
        private static bool IsPreprocessorConditionalStart(string trimmed)
        {
            return trimmed.StartsWith("#ifdef") ||
                trimmed.StartsWith("#ifndef") || trimmed.StartsWith("#if");
        }

        /// <summary>
        /// Compares two include units by their include path.
        /// </summary>
        private static int CompareUnitByPath(IncludeUnit a, IncludeUnit b)
        {
            return StringComparer.Ordinal.Compare(ExtractIncludePath(a.IncludeLine),
                ExtractIncludePath(b.IncludeLine));
        }

        /// <summary>
        /// Determines the bucket for an include line: 0=System,
        /// 1=Third-party, 2=Other Project Module, 3=Current Module.
        /// </summary>
        private static int ClassifyInclude(string includeLine)
        {
            char form = GetIncludeForm(includeLine);
            string path = ExtractIncludePath(includeLine);

            if (form == '<')
            {
                if (!path.Contains(".") && !path.Contains("/") &&
                    !path.Contains("\\"))
                {
                    return 0;
                }

                return 1;
            }

            if (path.Contains("..") || path.StartsWith("/") ||
                IsWindowsAbsolutePath(path))
            {
                return 2;
            }

            return 3;
        }

        /// <summary>
        /// Extracts the delimited form of an include directive.
        /// </summary>
        private static char GetIncludeForm(string includeLine)
        {
            string s = includeLine.Trim();

            if (s.StartsWith("#include"))
            {
                s = s.Substring("#include".Length);
            }

            s = s.TrimStart();

            if (s.Length > 0 && s[0] == '<')
            {
                return '<';
            }

            return '"';
        }

        /// <summary>
        /// Extracts the bare path string from an #include line, stripping
        /// the leading #include, trailing semicolons, comments, and
        /// enclosing delimiters.
        /// </summary>
        private static string ExtractIncludePath(string includeLine)
        {
            string s = includeLine.Trim();

            if (s.StartsWith("#include"))
            {
                s = s.Substring("#include".Length);
            }

            s = s.TrimStart();

            int sc = s.IndexOf(';');

            if (sc >= 0)
            {
                s = s.Substring(0, sc);
            }

            int lineComment = s.IndexOf("//");

            if (lineComment >= 0)
            {
                s = s.Substring(0, lineComment);
            }

            int blockComment = s.IndexOf("/*");

            if (blockComment >= 0)
            {
                int blockEnd = s.IndexOf("*/", blockComment + 2);

                if (blockEnd >= 0)
                {
                    s = s.Substring(0, blockComment) + s.Substring(blockEnd +
                        2);
                }
                else
                {
                    s = s.Substring(0, blockComment);
                }
            }

            s = s.Trim();

            if (s.Length >= 2 && s[0] == '<' && s[s.Length - 1] == '>')
            {
                return s.Substring(1, s.Length - 2).Trim();
            }

            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            {
                return s.Substring(1, s.Length - 2).Trim();
            }

            return s;
        }

        /// <summary>
        /// Determines whether a path matches the Windows drive-letter
        /// absolute path pattern: letter followed by colon and a slash.
        /// </summary>
        private static bool IsWindowsAbsolutePath(string path)
        {
            if (path.Length < 3)
            {
                return false;
            }

            if (!IsAsciiLetter(path[0]))
            {
                return false;
            }

            if (path[1] != ':')
            {
                return false;
            }

            if (path[2] != '\\' && path[2] != '/')
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether a character is an ASCII letter (A-Z or a-z).
        /// </summary>
        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }
    }
}
