using System;
using System.Collections.Generic;
using System.Text;

namespace CppFormatter
{
    /// <summary>
    /// Collects top-level #include directives and sorts them into four groups:
    /// System Libraries / Third-party Libraries / Other Project Modules / Current Module.
    /// </summary>
    internal static class IncludeSorter
    {
        /// <summary>
        /// Scans the entire source for all top-level #include directives,
        /// collects each include together with any preceding non-include
        /// lines (preprocessor directives, blank lines, comments) into a
        /// unit, sorts units by category (System / Third-party / Other
        /// Project / Current Module), then rebuilds the source with the
        /// sorted include region.
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <returns>The source string with sorted #include directives.</returns>
        public static string Sort(string source)
        {
            string unified = source.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = unified.Split('\n');
            // Phase 1: Find ALL include lines across the entire file.
            int firstInclude = -1;
            int lastInclude = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                if (IsIncludeDirective(lines[i].Trim()))
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

            // Phase 2: Build include units and collect non-include preprocessor
            // directives that appear between include lines. Preprocessor
            // directives (#ifndef, #define, #endif, etc.) are extracted and
            // placed at the very top of the file, before any #include.
            var units = new List<IncludeUnit>();
            var preprocessorLines = new List<string>();
            bool inPreprocessorBlock = false;

            for (int i = firstInclude; i <= lastInclude; i++)
            {
                string trimmed = lines[i].Trim();

                if (IsIncludeDirective(trimmed))
                {
                    units.Add(new IncludeUnit(
                        new List<string>(), lines[i]));
                }
                else if (trimmed.Length > 0 && trimmed[0] == '#')
                {
                    // Preprocessor directive (not an #include) —
                    // collect for placement before the include block.
                    preprocessorLines.Add(lines[i]);
                    inPreprocessorBlock = true;
                }
                else if (trimmed.Length == 0)
                {
                    // Blank line — add between preprocessor directives
                    // only if we're inside a preprocessor block.

                    if (inPreprocessorBlock)
                    {
                        preprocessorLines.Add(string.Empty);
                    }
                }
                else
                {
                    // Non-preprocessor, non-include content — stop collecting.
                    inPreprocessorBlock = false;
                }
            }

            // Phase 3: Sort units by category, then by include path.
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
            // Phase 4: Build the sorted include block.
            var newBlock = new List<string>();
            // 4a: Preprocessor directives go first (before any #include).

            if (preprocessorLines.Count > 0)
            {
                // Trim trailing blank lines from preprocessor block.

                while (preprocessorLines.Count > 0 &&
                    preprocessorLines[preprocessorLines.Count -
                    1].Trim().Length == 0)
                {
                    preprocessorLines.RemoveAt(preprocessorLines.Count - 1);
                }

                newBlock.AddRange(preprocessorLines);
                newBlock.Add(string.Empty);
            }

            // 4b: Sorted include groups.
            AppendUnitGroup(newBlock, systemGroup);
            AppendUnitGroup(newBlock, thirdPartyGroup);
            AppendUnitGroup(newBlock, projectModuleGroup);
            AppendUnitGroup(newBlock, currentModuleGroup);
            // Phase 5: Rebuild the source with the sorted block in place.
            var result = new StringBuilder();

            for (int i = 0; i < firstInclude; i++)
            {
                if (result.Length > 0)
                {
                    result.Append('\n');
                }

                result.Append(lines[i]);
            }

            // Ensure a blank line between a non-include preprocessor directive
            // (e.g., #pragma once) and the first #include directive. Scan
            // backward past any blank lines to find the actual content line.
            if (firstInclude > 0 && newBlock.Count > 0)
            {
                int scanIdx = firstInclude - 1;

                while (scanIdx >= 0 &&
                    lines[scanIdx].Trim().Length == 0)
                {
                    scanIdx--;
                }

                if (scanIdx >= 0)
                {
                    string lastBeforeInclude = lines[scanIdx].Trim();

                    if (!IsIncludeDirective(lastBeforeInclude) &&
                        lastBeforeInclude.Length > 0 &&
                        lastBeforeInclude[0] == '#' &&
                        IsIncludeDirective(newBlock[0]))
                    {
                        result.Append('\n');
                    }
                }
            }

            foreach (var line in newBlock)
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
        /// Appends a group of include units to the block, with a blank line
        /// separator if the block is non-empty. Each unit's preceding lines
        /// (preprocessor directives, etc.) are emitted before the include
        /// line.
        /// </summary>
        private static void AppendUnitGroup(List<string> block,
            List<IncludeUnit> group)
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
                block.AddRange(unit.PrecedingLines);
                block.Add(unit.IncludeLine);
            }
        }

