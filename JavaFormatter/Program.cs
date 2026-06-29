using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JavaFormatter
{
    /// <summary>
    /// Tool entry point: parses command-line arguments, recursively discovers .java files,
    /// invokes the formatter, and prints progress and summary.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">Command-line arguments; args[0] should be the target directory path.</param>
        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Error: missing target directory argument.");
                Environment.Exit(2);
                return;
            }

            string targetPath = args[0];

            if (!Directory.Exists(targetPath))
            {
                Console.Error.WriteLine("Error: path does not exist or is not a directory: " +
                    targetPath);

                Environment.Exit(2);
                return;
            }

            var files = DiscoverJavaFiles(targetPath);
            int formattedCount = 0;
            int skippedCount = 0;
            int failedCount = 0;

            foreach (var file in files)
            {
                ProcessFile(file, targetPath, ref formattedCount,
                    ref skippedCount, ref failedCount);
            }

            PrintSummary(formattedCount, skippedCount, failedCount);
        }

        /// <summary>
        /// Processes a single .java file: reads its content, invokes the formatter,
        /// writes the formatted result back when it differs, and updates the counters.
        /// </summary>
        /// <param name="file">The full path of the file to process.</param>
        /// <param name="targetPath">The target directory root used for relative path computation.</param>
        /// <param name="formattedCount">Counter incremented when a file is reformatted.</param>
        /// <param name="skippedCount">Counter incremented when a file is left unchanged.</param>
        /// <param name="failedCount">Counter incremented when processing raises an exception.</param>
        private static void ProcessFile(string file, string targetPath,
            ref int formattedCount, ref int skippedCount, ref int failedCount)
        {
            string relative = GetRelativePath(targetPath, file);

            try
            {
                string original = File.ReadAllText(file, Encoding.UTF8);
                string formatted = Formatter.Format(original, targetPath);

                if (!string.Equals(original, formatted,
                    StringComparison.Ordinal))
                {
                    File.WriteAllText(file, formatted, new UTF8Encoding(false));
                    Console.WriteLine("Formatting: " + relative);
                    formattedCount++;
                } else
                {
                    Console.WriteLine("Skipped: " + relative);
                    skippedCount++;
                }
            } catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + relative + ": " +
                    ex.Message);

                failedCount++;
            }
        }

        /// <summary>
        /// Prints the processing summary, including total, formatted, skipped, and failed counts.
        /// </summary>
        /// <param name="formattedCount">The number of files reformatted.</param>
        /// <param name="skippedCount">The number of files left unchanged.</param>
        /// <param name="failedCount">The number of files that failed to process.</param>
        private static void PrintSummary(int formattedCount, int skippedCount,
            int failedCount)
        {
            int total = formattedCount + skippedCount + failedCount;

            Console.WriteLine("Total: " + total + ", Formatted: " +
                formattedCount + ", Skipped: " + skippedCount + ", Failed: " +
                failedCount);
        }

        /// <summary>
        /// Recursively discovers all .java files in the target directory, sorted alphabetically.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <returns>The sorted list of full paths to .java files.</returns>
        private static List<string> DiscoverJavaFiles(string root)
        {
            var files = new List<string>(Directory.EnumerateFiles(root,
                "*.java", SearchOption.AllDirectories));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        /// <summary>
        /// Computes the relative path of a file with respect to the root directory.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <param name="file">The full file path.</param>
        /// <returns>The relative path.</returns>
        private static string GetRelativePath(string root, string file)
        {
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

            string rootWithSep = normalizedRoot + Path.DirectorySeparatorChar;

            if (file.StartsWith(rootWithSep,
                StringComparison.OrdinalIgnoreCase))
            {
                return file.Substring(rootWithSep.Length);
            }

            return file;
        }
    }
}
