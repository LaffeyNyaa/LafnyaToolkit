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
                Console.Error.WriteLine("Error: path does not exist or is not a directory: " + targetPath);
                Environment.Exit(2);
                return;
            }

            var files = DiscoverJavaFiles(targetPath);
            int formattedCount = 0;
            int skippedCount = 0;
            int failedCount = 0;

            foreach (var file in files)
            {
                string relative = GetRelativePath(targetPath, file);

                try
                {
                    string original = File.ReadAllText(file, Encoding.UTF8);
                    string formatted = Formatter.Format(original, targetPath);

                    if (!string.Equals(original, formatted, StringComparison.Ordinal))
                    {
                        File.WriteAllText(file, formatted, new UTF8Encoding(false));
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
                    Console.Error.WriteLine("Error: " + relative + ": " + ex.Message);
                    failedCount++;
                }
            }

            int total = formattedCount + skippedCount + failedCount;
            Console.WriteLine("Total: " + total + ", Formatted: " + formattedCount + ", Skipped: " + skippedCount + ", Failed: " + failedCount);
        }

        /// <summary>
        /// Recursively discovers all .java files in the target directory, sorted alphabetically.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <returns>The sorted list of full paths to .java files.</returns>
        private static List<string> DiscoverJavaFiles(string root)
        {
            var files = new List<string>(Directory.EnumerateFiles(root, "*.java", SearchOption.AllDirectories));
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
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string rootWithSep = normalizedRoot + Path.DirectorySeparatorChar;

            if (file.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
            {
                return file.Substring(rootWithSep.Length);
            }

            return file;
        }
    }
}
