using System;
using System.IO;
using LafnyaToolkit.Tests;
using CppFormatter;
using CSharpFormatter;
using GDScriptFormatter;
using JavaFormatter;
using JsonFormatter;

namespace LafnyaToolkit.Tests.Snapshots
{
    /// <summary>
    /// Snapshot tests using golden-file comparison. For each .in file under
    /// <c>Snapshots/&lt;Language&gt;/</c>, the test invokes the corresponding
    /// formatter and compares the output byte-for-byte with the .expected
    /// file. Setting <c>updateSnapshots=true</c> regenerates the .expected
    /// files from the actual formatter output.
    /// </summary>
    public sealed class SnapshotTests
    {
        /// <summary>
        /// Runs all snapshot tests for all five formatters.
        /// </summary>
        public void TestCppSnapshots(bool updateSnapshots) => RunCpp(updateSnapshots);
        public void TestCSharpSnapshots(bool updateSnapshots) => RunCSharp(updateSnapshots);
        public void TestGDScriptSnapshots(bool updateSnapshots) => RunGDScript(updateSnapshots);
        public void TestJavaSnapshots(bool updateSnapshots) => RunJava(updateSnapshots);
        public void TestJsonSnapshots(bool updateSnapshots) => RunJson(updateSnapshots);

        private static void RunCpp(bool updateSnapshots)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Snapshots", "Cpp");
            RunCases(dir, "C++", updateSnapshots, input => CppFormatter.Formatter.Instance.Format(input));
        }

        private static void RunCSharp(bool updateSnapshots)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Snapshots", "CSharp");
            RunCases(dir, "C#", updateSnapshots, input => CSharpFormatter.Formatter.Instance.Format(input, string.Empty));
        }

        private static void RunGDScript(bool updateSnapshots)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Snapshots", "GDScript");
            RunCases(dir, "GDScript", updateSnapshots, input => GDScriptFormatter.Formatter.Instance.Format(input));
        }

        private static void RunJava(bool updateSnapshots)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Snapshots", "Java");
            RunCases(dir, "Java", updateSnapshots, input => JavaFormatter.Formatter.Instance.Format(input, string.Empty));
        }

        private static void RunJson(bool updateSnapshots)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Snapshots", "Json");
            RunCases(dir, "JSON", updateSnapshots, input => JsonFormatter.JsonFormatter.Instance.Format(input));
        }

        private static void RunCases(string dir, string language, bool updateSnapshots, Func<string, string> format)
        {
            if (!Directory.Exists(dir))
            {
                throw new TestFailureException("Snapshot directory not found: " + dir);
            }

            var inFiles = Directory.GetFiles(dir, "*.in");
            Array.Sort(inFiles, StringComparer.Ordinal);

            if (inFiles.Length == 0)
            {
                throw new TestFailureException("No .in files found in " + dir);
            }

            foreach (var inFile in inFiles)
            {
                string name = Path.GetFileNameWithoutExtension(inFile);
                string expectedFile = Path.Combine(dir, name + ".expected");
                string input = File.ReadAllText(inFile);
                string actual = format(input);

                if (updateSnapshots)
                {
                    File.WriteAllText(expectedFile, actual);
                    continue;
                }

                if (!File.Exists(expectedFile))
                {
                    throw new TestFailureException(
                        language + " snapshot missing: " + expectedFile + " (run with snapshot-update to create)");
                }

                string expected = File.ReadAllText(expectedFile);
                TestHarness.AssertEqual(expected, actual);
            }
        }
    }
}
