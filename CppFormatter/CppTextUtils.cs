using System;
using System.Collections.Generic;

using LafnyaToolkit.Core.Text;

namespace CppFormatter
{
    /// <summary>
    /// C++-specific text helpers that complement
    /// <see cref="LafnyaToolkit.Core.Text.TextUtils"/>. Provides
    /// brace-pair navigation (respecting code regions), access
    /// specifier detection, include directive detection, and
    /// member-initializer pattern detection. Stateless; the
    /// single shared instance is exposed via <see cref="Instance"/>.
    /// </summary>
    internal sealed class CppTextUtils
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly CppTextUtils Instance = new CppTextUtils();
        private CppTextUtils()
        {
        }

        /// <summary>
        /// Finds the first open brace (<c>{</c>) at or after
        /// <paramref name="start"/> that lies in a code region.
        /// Stops early if a semicolon is encountered before any brace,
        /// returning -1.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">Boolean mask indicating code regions.</param>
        /// <param name="start">The character position to start searching from.</param>
        /// <returns>The position of the open brace, or -1 if not found.</returns>
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
        /// Finds the matching closing brace for the open brace at
        /// <paramref name="openPos"/>, respecting brace nesting and
        /// only considering code regions.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">Boolean mask indicating code regions.</param>
        /// <param name="openPos">The position of the open brace.</param>
        /// <returns>The position of the matching close brace, or -1 if not found.</returns>
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

        /// <summary>
        /// Determines whether a trimmed line is an access specifier:
        /// public:, protected:, or private:.
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>True if the line is an access specifier.</returns>
        public bool IsAccessSpecifier(string trimmed)
        {
            return trimmed == "public:" || trimmed == "protected:" || trimmed ==
                "private:";
        }

        /// <summary>
        /// Determines whether a line is an #include directive.
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>True if the line is an #include directive.</returns>
        public bool IsIncludeDirective(string trimmed)
        {
            return trimmed.StartsWith("#include");
        }

        /// <summary>
        /// Determines whether a string looks like a member initializer
        /// (identifier followed by parentheses or braces for initialization).
        /// </summary>
        /// <param name="s">The string to test.</param>
        /// <returns>True if <paramref name="s"/> looks like a member initializer.</returns>
        public bool LooksLikeMemberInitializer(string s)
        {
            if (s.Length == 0)
            {
                return false;
            }

            int parenPos = s.IndexOf('(');
            int bracePos = s.IndexOf('{');
            int initPos = -1;

            if (parenPos >= 0 && bracePos >= 0)
            {
                initPos = Math.Min(parenPos, bracePos);
            }
            else if (parenPos >= 0)
            {
                initPos = parenPos;
            }
            else if (bracePos >= 0)
            {
                initPos = bracePos;
            }

            if (initPos <= 0)
            {
                return false;
            }

            string beforeInit = s.Substring(0, initPos);
            return TextUtils.IsPureIdentifier(beforeInit)

            || (beforeInit.EndsWith("_") && beforeInit.Length > 1 &&
                TextUtils.IsPureIdentifier(beforeInit.Substring(0,
                beforeInit.Length - 1)));
        }
    }
}
