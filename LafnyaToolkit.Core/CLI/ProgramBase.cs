using System;
using System.Collections.Generic;
using System.IO;

using LafnyaToolkit.Core.IO;

namespace LafnyaToolkit.Core.CLI
{
    /// <summary>
    /// Abstract base for the CLI entry point of each formatter. Encapsulates
    /// the shared scaffolding: argument validation, recursive file
    /// discovery (skipping <c>build</c> directories and any
    /// formatter-specific excluded directories), per-file processing with
    /// atomic write, error handling, and the summary print.
    /// </summary>
    public abstract class ProgramBase
    {
        /// <summary>
        /// Result of processing a single file: whether it was reformatted,
        /// skipped (already formatted), or failed.
        /// </summary>
        protected enum ProcessFileResult
        {
            /// <summary>The file was modified.</summary>
            Formatted,

            /// <summary>The file was already formatted.</summary>
            Skipped,

            /// <summary>An error occurred while processing the file.</summary>
            Failed
        }

        /// <summary>
        /// File-name suffixes (without the dot) that this formatter
        /// recognizes. For example, the C++ formatter returns
        /// <c>{ "cpp", "cc", "cxx", "hpp", "hh", "hxx", "h" }</c>.
        /// </summary>
        protected abstract IReadOnlyList<string> FileExtensions
        {
            get;
        }

        /// <summary>
        /// Optional directory names to exclude from recursive discovery, in
        /// addition to the universally-excluded "build" directory.
        /// </summary>
        protected virtual IReadOnlyList<string> ExcludedDirectoryNames =>
            Array.Empty<string>();

        /// <summary>
        /// The human-readable name of the language, used in summary output.
        /// </summary>
        protected abstract string LanguageName
        {
            get;
        }

        /// <summary>
        /// Formats a single file's source. Concrete subclasses implement the
        /// per-language pipeline.
        /// </summary>
        /// <param name="source">The original file content.</param>
        /// <param name="filePath">The full path of the file (for diagnostics).</param>
        /// <returns>The formatted source.</returns>
        protected abstract string FormatPipeline(string source,
            string filePath);

        /// <summary>
        /// Program entry point. Validates the target-directory argument,
        /// discovers files, processes them, and prints the summary.
        /// </summary>
        /// <param name="args">Command-line arguments; args[0] should be the target directory path.</param>
        public void Run(string[] args)
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

            var files = DiscoverFiles(targetPath);
            int formatted = 0;
            int skipped = 0;
            int failed = 0;

            foreach (var file in files)
            {
                ProcessFileResult result = ProcessFile(file, targetPath);

                switch (result)
                {
                    case ProcessFileResult.Formatted: formatted++; break;
                    case ProcessFileResult.Skipped: skipped++; break;
                    case ProcessFileResult.Failed: failed++; break;
                }
            }

            PrintSummary(
                formatted,
                skipped,
                failed
            );
        }

        /// <summary>
        /// Reads, formats, compares, and writes a single file. The file is
        /// only rewritten if the formatted content differs from the original;
        /// the rewrite is atomic via <see cref="FileIO.WriteFileAtomic"/>.
        /// </summary>
        /// <param name="file">The full path to the file.</param>
        /// <param name="root">The root directory used for computing the relative path.</param>
        /// <returns>The processing result.</returns>
        private ProcessFileResult ProcessFile(string file, string root)
        {
            string relative = GetRelativePath(root, file);

            try
            {
                string original = FileIO.ReadAllTextAutoDetect(file);
                string formatted = FormatPipeline(original, file);

                if (!string.Equals(original, formatted,
                    StringComparison.Ordinal))
                {
                    FileIO.WriteFileAtomic(
                        file,
                        formatted,
                        FileIO.Utf8NoBom
                    );
                    Console.WriteLine("Formatting: " + relative);
                    return ProcessFileResult.Formatted;
                }

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
        /// Prints the final summary line with totals.
        /// </summary>
        private void PrintSummary(
            int formatted,
            int skipped,
            int failed
        )
        {
            int total = formatted + skipped + failed;

            Console.WriteLine("Total: " + total + ", Formatted: " + formatted +
                ", Skipped: " + skipped + ", Failed: " + failed);
        }

        /// <summary>
        /// Recursively discovers all files with the configured extensions
        /// under the target directory, sorted alphabetically. Inaccessible
        /// subdirectories are skipped with a warning to stderr. The
        /// universally-excluded "build" directory and any
        /// formatter-specifically-excluded directories are pruned.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <returns>A sorted list of full paths.</returns>
        private List<string> DiscoverFiles(string root)
        {
            var files = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<string>();
            stack.Push(root);

            var excludedNames = new HashSet<string>
                (StringComparer.OrdinalIgnoreCase) { "build" };

            foreach (var name in ExcludedDirectoryNames)
            {
                excludedNames.Add(name);
            }

            while (stack.Count > 0)
            {
                string current = stack.Pop();

                foreach (var ext in FileExtensions)
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
                    string dirName = Path.GetFileName(dir);

                    if (excludedNames.Contains(dirName))
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
        /// Computes the relative path of <paramref name="file"/> with
        /// respect to <paramref name="root"/>, using the system directory
        /// separator.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <param name="file">The full file path.</param>
        /// <returns>The relative path.</returns>
        private static string GetRelativePath(string root, string file)
        {
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

            string rootWithSep = normalizedRoot + Path.DirectorySeparatorChar;

            if (file.StartsWith(rootWithSep,
                StringComparison.OrdinalIgnoreCase))
            {
                return file.Substring(rootWithSep.Length);
            }

            return file;
        }
    }
}
