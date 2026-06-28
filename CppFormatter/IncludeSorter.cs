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
        /// Identifies the top-level #include block in the source string, re-groups,
        /// sorts, and replaces it according to the rules.
        /// Returns the source unchanged if there are no top-level #include directives.
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <returns>The source string with sorted #include directives.</returns>
        public static string Sort(string source)
        {
            string unified = source.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = unified.Split('\n');
            int firstInclude = -1;
            int lastInclude = -1;
            int firstCodeLine = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();

                if (IsIncludeDirective(trimmed))
                {
                    if (firstInclude == -1)
                    {
                        firstInclude = i;
                    }

                    lastInclude = i;
                    continue;
                }

                if (trimmed.Length == 0 || IsCommentLine(trimmed))
                {
                    continue;
                }

                firstCodeLine = i;
                break;
            }

            if (firstInclude == -1)
            {
                return source;
            }

            var includes = new List<string>();

            for (int i = firstInclude; i <= lastInclude; i++)
            {
                string trimmed = lines[i].Trim();

                if (IsIncludeDirective(trimmed))
                {
                    includes.Add(trimmed);
                }
            }

            var systemGroup = new List<string>();
            var thirdPartyGroup = new List<string>();
            var projectModuleGroup = new List<string>();
            var currentModuleGroup = new List<string>();

            foreach (var inc in includes)
            {
                int bucket = ClassifyInclude(inc);

                if (bucket == 0)
                {
                    systemGroup.Add(inc);
                }

                else if (bucket == 1)
                {
                    thirdPartyGroup.Add(inc);
                }

                else if (bucket == 2)
                {
                    projectModuleGroup.Add(inc);
                }

                else
                {
                    currentModuleGroup.Add(inc);
                }
            }

            systemGroup.Sort(CompareByPath);
            thirdPartyGroup.Sort(CompareByPath);
            projectModuleGroup.Sort(CompareByPath);
            currentModuleGroup.Sort(CompareByPath);
            var newBlock = new List<string>();
            AppendGroup(newBlock, systemGroup);
            AppendGroup(newBlock, thirdPartyGroup);
            AppendGroup(newBlock, projectModuleGroup);
            AppendGroup(newBlock, currentModuleGroup);
            var result = new StringBuilder();

            for (int i = 0; i < firstInclude; i++)
            {
                if (result.Length > 0)
                {
                    result.Append('\n');
                }

                result.Append(lines[i]);
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

        /// <summary>Appends include lines from one bucket to the result block, inserting exactly one blank line between buckets.</summary>
        /// <param name="block">The result block being built.</param>
        /// <param name="group">The list of include lines for the current bucket.</param>
        private static void AppendGroup(List<string> block, List<string> group)
        {
            if (group.Count == 0)
            {
                return;
            }

            if (block.Count > 0)
            {
                block.Add(string.Empty);
            }

            block.AddRange(group);
        }

        /// <summary>Compares by include path using Ordinal lexicographic order; falls back to comparing original lines Ordinal when paths are equal, to maintain stability.</summary>
        /// <param name="a">The first line.</param>
        /// <param name="b">The second line.</param>
        /// <returns>The comparison result.</returns>
        private static int CompareByPath(string a, string b)
        {
            int c = StringComparer.Ordinal.Compare(ExtractIncludePath(a),
                ExtractIncludePath(b));

            if (c != 0)
            {
                return c;
            }

            return StringComparer.Ordinal.Compare(a, b);
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
                if (!path.Contains(".") && !path.Contains("/") && !path.Contains("\\"))
                {
                    return 0;
                }

                return 1;
            }

            if (path.Contains("..") || path.StartsWith("/") || IsWindowsAbsolutePath(path))
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
                    s = s.Substring(0, blockComment) + s.Substring(blockEnd + 2);
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
    }
}
