using System;
using System.Collections.Generic;
using System.IO;

namespace GDScriptFormatter
{
    /// <summary>
    /// Tool entry point: parses command-line arguments, recursively discovers .gd files, invokes the formatter, prints progress and summary.
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

            var files = DiscoverGdFiles(targetPath);
            int formattedCount = 0;
            int skippedCount = 0;

            foreach (var file in files)
            {
                string relative = GetRelativePath(targetPath, file);
                string original = File.ReadAllText(file);
                string formatted = Formatter.Format(original);

                if (!string.Equals(original, formatted,
                    StringComparison.Ordinal))
                {
                    File.WriteAllText(file, formatted);
                    Console.WriteLine("Formatting: " + relative);
                    formattedCount++;
                }

                else
                {
                    Console.WriteLine("Skipped: " + relative);
                    skippedCount++;
                }
            }

            int total = formattedCount + skippedCount;
            Console.WriteLine("Total: " + total + ", Formatted: " +
                formattedCount + ", Skipped: " + skippedCount);
        }

        /// <summary>
        /// Recursively discovers all .gd files under the target directory, sorted alphabetically.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <returns>A sorted list of full paths to .gd files.</returns>
        private static List<string> DiscoverGdFiles(string root)
        {
            var files = new List<string>(Directory.EnumerateFiles(root, "*.gd",
                SearchOption.AllDirectories));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        /// <summary>
        /// Computes the path of file relative to root, using backslash separators.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <param name="file">The full path of the file.</param>
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
