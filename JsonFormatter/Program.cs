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
        /// UTF-8 encoding without BOM, reused across all file writes.
        /// </summary>
        private static readonly UTF8Encoding Utf8NoBom =
            new UTF8Encoding(false);

        /// <summary>
        /// Result of processing a single JSON file.
        /// </summary>
        private enum ProcessFileResult
        {
            Formatted,
            Skipped,
            Failed
        }

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
            int formatted = 0;
            int skipped = 0;
            int failed = 0;

            foreach (var file in files)
            {
                ProcessFileResult result = ProcessFile(file, targetPath);

                switch (result)
                {
                    case ProcessFileResult.Formatted:
                        formatted++;
                        break;
                    case ProcessFileResult.Skipped:
                        skipped++;
                        break;
                    case ProcessFileResult.Failed:
                        failed++;
                        break;
                }
            }

            PrintSummary(formatted, skipped, failed);
        }

        /// <summary>
        /// Reads, formats, compares, and optionally writes a single JSON file.
        /// Prints the per-file progress line and returns the processing result.
        /// </summary>
        /// <param name="file">The full path to the JSON file.</param>
        /// <param name="root">The root directory used for computing the relative path.</param>
        /// <returns>The processing result.</returns>
        private static ProcessFileResult ProcessFile(string file, string root)
        {
            string relative = GetRelativePath(root, file);

            try
            {
                string original = File.ReadAllText(file, Encoding.UTF8);
                string formatted = JsonFormatter.Format(original);

                if (!string.Equals(original, formatted,
                    StringComparison.Ordinal))
                {
                    File.WriteAllText(file, formatted, Utf8NoBom);
                    Console.WriteLine("Formatting: " + relative);
                    return ProcessFileResult.Formatted;
                }

                Console.WriteLine("Skipped: " + relative);
                return ProcessFileResult.Skipped;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + relative + ": " +
                    ex.Message);

                return ProcessFileResult.Failed;
            }
        }

        /// <summary>
        /// Prints the summary line: Total, Formatted, Skipped, Failed.
        /// </summary>
        private static void PrintSummary(int formatted, int skipped, int failed)
        {
            int total = formatted + skipped + failed;

            Console.WriteLine("Total: " + total + ", Formatted: " +
                formatted + ", Skipped: " + skipped + ", Failed: " + failed);
        }

        /// <summary>
        /// Recursively discovers all .json files under the target directory,
        /// sorted alphabetically (OrdinalIgnoreCase). Inaccessible subdirectories
        /// are skipped with a warning to stderr.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <returns>A sorted list of full paths to .json files.</returns>
        private static List<string> DiscoverJsonFiles(string root)
        {
            var files = new List<string>();
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                string current = stack.Pop();

                string[] currentFiles;

                try
                {
                    currentFiles = Directory.GetFiles(current, "*.json",
                        SearchOption.TopDirectoryOnly);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.Error.WriteLine("Warning: skipping inaccessible directory: " +
                        current + " (" + ex.Message + ")");

                    continue;
                }
                catch (PathTooLongException ex)
                {
                    Console.Error.WriteLine("Warning: skipping directory with path too long: " +
                        current + " (" + ex.Message + ")");

                    continue;
                }
                catch (DirectoryNotFoundException ex)
                {
                    Console.Error.WriteLine("Warning: skipping missing directory: " +
                        current + " (" + ex.Message + ")");

                    continue;
                }

                files.AddRange(currentFiles);

                string[] subdirs;

                try
                {
                    subdirs = Directory.GetDirectories(current, "*",
                        SearchOption.TopDirectoryOnly);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.Error.WriteLine("Warning: cannot enumerate subdirectories of: " +
                        current + " (" + ex.Message + ")");

                    continue;
                }
                catch (PathTooLongException ex)
                {
                    Console.Error.WriteLine("Warning: skipping directory with path too long: " +
                        current + " (" + ex.Message + ")");

                    continue;
                }
                catch (DirectoryNotFoundException ex)
                {
                    Console.Error.WriteLine("Warning: skipping missing directory: " +
                        current + " (" + ex.Message + ")");

                    continue;
                }

                foreach (string dir in subdirs)
                {
                    stack.Push(dir);
                }
            }

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