        /// <summary>
        /// Compares two include units by their include path.
        /// </summary>
        private static int CompareUnitByPath(IncludeUnit a, IncludeUnit b)
        {
            return StringComparer.Ordinal.Compare(
                ExtractIncludePath(a.IncludeLine),
                ExtractIncludePath(b.IncludeLine));
        }

        /// <summary>Determines the bucket for an include line: 0=System, 1=Third-party, 2=Other Project Module, 3=Current Module.</summary>
        /// <param name="includeLine">The include directive line.</param>
        /// <returns>The bucket number (0-3).</returns>
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

        /// <summary>Determines whether a line is an #include directive.</summary>
        /// <param name="line">The trimmed line.</param>
        /// <returns>true if the line is an #include directive; otherwise false.</returns>
        private static bool IsIncludeDirective(string line)
        {
            if (line.StartsWith("#include "))
            {
                return true;
            }

            if (line.StartsWith("#include\t"))
            {
                return true;
            }

            if (line.StartsWith("#include<") || line.StartsWith("#include\""))
            {
                return true;
            }

            return line == "#include";
        }

        /// <summary>Determines whether a line is a comment start (//, /*, or * continuation).</summary>
        /// <param name="line">The trimmed line.</param>
        /// <returns>true if the line is a comment line; otherwise false.</returns>
        private static bool IsCommentLine(string line)
        {
            return line.StartsWith("//") || line.StartsWith("/*") ||
                line.StartsWith("*");
        }

        /// <summary>Extracts the delimited form of an include directive.</summary>
        /// <param name="includeLine">The include directive line.</param>
        /// <returns>'&lt;' for angle brackets, '"' for quotes.</returns>
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

        /// <summary>Extracts the bare path string from an #include line, stripping the leading #include, trailing semicolons, comments, and enclosing delimiters.</summary>
        /// <param name="includeLine">The include directive line.</param>
        /// <returns>The bare path string.</returns>
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

        /// <summary>Determines whether a path matches the Windows drive-letter absolute path pattern ^[A-Za-z]:[\\/].</summary>
        /// <param name="path">The include path.</param>
        /// <returns>true if matched; otherwise false.</returns>
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

        /// <summary>Determines whether a character is an ASCII letter (A-Z or a-z).</summary>
        /// <param name="c">The character to test.</param>
        /// <returns>true if the character is an ASCII letter; otherwise false.</returns>
        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        /// <summary>
        /// Represents a single #include directive together with any preceding
        /// lines (preprocessor directives, blank lines, comments) that appeared
        /// between this include and the previous include.
        /// </summary>
        private class IncludeUnit
        {
            /// <summary>Gets the preceding lines (preprocessor, blanks, etc.).</summary>
            public List<string> PrecedingLines
            {
                get;
            }

            /// <summary>Gets the raw #include directive line.</summary>
            public string IncludeLine
            {
                get;
            }

            /// <summary>
            /// Initializes a new instance of the IncludeUnit class.
            /// </summary>
            /// <param name="precedingLines">The lines preceding this include.</param>
            /// <param name="includeLine">The include directive line.</param>
            public IncludeUnit(List<string> precedingLines,
                string includeLine)
            {
                PrecedingLines = precedingLines;
                IncludeLine = includeLine;
            }
        }
    }
}
