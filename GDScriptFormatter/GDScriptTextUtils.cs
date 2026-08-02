using System.Text;

namespace GDScriptFormatter
{
    /// <summary>
    /// GDScript-specific text utilities. Wraps the shared
    /// <see cref="LafnyaToolkit.Core.Text.TextUtils"/> methods and adds
    /// GDScript-only helpers (colon-block detection, comment-space
    /// normalization).
    /// </summary>
    public sealed class GDScriptTextUtils
    {
        /// <summary>4 spaces per indentation level.</summary>
        public const int IndentSize = 4;

        /// <summary>Maximum line length.</summary>
        public const int MaxLineLength = 80;

        /// <summary>Shared stateless instance.</summary>
        public static readonly GDScriptTextUtils Instance =
            new GDScriptTextUtils();

        private GDScriptTextUtils()
        {
        }

        /// <summary>
        /// Replaces tabs with 4 spaces only at Code-region positions,
        /// preserving tabs inside string literals and comments so that
        /// string contents are never modified.
        /// </summary>
        /// <param name="text">The normalized text.</param>
        /// <param name="isCode">The code mask of the text.</param>
        /// <returns>The text with Code-region tabs expanded to 4 spaces.</returns>
        public string NormalizeTabs(string text, bool[] isCode)
        {
            if (text.Length == 0)
            {
                return text;
            }

            var sb = new StringBuilder(text.Length + 16);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\t' && i < isCode.Length && isCode[i])
                {
                    sb.Append("    ");
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Ensures a single space between the hash prefix and the comment
        /// content for all comment lines. Lines consisting entirely of
        /// hash characters (e.g. separators) and lines that already have
        /// whitespace after the hashes are left unchanged.
        /// </summary>
        /// <param name="text">The text to normalize.</param>
        /// <returns>The text with comment hash prefixes followed by a single space.</returns>
        public string NormalizeCommentSpaces(string text)
        {
            if (text.Length == 0)
            {
                return text;
            }

            var sb = new StringBuilder(text.Length + 16);
            int i = 0;
            int len = text.Length;

            while (i < len)
            {
                if (text[i] == '#')
                {
                    int hashEnd = i;

                    while (hashEnd < len && text[hashEnd] == '#')
                    {
                        hashEnd++;
                    }

                    hashEnd--;
                    int lineEnd = hashEnd + 1;

                    while (lineEnd < len && text[lineEnd] != '\n')
                    {
                        lineEnd++;
                    }

                    if (hashEnd + 1 < lineEnd)
                    {
                        char next = text[hashEnd + 1];

                        if (next != ' ' && next != '\t')
                        {
                            sb.Append(
                                text,
                                i,
                                hashEnd + 1 - i
                            );
                            sb.Append(' ');

                            sb.Append(
                                text,
                                hashEnd + 1,
                                lineEnd - hashEnd - 1
                            );

                            if (lineEnd < len)
                            {
                                sb.Append('\n');
                            }

                            i = lineEnd + 1;
                            continue;
                        }
                    }

                    sb.Append(
                        text,
                        i,
                        lineEnd - i
                    );

                    if (lineEnd < len)
                    {
                        sb.Append('\n');
                    }

                    i = lineEnd + 1;
                }
                else
                {
                    int lineEnd = i;

                    while (lineEnd < len && text[lineEnd] != '\n')
                    {
                        lineEnd++;
                    }

                    sb.Append(
                        text,
                        i,
                        lineEnd - i
                    );

                    if (lineEnd < len)
                    {
                        sb.Append('\n');
                    }

                    i = lineEnd + 1;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Determines whether a line is a block-start line (a code line
        /// ending with a colon).
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>True if the line ends with a colon.</returns>
        public bool IsBlockStartLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            return EndsWithColon(trimmed);
        }

        /// <summary>
        /// Determines whether a line ends with a colon (excluding colons
        /// inside strings/comments; the caller is responsible for
        /// passing a code-region string when applicable).
        /// </summary>
        /// <param name="s">The line text.</param>
        /// <returns>True if the trimmed line ends with ':'.</returns>
        public bool EndsWithColon(string s)
        {
            string t = s.TrimEnd();

            if (t.Length == 0)
            {
                return false;
            }

            return t[t.Length - 1] == ':';
        }
    }
}
