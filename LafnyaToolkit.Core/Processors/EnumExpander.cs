using System.Collections.Generic;
using System.Text;

using LafnyaToolkit.Core.Text;

namespace LafnyaToolkit.Core.Processors
{
    /// <summary>
    /// Expands a single-line enum declaration into one constant per line.
    /// This is the shared algorithm used by the C++, C#, Java, and GDScript
    /// formatters; it lives in Core because the four near-identical copies
    /// differ only in line-ending convention (which the caller controls).
    /// </summary>
    public sealed class EnumExpander
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly EnumExpander Instance = new EnumExpander();
        private EnumExpander()
        {
        }

        /// <summary>
        /// Expands single-line enum bodies of the form
        /// <c>{ A, B, C = 2, D }</c> into one constant per line with the
        /// trailing comma added to every line. Multi-line enum bodies (i.e.
        /// those already containing newlines) are returned unchanged.
        /// </summary>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code-region mask from the tokenizer.</param>
        /// <returns>The text with single-line enum bodies expanded.</returns>
        public string Expand(string text, bool[] isCode)
        {
            var sb = new StringBuilder(text.Length);
            int i = 0;
            int n = text.Length;

            while (i < n)
            {
                int brace = FindEnumBrace(text, isCode, i);

                if (brace < 0)
                {
                    sb.Append(text, i, n - i);
                    break;
                }

                int closeBrace = FindMatchingBrace(text, isCode, brace);

                if (closeBrace < 0)
                {
                    sb.Append(text, i, n - i);
                    break;
                }

                int bodyStart = brace + 1;
                int bodyEnd = closeBrace;
                string body = text.Substring(bodyStart, bodyEnd - bodyStart);

                sb.Append(text, i, bodyStart - i);

                if (body.IndexOf('\n') < 0)
                {
                    sb.Append(ExpandSingleLineBody(body));
                }
                else
                {
                    sb.Append(body);
                }

                i = bodyEnd;
            }

            return sb.ToString();
        }

        private static int FindEnumBrace(string text, bool[] isCode, int start)
        {
            int i = start;

            while (i < text.Length)
            {
                if (isCode[i] && text[i] == '{')
                {
                    if (IsPrecededByEnumKeyword(text, isCode, i))
                    {
                        return i;
                    }
                }

                i++;
            }

            return -1;
        }

        private static bool IsPrecededByEnumKeyword(string text, bool[] isCode,
            int bracePos)
        {
            int j = bracePos - 1;

            while (j >= 0 && (text[j] == ' ' || text[j] == '\t'))
            {
                j--;
            }

            int end = j + 1;
            int len = 0;

            while (j >= 0 && TextUtils.IsWordChar(text[j]))
            {
                j--;
                len++;
            }

            if (len != 4)
            {
                return false;
            }

            return text.Substring(j + 1, len) == "enum";
        }

        private static int FindMatchingBrace(string text, bool[] isCode,
            int openPos)
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

        private static string ExpandSingleLineBody(string body)
        {
            var parts = SplitEnumParts(body);
            var sb = new StringBuilder();

            for (int k = 0; k < parts.Count; k++)
            {
                if (k > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(parts[k].Trim());

                if (k < parts.Count - 1)
                {
                    sb.Append(',');
                }
            }

            return sb.ToString();
        }

        private static List<string> SplitEnumParts(string body)
        {
            var parts = new List<string>();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < body.Length; i++)
            {
                char c = body[i];

                if (c == '(' || c == '{' || c == '[')
                {
                    depth++;
                }
                else if (c == ')' || c == '}' || c == ']')
                {
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    parts.Add(body.Substring(start, i - start));
                    start = i + 1;
                }
            }

            if (start < body.Length)
            {
                parts.Add(body.Substring(start));
            }

            return parts;
        }
    }
}
