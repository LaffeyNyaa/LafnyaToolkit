using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Computes whether each line should preserve its original leading whitespace.
    /// Lines fully inside a VerbatimString or MultiLineComment token (but not
    /// the first line of such a token) preserve their original leading whitespace
    /// to avoid damaging string/comment content.
    /// </summary>
    internal static class PreserveIndentComputer
    {
        /// <summary>
        /// Computes whether each line should preserve its original leading whitespace.
        /// </summary>
        internal static bool[] Compute(List<string> lines, List<Token> tokens)
        {
            var preserveIndent = new bool[lines.Count];
            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

            int tokenPos = 0;

            foreach (var token in tokens)
            {
                int tokenStart = tokenPos;
                int tokenEnd = tokenPos + token.Text.Length;

                if (token.Kind == TokenKind.VerbatimString ||
                    token.Kind == TokenKind.MultiLineComment)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lineStarts[i] > tokenStart &&
                            lineStarts[i] < tokenEnd)
                        {
                            preserveIndent[i] = true;
                        }
                    }
                }

                tokenPos = tokenEnd;
            }

            return preserveIndent;
        }
    }
}
