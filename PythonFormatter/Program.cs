using System.Collections.Generic;

using LafnyaToolkit.Core.CLI;
using LafnyaToolkit.Core.Text;

namespace PythonFormatter
{
    /// <summary>
    /// Tool entry point: derives from <see cref="ProgramBase"/>, supplies
    /// the <c>.py</c> and <c>.pyw</c> file extensions, and delegates the
    /// per-file pipeline to <see cref="Formatter.Instance"/>.
    /// </summary>
    public sealed class Program : ProgramBase
    {
        private static readonly IReadOnlyList<string> PythonExtensions =
            new[] { ".py", ".pyw" };

        private static readonly IReadOnlyList<string> PythonExcludedDirs =
            new[] { "venv", ".venv" };

        /// <summary>Shared stateless instance.</summary>
        public static readonly Program Instance = new Program();

        private Program()
        {
        }

        /// <summary>
        /// Program entry point. Delegates to <see cref="ProgramBase.Run"/>
        /// on the shared instance.
        /// </summary>
        /// <param name="args">CLI arguments; args[0] should be the target directory path.</param>
        public static void Main(string[] args)
        {
            Instance.Run(args);
        }

        /// <inheritdoc />
        protected override IReadOnlyList<string> FileExtensions =>
            PythonExtensions;

        /// <summary>
        /// Python virtual-environment directory names that should be
        /// excluded from recursive discovery, in addition to the
        /// universally-excluded <c>build</c> directory. <c>venv</c>
        /// and <c>.venv</c> are the conventional names for Python
        /// virtual environments; they contain third-party packages
        /// whose source is not meant to be reformatted by the project.
        /// </summary>
        protected override IReadOnlyList<string> ExcludedDirectoryNames =>
            PythonExcludedDirs;

        /// <inheritdoc />
        protected override string LanguageName => "Python";

        /// <inheritdoc />
        protected override string FormatPipeline(string source, string filePath)
        {
            string formatted = Formatter.Instance.Format(source);
            return TextUtils.EnsureSingleTrailingNewline(formatted);
        }
    }
}
