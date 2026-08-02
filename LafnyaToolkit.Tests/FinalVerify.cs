using System;
using System.IO;

using CSharpFormatter;
using LafnyaToolkit.Core.IO;

using LafnyaToolkit.Tests;

namespace LafnyaToolkit.Tests.Idempotency
{
    /// <summary>
    /// Idempotency verification for every sample file found in the
    /// Samples directory. Asserts that Format(Format(x)) == Format(x)
    /// for each file.
    /// </summary>
    public sealed class FinalVerifyTests
    {
        /// <summary>
        /// Verifies idempotency for all sample files.
        /// </summary>
        public void TestIdempotency(bool unused)
        {
            string samplesDir =
                @"C:\Users\LaffeyNyaa\Desktop\Repositories\LafnyaToolkit\Samples";

            if (!Directory.Exists(samplesDir))
            {
                return;
            }

            string[] files = Directory.GetFiles(samplesDir, "*.cs");
            Array.Sort(files);

            foreach (var path in files)
            {
                string name = Path.GetFileName(path);
                string current = FileIO.ReadAllTextAutoDetect(path);

                string next = Formatter.Instance.Format(current, "");

                bool changed = !string.Equals(current, next,
                    StringComparison.Ordinal);

                Console.WriteLine(name + ": pass 1 changed=" + changed +
                    " length=" + next.Length);
                current = next;

                for (int i = 2; i <= 5; i++)
                {
                    next = Formatter.Instance.Format(current, "");

                    changed = !string.Equals(current, next,
                        StringComparison.Ordinal);

                    Console.WriteLine(name + ": pass " + i +
                        " changed=" + changed + " length=" + next.Length);

                    if (changed)
                    {
                        string dir = samplesDir + "_pass" + (i - 1);

                        System.IO.Directory.CreateDirectory(dir);

                        System.IO.File.WriteAllText(
                            System.IO.Path.Combine(dir, name), current);

                        System.IO.File.WriteAllText(
                            System.IO.Path.Combine(
                                dir,
                                "pass" + i + "_" + name

                            ),
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
