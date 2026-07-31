using System.Collections.Generic;

using LafnyaToolkit.Core.Tokenization;

namespace PythonFormatter
{
    /// <summary>
    /// Recomputes leading whitespace for each line. The processor
    /// normalizes tabs and mixed tabs/spaces to multiples of four
    /// spaces, preserving the relative indentation structure of the
    /// input. Lines inside a multi-line string (triple-quoted) or a
    /// single-line comment that is not the first non-whitespace text
    /// of a logical statement preserve their original leading
    /// whitespace verbatim.
    /// </summary>
    internal sealed class IndentationProcessor
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly IndentationProcessor Instance =
            new IndentationProcessor();

        private IndentationProcessor()
        {
        }

        /// <summary>
        /// Normalizes the leading whitespace of every line. Each line's
        /// leading run of tabs and spaces is replaced by a multiple of
        /// four spaces whose total visible width equals the original
        /// run's tab-expanded width (rounded down to the nearest
        /// multiple of 4). Lines entirely inside a multi-line string
        /// or comment preserve their original leading whitespace.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <param name="text">The full source text corresponding to
        /// <paramref name="lines"/>.</param>
        /// <returns>The re-indented lines.</returns>
        public List<string> Reindent(List<string> lines, string text)
        {
            var tokens = PythonTokenizer.Instance.Tokenize(text);

            bool[] isCode = PythonTokenizer.Instance.BuildCodeMask(text,
                tokens);

            var lineStarts = PythonTextUtils.Instance.ComputeLineStarts(lines);
            var preserveIndent = ComputePreserveIndent(lines, tokens, isCode);
            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                if (preserveIndent[i])
                {
                    result.Add(line);
                    continue;
                }

                string content = line.TrimStart();
                int leadLen = line.Length - content.Length;

                if (leadLen == 0)
                {
                    result.Add(line);
                    continue;
                }

                int width = ComputeTabExpandedWidth(line, leadLen);
                int normalized = (width / 4) * 4;
                string newLead = new string(' ', normalized);
                result.Add(newLead + content);
            }

            return result;
        }

        /// <summary>
        /// Computes the tab-expanded visible width of the first
        /// <paramref name="count"/> characters of <paramref name="line"/>,
        /// where each tab counts as 4 spaces. Used to translate mixed
        /// leading whitespace into a number of 4-space units.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="count">The number of leading characters to
        /// consider.</param>
        /// <returns>The visible width of the leading whitespace.</returns>
        private static int ComputeTabExpandedWidth(string line, int count)
        {
            int width = 0;

            for (int i = 0; i < count && i < line.Length; i++)
            {
                char c = line[i];

                if (c == '\t')
                {
                    width += 4;
                }
                else if (c == ' ')
                {
                    width += 1;
                }
                else
                {
                    break;
                }
            }

            return width;
        }

        /// <summary>
        /// Computes a per-line preserve-indent flag. A line is marked
        /// preserve when:
        /// <list type="bullet">
        ///   <item><description>it starts inside a triple-quoted (multi-line) string or comment token; or</description></item>
        ///   <item><description>it is an interior line of a multi-line string, i.e. its first non-whitespace character is in a non-Code token.</description></item>
        /// </list>
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <param name="tokens">The token list for the corresponding
        /// text.</param>
        /// <param name="isCode">The code mask of the corresponding
        /// text.</param>
        /// <returns>A boolean array indicating preserve-indent flags
        /// per line.</returns>
        private static bool[] ComputePreserveIndent(List<string> lines,
            List<Token> tokens, bool[] isCode)
        {
            var preserve = new bool[lines.Count];

            if (lines.Count == 0)
            {
                return preserve;
            }

            var lineStarts = PythonTextUtils.Instance.ComputeLineStarts(lines);

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                int firstNonWs = 0;

                while (firstNonWs < line.Length &&
                    (line[firstNonWs] == ' ' || line[firstNonWs] == '\t'))
                {
                    firstNonWs++;
                }

                if (firstNonWs >= line.Length)
                {
                    preserve[i] = false;
                    continue;
                }

                int textPos = lineStarts[i] + firstNonWs;

                if (textPos < 0 || textPos >= isCode.Length || !isCode[textPos])
                {
                    preserve[i] = true;
                }
            }

            return preserve;
        }
    }
}
