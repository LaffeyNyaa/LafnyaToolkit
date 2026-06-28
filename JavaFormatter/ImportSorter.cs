using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JavaFormatter
{
    /// <summary>
    /// Collects top-level import directives and sorts them into four groups:
    /// System (java.*/javax.*) / Third-party / Project other modules / Current module.
    /// </summary>
    internal static class ImportSorter
    {
        /// <summary>
        /// Resolves the current module name and project root from the package declaration in the source.
        /// </summary>
        /// <param name="source">The source code string.</param>
        /// <param name="targetRoot">The target root directory path (used as fallback).</param>
        /// <param name="currentModule">Output: the fully qualified package name, or null if no package.</param>
        /// <param name="projectRoot">Output: the project root prefix.</param>
        public static void ResolveCurrentModule(string source, string targetRoot,
            out string currentModule, out string projectRoot)
        {
            currentModule = null;
            projectRoot = null;

            string unified = source.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = unified.Split('\n');

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("package "))
                {
                    string afterPackage = trimmed.Substring("package ".Length);
                    int semi = afterPackage.IndexOf(';');

                    if (semi >= 0)
                    {
                        currentModule = afterPackage.Substring(0, semi).Trim();
                    }

                    break;
                }

                if (trimmed.Length > 0 && !trimmed.StartsWith("//") &&
                    !trimmed.StartsWith("/*") && !trimmed.StartsWith("*") &&
                    !trimmed.StartsWith("import "))
                {
                    break;
                }
            }

            if (!string.IsNullOrEmpty(currentModule))
            {
                int lastDot = currentModule.LastIndexOf('.');

                if (lastDot > 0)
                {
                    projectRoot = currentModule.Substring(0, lastDot);
                }

                else
                {
                    projectRoot = currentModule;
                }
            }

            else
            {
                projectRoot = Path.GetFileName(targetRoot.TrimEnd('\\', '/'));
            }
        }

        /// <summary>
        /// Identifies the top-level import block in the source, regroups and sorts the imports,
        /// and replaces the original block. If there are no top-level import directives, returns the source unchanged.
        /// </summary>
        /// <param name="source">The source code string.</param>
        /// <param name="targetRoot">The target root directory path (used as fallback when no package).</param>
        /// <returns>The source with sorted imports.</returns>
        public static string Sort(string source, string targetRoot)
        {
            string currentModule;
            string projectRoot;
            ResolveCurrentModule(source, targetRoot, out currentModule,
                out projectRoot);

            string unified = source.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = unified.Split('\n');
            int firstImport = -1;
            int lastImport = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();

                if (IsImportDirective(trimmed))
                {
                    if (firstImport == -1)
                    {
                        firstImport = i;
                    }

                    lastImport = i;
                    continue;
                }

                if (trimmed.StartsWith("package "))
                {
                    continue;
                }

                if (trimmed.Length == 0 || IsCommentLine(trimmed))
                {
                    continue;
                }

                break;
            }

            if (firstImport == -1)
            {
                return source;
            }

            var newBlock = new List<string>();
            var currentSegment = new List<string>();

            for (int i = firstImport; i <= lastImport; i++)
            {
                string trimmed = lines[i].Trim();

                if (IsImportDirective(trimmed))
                {
                    currentSegment.Add(trimmed);
                }
                else if (IsCommentLine(trimmed))
                {
                    AppendSortedSegment(newBlock, currentSegment, currentModule,
                        projectRoot);
                    currentSegment.Clear();
                    newBlock.Add(trimmed);
                }
            }

            AppendSortedSegment(newBlock, currentSegment, currentModule,
                projectRoot);

            var result = new StringBuilder();

            for (int i = 0; i < firstImport; i++)
            {
                if (result.Length > 0)
                {
                    result.Append('\n');
                }

                result.Append(lines[i]);
            }

            foreach (string line in newBlock)
            {
                if (result.Length > 0)
                {
                    result.Append('\n');
                }

                result.Append(line);
            }

            int after = lastImport + 1;

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

        private static void AppendSortedSegment(List<string> newBlock,
            List<string> segment, string currentModule, string projectRoot)
        {
            if (segment.Count == 0)
            {
                return;
            }

            var systemGroup = new List<string>();
            var thirdPartyGroup = new List<string>();
            var projectModuleGroup = new List<string>();
            var currentModuleGroup = new List<string>();

            foreach (var imp in segment)
            {
                string ns = ExtractNamespace(imp);

                if (!string.IsNullOrEmpty(currentModule) && ns == currentModule)
                {
                    currentModuleGroup.Add(imp);
                }

                else if (!string.IsNullOrEmpty(projectRoot) &&
                    ns.StartsWith(projectRoot + ".") &&
                    (string.IsNullOrEmpty(currentModule) || ns != currentModule))
                {
                    projectModuleGroup.Add(imp);
                }

                else if (ns.StartsWith("java.") || ns.StartsWith("javax."))
                {
                    systemGroup.Add(imp);
                }

                else
                {
                    thirdPartyGroup.Add(imp);
                }
            }

            systemGroup.Sort(CompareByNamespace);
            thirdPartyGroup.Sort(CompareByNamespace);
            projectModuleGroup.Sort(CompareByNamespace);
            currentModuleGroup.Sort(CompareByNamespace);

            var segmentBlock = new List<string>();
            AppendGroup(segmentBlock, systemGroup);
            AppendGroup(segmentBlock, thirdPartyGroup);
            AppendGroup(segmentBlock, projectModuleGroup);
            AppendGroup(segmentBlock, currentModuleGroup);

            newBlock.AddRange(segmentBlock);
        }

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

        private static int CompareByNamespace(string a, string b)
        {
            int c = StringComparer.Ordinal.Compare(ExtractNamespace(a),
                ExtractNamespace(b));

            if (c != 0)
            {
                return c;
            }

            return StringComparer.Ordinal.Compare(a, b);
        }

        private static bool IsImportDirective(string line)
        {
            if (line.StartsWith("import "))
            {
                return true;
            }

            if (line.StartsWith("import\t"))
            {
                return true;
            }

            return line == "import";
        }

        private static bool IsCommentLine(string line)
        {
            return line.StartsWith("//") || line.StartsWith("/*") ||
                line.StartsWith("*");
        }

        private static string ExtractNamespace(string importLine)
        {
            string s = importLine.Trim();

            if (s.StartsWith("import "))
            {
                s = s.Substring("import ".Length);
            }

            if (s.StartsWith("static "))
            {
                s = s.Substring("static ".Length);
            }

            int semi = s.IndexOf(';');

            if (semi >= 0)
            {
                s = s.Substring(0, semi);
            }

            s = s.Trim();

            int lastDot = s.LastIndexOf('.');

            if (lastDot > 0)
            {
                return s.Substring(0, lastDot);
            }

            return s;
        }
    }
}
