using System.Collections.Generic;

using LafnyaToolkit.Core.CLI;
using LafnyaToolkit.Core.Text;

namespace JavaFormatter
{
    /// <summary>
    /// Tool entry point: derives from <see cref="ProgramBase"/>,
    /// supplies the <c>.java</c> file extension, and delegates the
    /// per-file pipeline to <see cref="Formatter.Instance"/>.
    /// </summary>
    public sealed class Program : ProgramBase
    {
        private static readonly IReadOnlyList<string> JavaExtensions =
            new[] { ".java" };

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
            JavaExtensions;

        /// <inheritdoc />
        protected override string LanguageName => "Java";

        /// <inheritdoc />
        protected override string FormatPipeline(string source, string filePath)
        {
            string root = System.IO.Path.GetDirectoryName(filePath);
            string formatted = Formatter.Instance.Format(source, root);
            return TextUtils.EnsureSingleTrailingNewline(formatted);
        }
    }
}
