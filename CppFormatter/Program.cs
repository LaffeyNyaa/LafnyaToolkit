using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CppFormatter
{
    /// <summary>
    /// Tool entry point: parses command-line arguments, recursively discovers C++ source files,
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
        /// Result of processing a single C++ file.
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
        /// <param name="args">Command-line arguments; args[0] should be the target directory path.</param>
        public static void Main(string[] args)
        {
            if (args == null || args.Length < 1)
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

            var files = DiscoverCppFiles(targetPath);
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
        /// Reads, formats, compares, and optionally writes a single C++ file.
        /// Prints the per-file progress line and returns the processing result.
        /// </summary>
        /// <param name="file">The full path to the C++ file.</param>
        /// <param name="root">The root directory used for computing the relative path.</param>
        /// <returns>The processing result.</returns>
        private static ProcessFileResult ProcessFile(string file, string root)
        {
            string relative = GetRelativePath(root, file);

            try
            {
                string original = File.ReadAllText(file, Encoding.UTF8);
                string formatted = Formatter.Format(original);

                if (!string.Equals(original, formatted,
                    StringComparison.Ordinal))
                {
                    string directory = Path.GetDirectoryName(file);

                    string tempPath = Path.Combine(directory,
                        Path.GetFileName(file) + ".tmp");

                    try
                    {
                        File.WriteAllText(tempPath, formatted, Utf8NoBom);

                        try
                        {
                            File.Replace(tempPath, file, null);
                        }
                        catch (Exception)
                        {
                            File.Delete(file);
                            File.Move(tempPath, file);
                        }
                    }
                    finally
                    {
                        if (File.Exists(tempPath))
                        {
                            try
                            {
                                File.Delete(tempPath);
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }

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
        /// Recursively discovers all C++ source files (.cpp/.cc/.cxx/.hpp/.hh/.hxx/.h) under the target directory,
        /// sorted alphabetically (OrdinalIgnoreCase). Inaccessible subdirectories are skipped with a warning to stderr.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <returns>A sorted list of full paths to C++ files.</returns>
        private static List<string> DiscoverCppFiles(string root)
        {
            var extensions = new[] { ".cpp", ".cc", ".cxx", ".hpp", ".hh",
                ".hxx", ".h" };
            var files = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                string current = stack.Pop();

                foreach (var ext in extensions)
                {
                    string[] currentFiles;

                    try
                    {
                        currentFiles = Directory.GetFiles(current, "*" + ext,
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

                    foreach (var file in currentFiles)
                    {
                        // Skip files under the "build" directory

                        if (file.IndexOf(Path.DirectorySeparatorChar + "build" +
                            Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            continue;
                        }

                        if (seen.Add(file))
                        {
                            files.Add(file);
                        }
                    }
                }

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
                    // Skip "build" directories
                    string dirName = Path.GetFileName(dir);

                    if (string.Equals(dirName, "build",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

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
