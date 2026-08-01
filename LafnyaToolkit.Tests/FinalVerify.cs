using System;
using System.IO;

using CSharpFormatter;
using LafnyaToolkit.Core.IO;
using LafnyaToolkit.Tests;

namespace LafnyaToolkit.Tests.Idempotency
{
    /// <summary>
    /// Idempotency verification for the 5 known oscillating sample
    /// files. Asserts that Format(Format(x)) == Format(x) for each
    /// file.
    /// </summary>
    public sealed class FinalVerifyTests
    {
        /// <summary>
        /// Verifies idempotency for all sample files.
        /// </summary>
        public void TestIdempotency(bool unused)
        {
            string[] files = new[] {
                @"C:\Users\LaffeyNyaa\Desktop\Repositories\LafnyaToolkit\Samples\OperatorBreakPolicy.cs",
                @"C:\Users\LaffeyNyaa\Desktop\Repositories\LafnyaToolkit\Samples\JsonFormatter.cs",
                @"C:\Users\LaffeyNyaa\Desktop\Repositories\LafnyaToolkit\Samples\ImportSorter.cs",
                @"C:\Users\LaffeyNyaa\Desktop\Repositories\LafnyaToolkit\Samples\ProgramBase.cs",
                @"C:\Users\LaffeyNyaa\Desktop\Repositories\LafnyaToolkit\Samples\LineLengthProcessor.cs"
            };

            foreach (var path in files)
            {
                string name = Path.GetFileName(path);
                string current = FileIO.ReadAllTextAutoDetect(path);

                // Pass 1: format the original file (expected to change)
                string next = Formatter.Instance.Format(current, "");
                bool changed = !string.Equals(current, next,
                    StringComparison.Ordinal);
                Console.WriteLine(name + ": pass 1 changed=" + changed +
                    " length=" + next.Length);

                current = next;

                // Passes 2-5: verify stability (idempotency)
                for (int i = 2; i <= 5; i++)
                {
                    next = Formatter.Instance.Format(current, "");
                    changed = !string.Equals(current, next,
                        StringComparison.Ordinal);
                    Console.WriteLine(name + ": pass " + i +
                        " changed=" + changed + " length=" + next.Length);

                    if (changed)
                    {
                        // Save diagnostic outputs
                        string dir = @"C:\Users\LaffeyNyaa\Desktop\Repositories\LafnyaToolkit\Samples_pass" +
                            (i - 1);

                        System.IO.Directory.CreateDirectory(dir);
                        System.IO.File.WriteAllText(
                            System.IO.Path.Combine(dir, name), current);
                        System.IO.File.WriteAllText(
                            System.IO.Path.Combine(dir, "pass" + i + "_" + name),
                            next);

                        throw new TestFailureException(
                            name + " changed on pass " + i);
                    }

                    current = next;
                }
            }
        }
    }
}
