using System;
using System.Collections.Generic;

using LafnyaToolkit.Core.CLI;

namespace CppFormatter
{
    /// <summary>
    /// Tool entry point: derives from
    /// <see cref="ProgramBase"/>, supplies C++ file extensions, and
    /// delegates the per-file pipeline to
    /// <see cref="Formatter.Instance"/>.
    /// </summary>
    public class Program : ProgramBase
    {
        private static readonly string[] CppExtensions =
            {
            ".cpp", ".cc", ".cxx", ".hpp", ".hh", ".hxx", ".h"
        };

        /// <inheritdoc />
        protected override IReadOnlyList<string> FileExtensions =>
            CppExtensions;

        /// <inheritdoc />
        protected override string LanguageName => "C++";

        /// <inheritdoc />
        protected override string FormatPipeline(string source, string filePath)
        {
            return Formatter.Instance.Format(source);
        }

        /// <summary>
        /// Program entry point. Constructs the <see cref="Program"/>
        /// and runs the shared CLI scaffolding.
        /// </summary>
        /// <param name="args">Command-line arguments; args[0] is the target directory.</param>
        public static void Main(string[] args)
        {
            new Program().Run(args);
        }
    }
}
