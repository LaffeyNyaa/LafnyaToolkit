using System.Collections.Generic;
using System.Text;

namespace CSharpFormatter
{
    /// <summary>
    /// Expands single-line enum declarations into multi-line form with
    /// each member on its own line.
    /// </summary>
    internal static class EnumFormatter
    {
        /// <summary>
        /// Finds all enum declarations and expands their members
        /// onto separate lines.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with enum members expanded.</returns>
        public static string FormatEnums(string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);

            var replacements =
                new List<TextUtils.Replacement>();

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                if (i > 0 && TextUtils.IsWordChar(text[i - 1]))
                {
                    continue;
                }

                if (!TextUtils.MatchesWord(text, i, "enum"))
                {
                    continue;
                }

                if (i + 4 < text.Length &&
                    TextUtils.IsWordChar(text[i + 4]))
                {
                    continue;
                }

                int braceStart = TextUtils.FindOpenBrace(text, isCode,
                    i + 4);

                if (braceStart < 0)
                {
                    continue;
                }

                int braceEnd = TextUtils.FindMatchingClose(text, isCode,
                    braceStart);

                if (braceEnd < 0)
                {
                    continue;
                }

                string content = text.Substring(braceStart + 1,
                    braceEnd - braceStart - 1);

                if (content.IndexOf('\n') >= 0)
                {
                    continue;
                }

                var members = SplitEnumMembers(content);

                if (members.Count == 0)
                {
                    continue;
                }

                var sb = new StringBuilder();
                sb.Append('\n');

                for (int k = 0; k < members.Count; k++)
                {
                    sb.Append(new string(' ', TextUtils.IndentSize));
                    sb.Append(members[k].Trim());

                    if (k < members.Count - 1)
                    {
                        sb.Append(',');
                    }

                    sb.Append('\n');
                }

                replacements.Add(new TextUtils.Replacement(braceStart + 1,
                    braceEnd, sb.ToString()));
            }

            return TextUtils.ApplyReplacements(text, replacements);
        }

        /// <summary>
        /// Splits enum member content by top-level commas, respecting
        /// nested brackets and parentheses.
        /// </summary>
        /// <param name="content">The content between enum braces.
        /// </param>
        /// <returns>A list of trimmed member strings.</returns>
        private static List<string> SplitEnumMembers(string content)
        {
            var members = new List<string>();
            var sb = new StringBuilder();
            int depth = 0;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];

                if (c == '(' || c == '[' || c == '{')
                {
                    depth++;
                    sb.Append(c);
                    continue;
                }

                if (c == ')' || c == ']' || c == '}')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }

                    sb.Append(c);
                    continue;
                }

                if (c == ',' && depth == 0)
                {
                    string m = sb.ToString().Trim();

                    if (m.Length > 0)
                    {
                        members.Add(m);
                    }

                    sb.Clear();
                    continue;
                }

                sb.Append(c);
            }

            string last = sb.ToString().Trim();

            if (last.Length > 0)
            {
                members.Add(last);
            }

            return members;
        }
    }
}
