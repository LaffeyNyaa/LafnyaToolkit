using System.Collections.Generic;
using System.IO;

using LafnyaToolkit.Core.CLI;

namespace JsonFormatter
{
    /// <summary>
    /// Tool entry point: derives the shared CLI scaffolding from
    /// <see cref="LafnyaToolkit.Core.CLI.ProgramBase"/>, declares the
    /// ".json" file pattern, and instantiates the JSON formatter pipeline.
    /// </summary>
    public sealed class Program : LafnyaToolkit.Core.CLI.ProgramBase
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly Program Instance = new Program();

        private static readonly IReadOnlyList<string> JsonExtensions =
            new[] { ".json" };

        private Program()
        {
        }

        /// <summary>
        /// Program entry point. Delegates to <see cref="LafnyaToolkit.Core.CLI.ProgramBase.Run"/>
        /// on the shared instance.
        /// </summary>
        /// <param name="args">CLI arguments; args[0] should be the target directory path.</param>
        public static void Main(string[] args)
        {
            Instance.Run(args);
        }

        /// <inheritdoc />
        protected override IReadOnlyList<string> FileExtensions =>
            JsonExtensions;

        /// <inheritdoc />
        protected override string LanguageName => "JSON";

        /// <inheritdoc />
        protected override string FormatPipeline(string source, string filePath)
        {
            return JsonFormatter.Instance.Format(source);
        }
    }
}
