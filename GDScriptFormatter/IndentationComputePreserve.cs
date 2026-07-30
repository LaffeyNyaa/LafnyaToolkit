using System.Collections.Generic;
using LafnyaToolkit.Core.Tokenization;

namespace GDScriptFormatter
{
    /// <summary>
    /// Helpers that determine which lines must keep their original
    /// indentation (lines inside triple-quoted strings), plus
    /// line-indent and closing-bracket depth utilities. Kept in its
    /// own file so each file in the indentation pipeline stays under
    /// 600 lines.
    /// </summary>
    public sealed partial class IndentationProcessor
    {
        /// <summary>
        /// Determines whether each line is inside a triple-quoted
        /// string (non-first line), where original indentation must be
        /// preserved.
        /// </summary>
        /// <param name="lines">The input lines.</param>
        /// <param name="tokens">The tokenization of the joined text.</param>
        /// <returns>Per-line flag: true means the line is inside a triple-quoted string.</returns>
        public bool[] ComputePreserveIndent(List<string> lines,
            List<Token> tokens)
        {
            var preserveIndent = new bool[lines.Count];
            var lineStarts = ComputeLineStarts(lines);
            int tokenPos = 0;

            foreach (var token in tokens)
            {
                int tokenStart = tokenPos;
                int tokenEnd = tokenPos + token.Text.Length;

                if (token.Kind == TokenKind.VerbatimString)
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

        /// <summary>
        /// Computes the indentation level of a line (leading spaces /
        /// <see cref="GDScriptTextUtils.IndentSize"/>).
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <returns>The indent level.</returns>
        public int LineIndentLevel(string line)
        {
            int spaces = 0;

            while (spaces < line.Length && line[spaces] == ' ')
            {
                spaces++;
            }

            return spaces / GDScriptTextUtils.IndentSize;
        }
    }
}
