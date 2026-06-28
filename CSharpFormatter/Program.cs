using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CSharpFormatter
{
    /// <summary>
    /// Tool entry point: parses command-line arguments, recursively discovers
    /// .cs files, invokes the formatter, and prints progress and summary.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">Command-line arguments; args[0] must be the
        /// target directory path.</param>
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

            var files = DiscoverCsFiles(targetPath);
            int formattedCount = 0;
            int skippedCount = 0;
            int failedCount = 0;

            foreach (var file in files)
            {
                string relative = GetRelativePath(targetPath, file);

                try
                {
                    string rootNamespace =
                        UsingSorter.ResolveRootNamespace(file,
                        targetPath);
                    string original = File.ReadAllText(file, Encoding.UTF8);
                    string formatted = Formatter.Format(original,
                        rootNamespace);

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
        /// Recursively discovers all .cs files under the target directory,
        /// sorted alphabetically.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <returns>A sorted list of absolute paths to .cs files.</returns>
        private static List<string> DiscoverCsFiles(string root)
        {
            var files = new List<string>(Directory.EnumerateFiles(root, "*.cs",
                SearchOption.AllDirectories));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        /// <summary>
        /// Computes the relative path of <paramref name="file"/>
        /// with respect to <paramref name="root"/>, using backslash separators.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <param name="file">The absolute file path.</param>
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
