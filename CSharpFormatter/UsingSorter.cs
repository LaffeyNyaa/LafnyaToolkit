using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace CSharpFormatter
{
    /// <summary>
    /// Collects top-level using directives and sorts them into four groups:
    /// System* / Third-party / Other project modules / Current module.
    /// </summary>
    internal static class UsingSorter
    {
        /// <summary>
        /// Walks upward from the file path to find a .csproj file, then
        /// resolves its &lt;RootNamespace&gt; element. Falls back to the
        /// directory name of <paramref name="targetRoot"/> if no .csproj
        /// is found or the element is absent.
        /// </summary>
        /// <param name="filePath">The source file path.</param>
        /// <param name="targetRoot">The target root directory path
        /// (used as fallback).</param>
        /// <returns>The resolved root namespace.</returns>
        public static string ResolveRootNamespace(string filePath,
            string targetRoot)
        {
            string dir = Path.GetDirectoryName(filePath);

            while (!string.IsNullOrEmpty(dir))
            {
                string[] csprojs = Directory.GetFiles(dir, "*.csproj");

                if (csprojs.Length > 0)
                {
                    string rootNs = null;

                    try
                    {
                        var doc = XDocument.Load(csprojs[0]);
                        XNamespace ns =
                            "http://schemas.microsoft.com/developer/msbuild/2003";

                        foreach (var elem in doc.Descendants(ns +
                            "RootNamespace"))
                        {
                            rootNs = elem.Value;
                            break;
                        }
                    }

                    catch (Exception)
                    {
                        rootNs = null;
                    }

                    if (!string.IsNullOrWhiteSpace(rootNs))
                    {
                        return rootNs.Trim();
                    }
                }

                string parent = Path.GetDirectoryName(dir);

                if (parent == dir)
                {
                    break;
                }

                dir = parent;
            }

            return Path.GetFileName(targetRoot.TrimEnd('\\', '/'));
        }

        /// <summary>
        /// Identifies the top-level using block in the source string,
        /// re-groups, sorts, and replaces it according to the rules.
        /// Returns the source unchanged if there are no top-level using
        /// directives.
        /// </summary>
        /// <param name="source">The source code string.</param>
        /// <param name="rootNamespace">The root namespace of the current
        /// module.</param>
        /// <returns>The source string with using directives sorted.</returns>
        public static string Sort(string source, string rootNamespace)
        {
            string unified = source.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = unified.Split('\n');
            int firstUsing = -1;
            int lastUsing = -1;
            int firstCodeLine = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();

                if (IsUsingDirective(trimmed))
                {
                    if (firstUsing == -1)
                    {
                        firstUsing = i;
                    }

                    lastUsing = i;
                    continue;
                }

                if (trimmed.Length == 0 || IsCommentLine(trimmed))
                {
                    continue;
                }

                firstCodeLine = i;
                break;
            }

            if (firstUsing == -1)
            {
                return source;
            }

            var usings = new List<string>();

            for (int i = firstUsing; i <= lastUsing; i++)
            {
                string trimmed = lines[i].Trim();

                if (IsUsingDirective(trimmed))
                {
                    usings.Add(trimmed);
                }
            }

            var systemGroup = new List<string>();
            var thirdPartyGroup = new List<string>();
            var projectModuleGroup = new List<string>();
            var currentModuleGroup = new List<string>();

            foreach (var u in usings)
            {
                string ns = ExtractNamespace(u);

                if (ns == rootNamespace)
                {
                    currentModuleGroup.Add(u);
                }

                else if (ns.StartsWith(rootNamespace + "."))
                {
                    projectModuleGroup.Add(u);
                }

                else if (ns.StartsWith("System"))
                {
                    systemGroup.Add(u);
                }

                else
                {
                    thirdPartyGroup.Add(u);
                }
            }

            systemGroup.Sort(CompareByNamespace);
            thirdPartyGroup.Sort(CompareByNamespace);
            projectModuleGroup.Sort(CompareByNamespace);
            currentModuleGroup.Sort(CompareByNamespace);
            var newBlock = new List<string>();
            AppendGroup(newBlock, systemGroup);
            AppendGroup(newBlock, thirdPartyGroup);
            AppendGroup(newBlock, projectModuleGroup);
            AppendGroup(newBlock, currentModuleGroup);
            var result = new StringBuilder();

            for (int i = 0; i < firstUsing; i++)
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

            int after = lastUsing + 1;

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

        private static bool IsUsingDirective(string line)
        {
            if (line.StartsWith("using "))
            {
                return true;
            }

            if (line.StartsWith("using\t"))
            {
                return true;
            }

            return line == "using";
        }

        private static bool IsCommentLine(string line)
        {
            return line.StartsWith("//") || line.StartsWith("/*") ||
                line.StartsWith("*");
        }

        private static string ExtractNamespace(string usingLine)
        {
            string s = usingLine.Trim();

            if (s.StartsWith("using "))
            {
                s = s.Substring("using ".Length);
            }

            if (s.StartsWith("static "))
            {
                s = s.Substring("static ".Length);
            }

            int eq = s.IndexOf('=');

            if (eq >= 0)
            {
                s = s.Substring(0, eq);
            }

            int sc = s.IndexOf(';');

            if (sc >= 0)
            {
                s = s.Substring(0, sc);
            }

            return s.Trim();
        }
    }
}
