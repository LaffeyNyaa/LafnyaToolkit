using System.Collections.Generic;
using System.Text;

namespace GDScriptFormatter
{
    /// <summary>
    /// Expands single-line enum declarations into multi-line form with
    /// each member on its own line.
    /// </summary>
    internal static class EnumFormatter
    {
        /// <summary>
        /// Replacement entry: replaces [Start, End) with NewText.
        /// </summary>
        internal struct Replacement
        {
            /// <summary>The start position (inclusive).</summary>
            public int Start;
            /// <summary>The end position (exclusive).</summary>
            public int End;
            /// <summary>The replacement text.</summary>
            public string NewText;

            public Replacement(int start, int end, string newText)
            {
                Start = start;
                End = end;
                NewText = newText;
            }
        }

        /// <summary>
        /// Expands a single-line enum so each member occupies its own line, with a trailing comma after the last member.
        /// </summary>
        internal static string ExpandEnums(string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            var replacements = new List<Replacement>();

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

                int afterEnum = i + 4;

                if (afterEnum < text.Length && TextUtils.IsWordChar(text[afterEnum]))
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
                    sb.Append(new string(' ', TextUtils.IndentSize));
                    sb.Append(members[k].Trim());
                    sb.Append(',');
                    sb.Append('\n');
                }

                replacements.Add(new Replacement(braceStart + 1, braceEnd,
                    sb.ToString()));
            }

            return ApplyReplacements(text, replacements);
        }

        /// <summary>
        /// Splits enum members by top-level commas (tracking bracket depth).
        /// </summary>
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
        /// Applies a list of replacements to text (sorted by position, deduplicates overlaps).
        /// </summary>
        private static string ApplyReplacements(string text,
            List<Replacement> replacements)
        {
            if (replacements.Count == 0)
            {
                return text;
            }

            replacements.Sort((a, b) => a.Start.CompareTo(b.Start));
            var sb = new StringBuilder(text.Length);
            int pos = 0;

            foreach (var r in replacements)
            {
                if (r.Start < pos)
                {
                    continue;
                }

                sb.Append(text, pos, r.Start - pos);
                sb.Append(r.NewText);
                pos = r.End;
            }

            sb.Append(text, pos, text.Length - pos);
            return sb.ToString();
        }

        /// <summary>
        /// Finds the first { in code regions starting from the given position.
        /// </summary>
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
