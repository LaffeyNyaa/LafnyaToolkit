using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using PythonFormatter;

using LafnyaToolkit.Tests;

namespace LafnyaToolkit.Tests.Snapshots
{
    /// <summary>
    /// Tests for the Python formatter's directory-discovery skip
    /// behavior. Verifies that <c>venv</c> and <c>.venv</c> directories
    /// are excluded from recursive file discovery, in addition to the
    /// universally-excluded <c>build</c> directory.
    /// </summary>
    public sealed class PythonExcludedDirectoryTests
    {
        /// <summary>
        /// Verifies that the PythonFormatter's <c>ExcludedDirectoryNames</c>
        /// property contains <c>venv</c> and <c>.venv</c>.
        /// </summary>
        public void TestExcludedDirectoryNames(bool unused)
        {
            PropertyInfo prop = typeof(Program).GetProperty(
                "ExcludedDirectoryNames",
                BindingFlags.NonPublic | BindingFlags.Instance);

            TestHarness.AssertTrue(prop != null,
                "PythonFormatter.Program must declare a protected ExcludedDirectoryNames property");
            object value = prop.GetValue(Program.Instance);
            var list = (IReadOnlyList<string>)value;
            bool hasVenv = false;
            bool hasDotVenv = false;

            foreach (var name in list)
            {
                if (string.Equals(name, "venv",
                    StringComparison.OrdinalIgnoreCase))
                {
                    hasVenv = true;
                }
                else if (string.Equals(name, ".venv",
                    StringComparison.OrdinalIgnoreCase))
                {
                    hasDotVenv = true;
                }
            }

            TestHarness.AssertTrue(hasVenv,
                "PythonFormatter.Program.ExcludedDirectoryNames must contain 'venv'");

            TestHarness.AssertTrue(hasDotVenv,
                "PythonFormatter.Program.ExcludedDirectoryNames must contain '.venv'");
        }

        /// <summary>
        /// End-to-end test: builds a temporary directory tree with files
        /// inside <c>venv</c> and <c>.venv</c> subdirectories and
        /// outside of them, invokes the formatter on the root, and
        /// verifies that the in-venv files are reported as
        /// <c>Skipped:</c> (untouched) while the out-of-venv files are
        /// processed normally.
        /// </summary>
        public void TestExcludedDirectoriesAreSkipped(bool unused)
        {
            string root = Path.Combine(Path.GetTempPath(),
                "LafnyaToolkit_venv_skip_" + Guid.NewGuid().ToString("N"));
            string venvDir = Path.Combine(root, "venv");
            string dotVenvDir = Path.Combine(root, ".venv");
            string regularDir = Path.Combine(root, "src");
            string nonVenvFile = Path.Combine(regularDir, "main.py");
            string inVenvFile = Path.Combine(venvDir, "package", "mod.py");

            string inDotVenvFile = Path.Combine(dotVenvDir, "package",
                "mod.py");

            try
            {
                Directory.CreateDirectory(Path.Combine(venvDir, "package"));
                Directory.CreateDirectory(Path.Combine(dotVenvDir, "package"));
                Directory.CreateDirectory(regularDir);

                File.WriteAllText(nonVenvFile, "x=1\n");
                File.WriteAllText(inVenvFile, "x=1\n");
                File.WriteAllText(inDotVenvFile, "x=1\n");

                TextWriter originalOut = Console.Out;
                var sw = new StringWriter();
                Console.SetOut(sw);

                try
                {
                    Program.Instance.Run(new[] { root });
                }
                finally
                {
                    Console.SetOut(originalOut);
                }

                string output = sw.ToString();

                TestHarness.AssertTrue(
                    output.Contains("Formatting: " + Path.Combine("src",
                    "main.py")) ||
                    output.Contains("Skipped: " + Path.Combine("src",
                    "main.py")),
                    "Expected the regular out-of-venv file to be discovered. Output was:\n" +
                    output);

                string venvRelative = Path.Combine("venv", "package", "mod.py");

                string dotVenvRelative = Path.Combine(".venv", "package",
                    "mod.py");

                TestHarness.AssertTrue(!output.Contains(venvRelative),
                    "Files under venv/ must be skipped entirely. Output was:\n" +
                    output);

                TestHarness.AssertTrue(!output.Contains(dotVenvRelative),
                    "Files under .venv/ must be skipped entirely. Output was:\n" +
                    output);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }
    }
}
