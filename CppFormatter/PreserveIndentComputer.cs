using System.Collections.Generic;

using LafnyaToolkit.Core.Tokenization;

namespace CppFormatter
{
    /// <summary>
    /// Computes whether each line should preserve its original leading
    /// whitespace. Lines fully inside a VerbatimString or
    /// MultiLineComment token (but not the first line of such a token)
    /// preserve their original leading whitespace to avoid damaging
    /// string/comment content. Lines starting with the trailing doc
    /// comment marker <c>/**&lt;</c> also preserve their indent so
    /// that <see cref="LineLengthProcessor"/> splits them safely
    /// without non-idempotent re-indentation. Stateless; the shared
    /// instance is exposed via <see cref="Instance"/>.
    /// </summary>
    internal sealed class PreserveIndentComputer
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly PreserveIndentComputer Instance =
            new PreserveIndentComputer();

        private PreserveIndentComputer()
        {
        }

        /// <summary>
        /// Computes whether each line should preserve its original
        /// leading whitespace.
        /// </summary>
        public bool[] Compute(List<string> lines, List<Token> tokens)
        {
            var preserveIndent = new bool[lines.Count];
            int[] lineStarts = CppTokenizer.Instance.ComputeLineStarts(lines);

            int tokenPos = 0;

            foreach (var token in tokens)
            {
                int tokenStart = tokenPos;
                int tokenEnd = tokenPos + token.Text.Length;

                if (token.Kind == TokenKind.VerbatimString || token.Kind ==
                    TokenKind.MultiLineComment)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lineStarts[i] > tokenStart && lineStarts[i] <
                            tokenEnd)
                        {
                            preserveIndent[i] = true;
                        }
                    }
                }

                tokenPos = tokenEnd;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].TrimStart().StartsWith("/**<"))
                {
                    preserveIndent[i] = true;
                }
            }

            return preserveIndent;
        }
    }
}
