using System.Collections.Generic;
using System.Text;

namespace LafnyaToolkit.Core.Tokenization
{
    /// <summary>
    /// Abstract base class for language-specific tokenizers. Provides the
    /// shared tokenize/reconstruct/mask pipeline; derived classes implement
    /// only the language-specific character-by-character scanner
    /// (<see cref="ScanNextToken"/>) and string-prefix detection.
    /// </summary>
    public abstract class TokenizerBase
    {
        /// <summary>
        /// Tokenizes the source into a list of tokens, each preserving its
        /// original text. Adjacent non-code regions (strings, comments,
        /// preprocessor) are emitted as separate tokens; runs of code
        /// characters between them are coalesced into single Code tokens.
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <returns>The list of tokens in source order.</returns>
        public List<Token> Tokenize(string source)
        {
            var tokens = new List<Token>();
            var code = new StringBuilder();
            int i = 0;
            int n = source.Length;

            while (i < n)
            {
                int consumed = ScanNextToken(source, i, out Token token);

                if (consumed <= 0)
                {
                    code.Append(source[i]);
                    i++;
                    continue;
                }

                if (code.Length > 0)
                {
                    FlushCode(tokens, code, i - code.Length);
                }

                tokens.Add(token);
                i += consumed;
            }

            FlushCode(tokens, code, n - code.Length);
            return tokens;
        }

        /// <summary>
        /// Reconstructs the token list back into a string. The result
        /// concatenates each token's original text and should equal the
        /// source the tokens were produced from.
        /// </summary>
        /// <param name="tokens">The token list.</param>
        /// <returns>The reconstructed string.</returns>
        public string Reconstruct(List<Token> tokens)
        {
            var sb = new StringBuilder();

            foreach (var t in tokens)
            {
                sb.Append(t.Text);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Constructs a boolean array marking whether each character position
        /// belongs to a Code token, given the source text and the token list.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="tokens">The token list produced by <see cref="Tokenize"/>.</param>
        /// <returns>A boolean array; true means the position is a Code token character.</returns>
        public bool[] BuildCodeMask(string text, List<Token> tokens)
        {
            var mask = new bool[text.Length];
            int pos = 0;

            foreach (var t in tokens)
            {
                for (int j = 0; j < t.Text.Length; j++)
                {
                    if (pos + j < mask.Length)
                    {
                        mask[pos + j] = t.Kind == TokenKind.Code;
                    }
                }

                pos += t.Text.Length;
            }

            return mask;
        }

        /// <summary>
        /// Computes the starting character position of each line in the
        /// concatenated full text. Lines are separated by a single '\n'.
        /// </summary>
        /// <param name="lines">The list of lines.</param>
        /// <returns>An array where result[i] is the start position of lines[i].</returns>
        public int[] ComputeLineStarts(IList<string> lines)
        {
            var starts = new int[lines.Count];
            int pos = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                starts[i] = pos;
                pos += lines[i].Length;

                if (i < lines.Count - 1)
                {
                    pos++;
                }
            }

            return starts;
        }

        /// <summary>
        /// Scans the source starting at <paramref name="position"/> and
        /// returns either a recognized non-code token (string, comment,
        /// preprocessor) or zero to indicate "this character is ordinary
        /// code; the caller should accumulate it".
        /// </summary>
        /// <param name="source">The full source text.</param>
        /// <param name="position">The current character position.</param>
        /// <param name="token">When the return value is positive, the token to emit.</param>
        /// <returns>The number of characters consumed (positive), or zero if this character is ordinary code.</returns>
        protected abstract int ScanNextToken(string source, int position,
            out Token token);

        /// <summary>
        /// Flushes accumulated Code characters as a single Code token and
        /// clears the accumulator.
        /// </summary>
        /// <param name="tokens">The token list to append to.</param>
        /// <param name="code">The accumulator of Code characters.</param>
        /// <param name="start">The start position of the accumulated code in the source.</param>
        private void FlushCode(List<Token> tokens, StringBuilder code,
            int start)
        {
            if (code.Length > 0)
            {
                tokens.Add(new Token(TokenKind.Code, code.ToString(), start));
                code.Clear();
            }
        }
    }
}
