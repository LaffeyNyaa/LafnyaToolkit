using System;
using System.Collections.Generic;
using LafnyaToolkit.Core.CLI;

namespace GDScriptFormatter
{
    /// <summary>
    /// Tool entry point: delegates to <see cref="LafnyaToolkit.Core.CLI.ProgramBase"/>
    /// for argument validation, recursive file discovery, atomic writes, and
    /// summary printing; supplies the GDScript-specific file extensions and
    /// delegates formatting to <see cref="Formatter.Instance"/>.
    /// </summary>
    public class Program : ProgramBase
    {
        /// <summary>
        /// Program entry point. Forwards to the base class pipeline.
        /// </summary>
        /// <param name="args">Command-line arguments; args[0] should be the target directory path.</param>
        public static void Main(string[] args)
        {
            new Program().Run(args);
        }

        /// <summary>
        /// File extensions recognized by the GDScript formatter.
        /// </summary>
        protected override IReadOnlyList<string> FileExtensions => new[] { "gd" };

        /// <summary>
        /// GDScript-specific excluded directory names (Godot "addons" directory).
        /// </summary>
        protected override IReadOnlyList<string> ExcludedDirectoryNames => new[] { "addons" };

        /// <summary>
        /// Human-readable language name used in the summary.
        /// </summary>
        protected override string LanguageName => "GDScript";

        /// <summary>
        /// Runs the GDScript formatting pipeline on a single file's source.
        /// </summary>
        /// <param name="source">The original file content.</param>
        /// <param name="filePath">The full file path (for diagnostics).</param>
        /// <returns>The formatted source.</returns>
        protected override string FormatPipeline(string source, string filePath)
        {
            return Formatter.Instance.Format(source);
        }
    }
}
