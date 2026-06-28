using System.Collections.Generic;
using System.Text;

namespace CSharpFormatter
{
    /// <summary>
    /// Expands single-line property accessor blocks into multi-line form,
    /// supporting recursive expansion of nested accessor blocks.
    /// </summary>
    internal static class PropertyFormatter
    {
        /// <summary>
        /// Expands single-line property accessor blocks into multi-line
        /// form. Supports recursive expansion of nested accessor blocks
        /// (e.g., get { return x; } set { y = value; }).
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <returns>The text with property accessors expanded.</returns>
        public static string FormatPropertyAccessors(string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            var replacements =
                new List<TextUtils.Replacement>();

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i] || text[i] != '{')
                {
                    continue;
                }

                int braceEnd = FindSingleLineBraceEnd(text, isCode, i);

                if (braceEnd < 0)
                {
                    continue;
                }

                string content = text.Substring(i + 1, braceEnd - i - 1);
                if (!IsAccessorContent(content))
                {
                    continue;
                }

                string replacement = ExpandAccessors(content, 1);
                replacements.Add(new TextUtils.Replacement(i + 1, braceEnd,
                    replacement));
            }

            return TextUtils.ApplyReplacements(text, replacements);
        }

        /// <summary>
        /// Finds the closing brace of a single-line block starting at
        /// <paramref name="openPos"/>. Returns -1 if the block spans
        /// multiple lines.
        /// </summary>
        private static int FindSingleLineBraceEnd(string text,
            bool[] isCode, int openPos)
        {
            int depth = 1;
            int j = openPos + 1;
            while (j < text.Length)
            {
                if (text[j] == '\n')
                {
                    return -1;
                }

                if (isCode[j])
                {
                    if (text[j] == '{')
                    {
                        depth++;
                    }

                    else if (text[j] == '}')
                    {
                        depth--;

                        if (depth == 0)
                        {
                            return j;
                        }
                    }
                }

                j++;
            }

            return -1;
        }

        /// <summary>
        /// Recursively expands accessor content into multi-line form. Each
        /// accessor of the form keyword { block } is expanded into keyword,
        /// {, the block content (recursively), }; accessors of the form
        /// keyword; or keyword => expr; remain single-line.
        /// </summary>
        /// <param name="content">The accessor block content.</param>
        /// <param name="indentLevel">The current indentation level.</param>
        /// <returns>The expanded multi-line text.</returns>
        private static string ExpandAccessors(string content,
            int indentLevel)
        {
            var sb = new StringBuilder();
            sb.Append('\n');
            string indent = new string(' ', indentLevel * TextUtils.IndentSize);
            foreach (var part in SplitAccessors(content))
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                string keyword = FindAccessorKeyword(trimmed);

                if (keyword != null)
                {
                    string after = trimmed.Substring(keyword.Length)
                    .TrimStart();

                    if (after.StartsWith("{"))
                    {
                        int braceEnd = FindMatchingBraceInString(after);

                        if (braceEnd > 0)
                        {
                            string blockContent = after.Substring(1,
                                braceEnd - 1).Trim();
                            sb.Append(indent);
                            sb.Append(keyword);
                            sb.Append('\n');
                            sb.Append(indent);
                            sb.Append("{\n");
                            if (blockContent.Length > 0)
                            {
                                sb.Append(ExpandAccessors(blockContent,
                                    indentLevel + 1));
                            }

                            sb.Append(indent);
                            sb.Append("}\n");
                            continue;
                        }
                    }
                }

                sb.Append(indent);
                sb.Append(trimmed);
                sb.Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Finds an accessor keyword (get/set/init/add/remove) at the
        /// beginning of the string.
        /// </summary>
        /// <param name="s">The string to inspect.</param>
        /// <returns>The matched keyword, or null.</returns>
        private static string FindAccessorKeyword(string s)
        {
            string[] keywords = { "get", "set", "init", "add", "remove" };

            foreach (var kw in keywords)
            {
                if (TextUtils.StartsWithKeyword(s, kw))
                {
                    return kw;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the position of the } that balances the first { in the
        /// string.
        /// </summary>
        /// <param name="s">The string to search.</param>
        /// <returns>The position of the matching }, or -1.</returns>
        private static int FindMatchingBraceInString(string s)
        {
            int depth = 0;

            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '{')
                {
                    depth++;
                }

                else if (s[i] == '}')
                {
                    depth--;

                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Determines whether content is property accessor content: after
        /// splitting, each part starts with an accessor keyword.
        /// </summary>
        /// <param name="content">The content to inspect.</param>
        /// <returns>true if the content is accessor content.</returns>
        private static bool IsAccessorContent(string content)
        {
            string s = content.Trim();
            if (s.Length == 0)
            {
                return false;
            }

            var parts = SplitAccessors(s);

            if (parts.Count == 0)
            {
                return false;
            }

            foreach (var part in parts)
            {
                if (FindAccessorKeyword(part) == null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Splits content at accessor/statement boundaries: splits at ;
        /// at depth 0 or at } close (when depth returns to 0).
        /// </summary>
        /// <param name="content">The content to split.</param>
        /// <returns>A list of split parts (trimmed).</returns>
        private static List<string> SplitAccessors(string content)
        {
            var parts = new List<string>();
            int i = 0;

            while (i < content.Length)
            {
                while (i < content.Length &&
                    char.IsWhiteSpace(content[i]))
                {
                    i++;
                }

                if (i >= content.Length)
                {
                    break;
                }

                int start = i;
                int depth = 0;

                while (i < content.Length)
                {
                    char c = content[i];

                    if (c == '(' || c == '[' || c == '{')
                    {
                        depth++;
                    }

                    else if (c == ')' || c == ']' || c == '}')
                    {
                        if (depth > 0)
                        {
                            depth--;
                        }

                        if (depth == 0 && c == '}')
                        {
                            i++;
                            break;
                        }
                    }

                    else if (c == ';' && depth == 0)
                    {
                        i++;
                        break;
                    }

                    i++;
                }

                string part = content.Substring(start, i - start).Trim();
                if (part.Length > 0)
                {
                    parts.Add(part);
                }
            }

            return parts;
        }
    }
}
