using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
            int failedCount = 0;
            Encoding utf8NoBom = new UTF8Encoding(false);

            foreach (var file in files)
            {
                string relative = GetRelativePath(targetPath, file);

                try
                {
                    byte[] rawBytes = File.ReadAllBytes(file);
                    var (enc, bomLen) = FileIO.DetectEncoding(rawBytes);

                    string original = enc.GetString(rawBytes, bomLen,
                        rawBytes.Length - bomLen);

                    string formatted = Formatter.Format(original);

                    if (!string.Equals(original, formatted,
                        StringComparison.Ordinal))
                    {
                        FileIO.WriteFileAtomic(file, formatted, utf8NoBom);
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

            PrintSummary(formattedCount, skippedCount, failedCount);
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
        /// Recursively discovers all .gd files under the target directory,
        /// sorted alphabetically (OrdinalIgnoreCase). Directories named "addons"
        /// (case-insensitive) are excluded. Inaccessible subdirectories are skipped
        /// with a warning to stderr.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <returns>A sorted list of full paths to .gd files.</returns>
        private static List<string> DiscoverGdFiles(string root)
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
                    currentFiles = Directory.GetFiles(current, "*.gd",
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
                    string dirName = Path.GetFileName(dir);

                    if (!string.Equals(dirName, "addons",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        stack.Push(dir);
                    }
                }
            }

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
