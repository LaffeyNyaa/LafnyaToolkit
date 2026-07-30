using System.Collections.Generic;
using System.Text;

using LafnyaToolkit.Core.Tokenization;

namespace GDScriptFormatter
{
    /// <summary>
    /// Expands single-line enum declarations into multi-line form
    /// with each member on its own line and a trailing comma after
    /// the last member. Multi-line enum bodies are left unchanged.
    /// </summary>
    public sealed class EnumFormatter
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly EnumFormatter Instance = new EnumFormatter();
        private EnumFormatter()
        {
        }

        /// <summary>
        /// Expands a single-line enum so each member occupies its own
        /// line, with a trailing comma after the last member.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with single-line enum bodies expanded.</returns>
        public string ExpandEnums(string text)
        {
            var tokens = GDScriptTokenizer.Instance.Tokenize(text);

            bool[] isCode = GDScriptTokenizer.Instance.BuildCodeMask(text,
                tokens);

            var replacements = new List<Replacement>();

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                if (i > 0 &&
                    LafnyaToolkit.Core.Text.TextUtils.IsWordChar(text[i - 1]))
                {
                    continue;
                }

                if (!LafnyaToolkit.Core.Text.TextUtils.MatchesWord(text, i,
                    "enum"))
                {
                    continue;
                }

                int afterEnum = i + 4;

                if (afterEnum < text.Length &&
                    LafnyaToolkit.Core.Text.TextUtils.IsWordChar(text[afterEnum]))
                {
                    continue;
                }

                int braceStart = FindOpenBrace(text, isCode, afterEnum);

                if (braceStart < 0)
                {
                    continue;
                }

                int braceEnd = FindMatchingClose(text, isCode, braceStart);

                if (braceEnd < 0)
                {
                    continue;
                }

                string content = text.Substring(braceStart + 1,
                    braceEnd - braceStart - 1);

                var members = SplitEnumMembers(content);

                if (members.Count == 0)
                {
                    continue;
                }

                var sb = new StringBuilder();
                sb.Append('\n');

                for (int k = 0; k < members.Count; k++)
                {
                    sb.Append(new string(' ', GDScriptTextUtils.IndentSize));
                    sb.Append(members[k].Trim());
                    sb.Append(',');
                    sb.Append('\n');
                }

                replacements.Add(new Replacement(braceStart + 1, braceEnd,
                    sb.ToString()));
            }

            return LafnyaToolkit.Core.Text.TextUtils.ApplyReplacements(text,
                replacements);
        }

        /// <summary>
        /// Splits enum members by top-level commas (tracking bracket
        /// depth).
        /// </summary>
        /// <param name="content">The text between the enum braces.</param>
        /// <returns>The list of trimmed member strings.</returns>
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

        /// <summary>
        /// Finds the first { in code regions starting from the given
        /// position.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask of the text.</param>
        /// <param name="start">The starting position.</param>
        /// <returns>The index of the open brace, or -1 if not found.</returns>
        private static int FindOpenBrace(string text, bool[] isCode, int start)
        {
            int i = start;

            while (i < text.Length)
            {
                if (isCode[i] && text[i] == '{')
                {
                    return i;
                }

                i++;
            }

            return -1;
        }

        /// <summary>
        /// Finds the } that matches the { at openPos.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask of the text.</param>
        /// <param name="openPos">The position of the open brace.</param>
        /// <returns>The index of the matching close brace, or -1 if unbalanced.</returns>
        private static int FindMatchingClose(string text, bool[] isCode,
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
    }
}
