using System;
using System.Collections.Generic;
using System.Text;

namespace PythonFormatter
{
    /// <summary>
    /// Collects top-level import and from-import statements, classifies
    /// them into three groups (standard library, third-party, local),
    /// sorts each group alphabetically, and reassembles them at the
    /// top of the file with exactly one blank line between groups and
    /// between the import block and the first non-import statement.
    /// </summary>
    internal sealed class ImportSorter
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly ImportSorter Instance = new ImportSorter();

        private static readonly HashSet<string> ThirdPartyModules =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "numpy", "pandas", "requests", "flask", "django", "torch",
                "tensorflow", "scipy", "matplotlib", "pytest", "yaml", "PIL",
                "cv2", "sklearn", "sqlalchemy", "pydantic", "fastapi", "click",
                "attrs", "httpx", "boto3", "loguru", "rich", "attrs", "mypy",
                "black", "isort", "sphinx", "pytest", "tox", "celery", "redis",
                "pymongo", "aiohttp", "uvicorn", "gunicorn", "Pillow"
            };

        private ImportSorter()
        {
        }

        /// <summary>
        /// Sorts top-level import statements. Lines that are not
        /// imports are left in place outside the import block. Comments
        /// and blank lines inside the import block are preserved as
        /// group boundaries (an import is grouped with imports on the
        /// same side of the most recent comment or blank line).
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <returns>The source with imports sorted and grouped.</returns>
        public string Sort(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return source;
            }

            string unified = source.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = unified.Split('\n');
            int firstImport = -1;
            int lastImport = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();

                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (LineClassifier.Instance.IsImportStatement(trimmed))
                {
                    if (firstImport == -1)
                    {
                        firstImport = i;
                    }

                    lastImport = i;
                    continue;
                }

                if (firstImport == -1)
                {
                    continue;
                }

                break;
            }

            if (firstImport == -1)
            {
                return source;
            }

            var allImports = new List<string>();
            var nonImportLines = new List<string>();

            for (int i = firstImport; i <= lastImport; i++)
            {
                string raw = lines[i];
                string trimmed = raw.Trim();

                if (LineClassifier.Instance.IsImportStatement(trimmed))
                {
                    foreach (var split in SplitCombinedImport(trimmed))
                    {
                        allImports.Add(split);
                    }
                }
                else
                {
                    nonImportLines.Add(raw);
                }
            }

            var futureGroup = new List<string>();
            var stdlibGroup = new List<string>();
            var thirdPartyGroup = new List<string>();
            var localGroup = new List<string>();

            foreach (var imp in allImports)
            {
                ImportGroup group = Classify(imp);

                switch (group)
                {
                    case ImportGroup.Future: futureGroup.Add(imp); break;
                    case ImportGroup.Stdlib: stdlibGroup.Add(imp); break;
                    case ImportGroup.ThirdParty:
                        thirdPartyGroup.Add(imp); break;
                    case ImportGroup.Local: localGroup.Add(imp); break;
                }
            }

            futureGroup.Sort(CompareImports);
            stdlibGroup.Sort(CompareImports);
            thirdPartyGroup.Sort(CompareImports);
            localGroup.Sort(CompareImports);

            var newBlock = new List<string>();
            newBlock.AddRange(futureGroup);

            if (stdlibGroup.Count > 0)
            {
                if (newBlock.Count > 0)
                {
                    newBlock.Add(string.Empty);
                }

                newBlock.AddRange(stdlibGroup);
            }

            if (thirdPartyGroup.Count > 0)
            {
                if (newBlock.Count > 0)
                {
                    newBlock.Add(string.Empty);
                }

                newBlock.AddRange(thirdPartyGroup);
            }

            if (localGroup.Count > 0)
            {
                if (newBlock.Count > 0)
                {
                    newBlock.Add(string.Empty);
                }

                newBlock.AddRange(localGroup);
            }

            var result = new StringBuilder();

            int lastNonBlankHeader = -1;

            for (int i = 0; i < firstImport; i++)
            {
                if (!string.IsNullOrEmpty(lines[i].Trim()))
                {
                    lastNonBlankHeader = i;
                }
            }

            for (int i = 0; i <= lastNonBlankHeader; i++)
            {
                if (result.Length > 0)
                {
                    result.Append('\n');
                }

                result.Append(lines[i]);
            }

            for (int i = 0; i < newBlock.Count; i++)
            {
                if (result.Length > 0)
                {
                    result.Append('\n');
                }

                result.Append(newBlock[i]);
            }

            if (nonImportLines.Count > 0)
            {
                if (result.Length > 0 && !EndsWithBlankLine(result))
                {
                    result.Append('\n');
                }

                result.Append(string.Empty);

                for (int i = 0; i < nonImportLines.Count; i++)
                {
                    result.Append('\n');
                    result.Append(nonImportLines[i]);
                }
            }

            int after = lastImport + 1;

            while (after < lines.Length &&
                string.IsNullOrEmpty(lines[after].Trim()))
            {
                after++;
            }

            if (after < lines.Length)
            {
                if (result.Length > 0 && !EndsWithBlankLine(result))
                {
                    result.Append('\n');
                }

                result.Append(string.Empty);
                result.Append('\n');
                result.Append(string.Empty);

                for (int i = after; i < lines.Length; i++)
                {
                    if (result.Length > 0)
                    {
                        result.Append('\n');
                    }

                    result.Append(lines[i]);
                }
            }

            return result.ToString();
        }

        private static bool EndsWithBlankLine(StringBuilder sb)
        {
            return sb.Length > 0 && sb[sb.Length - 1] == '\n';
        }

        private static void FlushSegment(List<string> newBlock,
            List<string> segment, List<string> comments)
        {
            if (segment.Count == 0)
            {
                if (comments.Count > 0 && newBlock.Count > 0 &&
                    newBlock[newBlock.Count - 1] != string.Empty)
                {
                    newBlock.Add(string.Empty);
                }

                newBlock.AddRange(comments);
                comments.Clear();
                return;
            }

            var futureGroup = new List<string>();
            var stdlibGroup = new List<string>();
            var thirdPartyGroup = new List<string>();
            var localGroup = new List<string>();

            foreach (var imp in segment)
            {
                ImportGroup group = Classify(imp);

                switch (group)
                {
                    case ImportGroup.Future: futureGroup.Add(imp); break;
                    case ImportGroup.Stdlib: stdlibGroup.Add(imp); break;
                    case ImportGroup.ThirdParty:
                        thirdPartyGroup.Add(imp); break;
                    case ImportGroup.Local: localGroup.Add(imp); break;
                }
            }

            futureGroup.Sort(CompareImports);
            stdlibGroup.Sort(CompareImports);
            thirdPartyGroup.Sort(CompareImports);
            localGroup.Sort(CompareImports);

            if (comments.Count > 0 && newBlock.Count > 0 &&
                newBlock[newBlock.Count - 1] != string.Empty)
            {
                newBlock.Add(string.Empty);
            }

            newBlock.AddRange(comments);
            comments.Clear();

            if (newBlock.Count > 0 && newBlock[newBlock.Count - 1] !=
                string.Empty)
            {
                newBlock.Add(string.Empty);
            }

            // Append groups in PEP 8 order: future imports first
            // (without a leading blank line separator), then stdlib,
            // then third-party, then local.
            AppendGroup(newBlock, futureGroup, suppressLeadingBlank: true);
            AppendGroup(newBlock, stdlibGroup);
            AppendGroup(newBlock, thirdPartyGroup);
            AppendGroup(newBlock, localGroup);

            segment.Clear();
        }

        private static void AppendGroup(List<string> block, List<string> group)
        {
            AppendGroup(block, group, suppressLeadingBlank: false);
        }

        private static void AppendGroup(List<string> block, List<string> group,
            bool suppressLeadingBlank)
        {
            if (group.Count == 0)
            {
                return;
            }

            if (!suppressLeadingBlank && block.Count > 0 &&
                block[block.Count - 1] != string.Empty)
            {
                block.Add(string.Empty);
            }

            block.AddRange(group);
        }

        private static int CompareImports(string a, string b)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(a, b);
        }

        /// <summary>
        /// Splits a combined import statement (e.g. <c>import os, sys,
        /// json</c>) into separate single-import statements. Imports
        /// that contain <c>as</c> aliases are preserved verbatim.
        /// </summary>
        /// <param name="trimmed">The trimmed import line.</param>
        /// <returns>The list of single imports.</returns>
        private static IEnumerable<string> SplitCombinedImport(string trimmed)
        {
            if (!trimmed.StartsWith("import ", StringComparison.Ordinal))
            {
                yield return trimmed;
                yield break;
            }

            string body = trimmed.Substring("import ".Length);
            var parts = new List<string>();
            int depth = 0;
            int start = 0;
            bool inString = false;
            char stringChar = '\0';

            for (int i = 0; i < body.Length; i++)
            {
                char c = body[i];

                if (inString)
                {
                    if (c == stringChar && (i == 0 || body[i - 1] != '\\'))
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    inString = true;
                    stringChar = c;
                    continue;
                }

                if (c == '(' || c == '[' || c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == ')' || c == ']' || c == '}')
                {
                    depth--;
                    continue;
                }

                if (c == ',' && depth == 0)
                {
                    string part = body.Substring(start, i - start).Trim();

                    if (part.Length > 0)
                    {
                        parts.Add("import " + part);
                    }

                    start = i + 1;
                }
            }

            string last = body.Substring(start).Trim();

            if (last.Length > 0)
            {
                parts.Add("import " + last);
            }

            foreach (var p in parts)
            {
                yield return p;
            }
        }

        /// <summary>
        /// Classifies an import statement into one of four groups:
        /// future, standard library, third-party, or local. The
        /// classification uses a conservative heuristic since the
        /// formatter does not execute Python: an unknown module
        /// name containing a dot is treated as a local package
        /// (e.g. <c>myproject.utils</c>), and any single-name module
        /// not in the third-party allowlist whose name consists only
        /// of lowercase ASCII letters/digits/underscores is treated
        /// as stdlib.
        /// </summary>
        /// <param name="importLine">The trimmed import line.</param>
        /// <returns>The import group.</returns>
        internal static ImportGroup Classify(string importLine)
        {
            string top = ExtractTopLevelModule(importLine);
            // PEP 8: `from __future__ import ...` statements must come
            // before any other import statement in the file. They are
            // a special category and get their own group at the top.

            if (top == "__future__")
            {
                return ImportGroup.Future;
            }

            if (top.StartsWith(".", StringComparison.Ordinal))
            {
                return ImportGroup.Local;
            }

            string topPackage = top;
            int dotIdx = topPackage.IndexOf('.');

            if (dotIdx > 0)
            {
                topPackage = topPackage.Substring(0, dotIdx);
            }

            if (ThirdPartyModules.Contains(topPackage))
            {
                return ImportGroup.ThirdParty;
            }

            if (top.IndexOf('.') >= 0)
            {
                return ImportGroup.Local;
            }

            if (IsLikelyStdlib(topPackage))
            {
                return ImportGroup.Stdlib;
            }

            return ImportGroup.ThirdParty;
        }

        private static bool IsLikelyStdlib(string name)
        {
            if (name.Length == 0)
            {
                return false;
            }

            if (!char.IsLower(name[0]))
            {
                return false;
            }

            foreach (char c in name)
            {
                if (!(char.IsLower(c) || char.IsDigit(c) || c == '_'))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Extracts the top-level module name from an import
        /// statement. For <c>import x.y.z</c> returns <c>x</c>; for
        /// <c>from x.y import z</c> returns <c>x</c>.
        /// </summary>
        /// <param name="importLine">The trimmed import line.</param>
        /// <returns>The top-level module name.</returns>
        internal static string ExtractTopLevelModule(string importLine)
        {
            string s = importLine.Trim();

            if (s.StartsWith("from ", StringComparison.Ordinal))
            {
                s = s.Substring("from ".Length);
                int spaceIdx = s.IndexOf(' ');
                int tabIdx = s.IndexOf('\t');
                int cutIdx = spaceIdx;

                if (tabIdx >= 0 && (cutIdx < 0 || tabIdx < cutIdx))
                {
                    cutIdx = tabIdx;
                }

                if (cutIdx < 0)
                {
                    return s.Trim();
                }

                s = s.Substring(0, cutIdx);
                return s.Trim();
            }

            if (s.StartsWith("import ", StringComparison.Ordinal))
            {
                s = s.Substring("import ".Length);
                int comma = s.IndexOf(',');
                int asIdx = s.IndexOf(" as ", StringComparison.Ordinal);

                if (asIdx >= 0 && (comma < 0 || asIdx < comma))
                {
                    comma = asIdx;
                }

                if (comma < 0)
                {
                    return s.Trim();
                }

                s = s.Substring(0, comma);
                return s.Trim();
            }

            return s;
        }
    }

    /// <summary>
    /// Group classification for an import statement.
    /// </summary>
    internal enum ImportGroup
    {
        /// <summary>
        /// <c>from __future__ import ...</c> statements. These must
        /// appear at the very top of the file, before any other import
        /// (PEP 8: "future imports must come first").
        /// </summary>
        Future,

        /// <summary>Python standard library.</summary>
        Stdlib,

        /// <summary>Third-party package.</summary>
        ThirdParty,

        /// <summary>Local / relative import.</summary>
        Local
    }
}
