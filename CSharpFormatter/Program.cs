using System;
using System.Collections.Generic;
using System.IO;
using LafnyaToolkit.Core.CLI;
using LafnyaToolkit.Core.Text;

namespace CSharpFormatter
{
    /// <summary>
    /// Tool entry point: derives from <see cref="ProgramBase"/>,
    /// supplies the <c>.cs</c> file extensions, and delegates the
    /// per-file pipeline to <see cref="Formatter.Instance"/> after
    /// resolving the root namespace via
    /// <see cref="UsingSorter.ResolveRootNamespace"/>.
    /// </summary>
    public sealed class Program : ProgramBase
    {
        private static readonly IReadOnlyList<string> CsExtensions = new[] { ".cs" };

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
        protected override IReadOnlyList<string> FileExtensions => CsExtensions;

        /// <inheritdoc />
        protected override string LanguageName => "C#";

        /// <inheritdoc />
        protected override string FormatPipeline(string source, string filePath)
        {
            string rootNamespace = UsingSorter.Instance.ResolveRootNamespace(
                filePath, Path.GetDirectoryName(filePath));

            string formatted = Formatter.Instance.Format(source, rootNamespace);
            return TextUtils.EnsureSingleTrailingNewline(formatted);
        }
    }
}
