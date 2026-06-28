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
        /// Describes the outcome of processing a single source file.
        /// </summary>
        private enum FileProcessResult
        {
            /// <summary>
            /// The file was reformatted and its content changed.
            /// </summary>
            Formatted,
            /// <summary>
            /// The file content was already formatted and unchanged.
            /// </summary>
            Skipped,
            /// <summary>
            /// Processing the file raised an exception.
            /// </summary>
            Failed
        }

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
                var result = ProcessFile(file, targetPath, relative);
                switch (result)
                {
                    case FileProcessResult.Formatted:
                        formattedCount++;
                        break;
                    case FileProcessResult.Skipped:
                        skippedCount++;
                        break;
                    case FileProcessResult.Failed:
                        failedCount++;
                        break;
                }
            }

            PrintSummary(formattedCount, skippedCount, failedCount);
        }

        /// <summary>
        /// Processes a single .cs file: reads its content, resolves the
        /// root namespace, formats the source, and writes the result back
        /// when the formatted text differs from the original. Any
        /// exception raised during processing is reported to stderr and
        /// translated into a <see cref="FileProcessResult.Failed"/>
        /// result without re-throwing.
        /// </summary>
        /// <param name="file">The absolute path of the file to process.</param>
        /// <param name="targetPath">The root target directory supplied on
        /// the command line, used to resolve the root namespace.</param>
        /// <param name="relativePath">The path of <paramref name="file"/>
        /// relative to <paramref name="targetPath"/>, used for log
        /// output.</param>
        /// <returns>A <see cref="FileProcessResult"/> value indicating
        /// whether the file was formatted, skipped, or failed.</returns>
        private static FileProcessResult ProcessFile(
            string file,
            string targetPath,
            string relativePath)
        {
            try
            {
                string rootNamespace =
                    UsingSorter.ResolveRootNamespace(file, targetPath);
                string original = File.ReadAllText(file, Encoding.UTF8);
                string formatted = Formatter.Format(original, rootNamespace);

                if (!string.Equals(original, formatted,
                    StringComparison.Ordinal))
                {
                    File.WriteAllText(file, formatted,
                        new UTF8Encoding(false));
                    Console.WriteLine("Formatting: " + relativePath);
                    return FileProcessResult.Formatted;
                }

                Console.WriteLine("Skipped: " + relativePath);
                return FileProcessResult.Skipped;
            }

            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + relativePath + ": " +
                    ex.Message);
                return FileProcessResult.Failed;
            }
        }

        /// <summary>
        /// Prints the run summary line listing total, formatted, skipped,
        /// and failed file counts.
        /// </summary>
        /// <param name="formattedCount">Number of files that were
        /// reformatted.</param>
        /// <param name="skippedCount">Number of files left unchanged.</param>
        /// <param name="failedCount">Number of files that failed
        /// processing.</param>
        private static void PrintSummary(
            int formattedCount,
            int skippedCount,
            int failedCount)
        {
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
