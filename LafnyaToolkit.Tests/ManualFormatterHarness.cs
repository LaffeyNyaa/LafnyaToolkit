using System;
using System.IO;
using System.Reflection;

using PythonFormatter;

namespace LafnyaToolkit.Tests
{
    /// <summary>
    /// Manually runs the Python formatter on a target directory
    /// (default: the Samples directory of this repo) and reports the
    /// result. Used for diagnosing idempotency issues outside of the
    /// snapshot test harness.
    /// </summary>
    public sealed class ManualFormatterHarnessTests
    {
        public void TestRun(bool unused)
        {
            string[] args = Environment.GetCommandLineArgs();
            string target = args.Length > 1
            ? args[1]
            : @"c:\Users\LaffeyNyaa\Desktop\Repositories\LafnyaToolkit\Samples";

            if (!Directory.Exists(target))
            {
                Console.Error.WriteLine("Directory not found: " + target);
                return;
            }

            var files = Directory.GetFiles(target, "*.py",
                SearchOption.AllDirectories);

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                string original = File.ReadAllText(file);
                string first = Formatter.Instance.Format(original);
                string second = Formatter.Instance.Format(first);

                string relative = file.StartsWith(target,
                    StringComparison.OrdinalIgnoreCase)

                ? file.Substring(target.Length).TrimStart(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                : file;

                bool idempotent = string.Equals(first, second,
                    StringComparison.Ordinal);

                bool changed = !string.Equals(original, first,
                    StringComparison.Ordinal);

                if (changed)
                {
                    File.WriteAllText(file, first);
                }

                string status = idempotent
                ? (changed ? "Formatting: " : "Skipped: ")
                : "IDEMPOTENCY VIOLATION: ";
                Console.WriteLine(status + relative);
            }
        }
    }
}
