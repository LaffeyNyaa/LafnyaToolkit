using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JsonFormatter
{
    /// <summary>
    /// Tool entry point: parses CLI arguments, recursively discovers .json files,
    /// invokes the formatter, and prints progress and summary.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">CLI arguments; args[0] should be the target directory path.</param>
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

            var files = DiscoverJsonFiles(targetPath);
            int formattedCount = 0;
            int skippedCount = 0;
            int failedCount = 0;

            foreach (var file in files)
            {
                string relative = GetRelativePath(targetPath, file);

                try
                {
                    string original = File.ReadAllText(file, Encoding.UTF8);
                    string formatted = JsonFormatter.Format(original);

                    if (!string.Equals(original, formatted,
                        StringComparison.Ordinal))
                    {
                        File.WriteAllText(file, formatted,
                            new UTF8Encoding(false));
                        Console.WriteLine("Formatting: " + relative);
                        formattedCount++;
                    }

                    else
                    {
                        Console.WriteLine("Skipped: " + relative);
                        skippedCount++;
                    }
                }

                catch (Exception ex)
                {
                    Console.Error.WriteLine("Error: " + relative + ": " +
                        ex.Message);
                    failedCount++;
                }
            }

            int total = formattedCount + skippedCount + failedCount;
            Console.WriteLine("Total: " + total + ", Formatted: " +
                formattedCount + ", Skipped: " + skippedCount + ", Failed: " +
                failedCount);
        }

        /// <summary>
        /// Recursively discovers all .json files under the target directory,
        /// sorted alphabetically.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <returns>A sorted list of full paths to .json files.</returns>
        private static List<string> DiscoverJsonFiles(string root)
        {
            var files = new List<string>(Directory.EnumerateFiles(root, "*.json",
                SearchOption.AllDirectories));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        /// <summary>
        /// Computes the relative path of <paramref name="file"/> with respect to
        /// <paramref name="root"/>, using the system directory separator.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <param name="file">The full file path.</param>
        /// <returns>The relative path.</returns>
        private static string GetRelativePath(string root, string file)
        {
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            string normalizedFile = file;
            string rootWithSep = normalizedRoot + Path.DirectorySeparatorChar;

            if (normalizedFile.StartsWith(rootWithSep,
                StringComparison.OrdinalIgnoreCase))
            {
                return normalizedFile.Substring(rootWithSep.Length);
            }

            return normalizedFile;
        }
    }
}
