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
                    Console.Error.WriteLine("Error: failed to process " +
                        relative +
                        ": " + ex.Message);
                    failedCount++;
                }
            }

            int total = formattedCount + skippedCount + failedCount;

            string summary = "Total: " + total + ", Formatted: " +
                formattedCount + ", Skipped: " + skippedCount;

            if (failedCount > 0)
            {
                summary += ", Failed: " + failedCount;
            }

            Console.WriteLine(summary);
            Environment.Exit(failedCount);
        }

        /// <summary>
        /// Recursively discovers all .gd files under the target directory, sorted alphabetically.
        /// Directories named "addons" (case-insensitive) and their contents are excluded.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <returns>A sorted list of full paths to .gd files.</returns>
        private static List<string> DiscoverGdFiles(string root)
        {
            var files = new List<string>();
            var dirsToVisit = new Queue<string>();
            dirsToVisit.Enqueue(root);

            while (dirsToVisit.Count > 0)
            {
                string currentDir = dirsToVisit.Dequeue();

                try
                {
                    files.AddRange(Directory.EnumerateFiles(currentDir,
                        "*.gd"));
                }
                catch (UnauthorizedAccessException)
                {
                }

                try
                {
                    foreach (string subDir in Directory.EnumerateDirectories(currentDir))
                    {
                        string dirName = Path.GetFileName(subDir);

                        if (!string.Equals(dirName, "addons",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            dirsToVisit.Enqueue(subDir);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
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
