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

            var files = DiscoverCppFiles(targetPath);
            int formattedCount = 0;
            int skippedCount = 0;

            foreach (var file in files)
            {
                string relative = GetRelativePath(targetPath, file);
                string original = File.ReadAllText(file, Encoding.UTF8);
                string formatted = Formatter.Format(original);

                if (!string.Equals(original, formatted,
                    StringComparison.Ordinal))
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

            int total = formattedCount + skippedCount;
            Console.WriteLine("Total: " + total + ", Formatted: " +
                formattedCount + ", Skipped: " + skippedCount);
        }

        /// <summary>
        /// Recursively discovers all C++ source files (.cpp/.cc/.cxx/.hpp/.hh/.hxx/.h) under the target directory,
        /// sorted alphabetically.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <returns>A sorted list of full paths to C++ files.</returns>
        private static List<string> DiscoverCppFiles(string root)
        {
            var extensions = new[] { ".cpp", ".cc", ".cxx", ".hpp", ".hh", ".hxx", ".h" };
            var files = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ext in extensions)
            {
                foreach (var file in Directory.EnumerateFiles(root, "*" + ext,
                    SearchOption.AllDirectories))
                {
                    string fileExt = Path.GetExtension(file);
                    bool match = false;
                    foreach (var e in extensions)
                    {
                        if (string.Equals(fileExt, e,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            match = true;
                            break;
                        }
                    }

                    if (!match)
                    {
                        continue;
                    }

                    if (seen.Add(file))
                    {
                        files.Add(file);
                    }
                }
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        /// <summary>
        /// Computes the relative path of a file with respect to the root, using backslash separators.
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
