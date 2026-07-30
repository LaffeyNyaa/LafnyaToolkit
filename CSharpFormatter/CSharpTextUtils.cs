using System.Collections.Generic;
using System.Text;

using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace CSharpFormatter
{
    /// <summary>
    /// C#-specific text utilities. Provides the Allman-style brace
    /// move (move a trailing open brace to its own line) and the
    /// tab-to-spaces replacement restricted to code regions.
    /// </summary>
    internal sealed class CSharpTextUtils
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly CSharpTextUtils Instance = new CSharpTextUtils();
        private CSharpTextUtils()
        {
        }

        /// <summary>
        /// Replaces tab characters with four spaces, but only inside
        /// Code tokens. Tabs inside string literals, verbatim strings,
        /// and comments are preserved.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with code-region tabs expanded.</returns>
        public string ReplaceTabsInCode(string text)
        {
            var tokens = CSharpTokenizer.Instance.Tokenize(text);
            var sb = new StringBuilder(text.Length);

            foreach (var token in tokens)
            {
                if (token.Kind == TokenKind.Code)
                {
                    sb.Append(token.Text.Replace("\t", "    "));
                }
                else
                {
                    sb.Append(token.Text);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Moves a trailing open brace from the end of a line to its
        /// own line (Allman style). Only braces in code regions are
        /// moved; braces inside comments or strings are left untouched.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with code-region trailing braces moved to their own lines.</returns>
        public string MoveOpenBraceToOwnLine(string text)
        {
            var tokens = CSharpTokenizer.Instance.Tokenize(text);

            bool[] isCode = CSharpTokenizer.Instance.BuildCodeMask(text,
                tokens);

            string[] lines = text.Split('\n');
            var result = new List<string>(lines.Length + 16);
            int lineStart = 0;

            foreach (var line in lines)
            {
                string trimmedEnd = line.TrimEnd();

                if (trimmedEnd.Length > 1 &&
                    trimmedEnd[trimmedEnd.Length - 1] == '{')
                {
                    int bracePos = lineStart + trimmedEnd.Length - 1;

                    if (bracePos < isCode.Length && isCode[bracePos])
                    {
                        string beforeBrace = trimmedEnd.Substring(0,
                            trimmedEnd.Length - 1).TrimEnd();

                        if (beforeBrace.Length > 0)
                        {
                            result.Add(beforeBrace);
                            result.Add("{");
                        }
                        else
                        {
                            result.Add(line);
                        }
                    }
                    else
                    {
                        result.Add(line);
                    }
                }
                else
                {
                    result.Add(line);
                }

                lineStart += line.Length + 1;
            }

            return string.Join("\n", result);
        }

        /// <summary>
        /// Finds the next open brace <c>{</c> in a code region, stopping
        /// at a semicolon.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="start">The starting position.</param>
        /// <returns>The position of the brace, or -1 if not found.</returns>
        public int FindOpenBrace(string text, bool[] isCode, int start)
        {
            int i = start;

            while (i < text.Length)
            {
                if (isCode[i] && text[i] == '{')
                {
                    return i;
                }

                if (text[i] == ';')
                {
                    return -1;
                }

                i++;
            }

            return -1;
        }

        /// <summary>
        /// Finds the close brace <c>}</c> that matches the open brace at
        /// <paramref name="openPos"/>.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="openPos">The position of the open brace.</param>
        /// <returns>The position of the matching close brace, or -1.</returns>
        public int FindMatchingClose(string text, bool[] isCode, int openPos)
        {
            int depth = 1;
            int i = openPos + 1;

            while (i < text.Length)
            {
                if (isCode[i])
                {
                    if (text[i] == '{')
                    {
                        depth++;
                    }
                    else if (text[i] == '}')
                    {
                        depth--;

                        if (depth == 0)
                        {
                            return i;
                        }
                    }
                }

                i++;
            }

            return -1;
        }
    }
}
