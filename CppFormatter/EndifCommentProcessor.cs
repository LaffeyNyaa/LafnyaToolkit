using System.Collections.Generic;
using System.Text;

namespace CppFormatter
{
    /// <summary>
    /// Appends <c>// &lt;macro_name&gt;</c> comments after each <c>#endif</c>
    /// directive, where the macro name is taken from the matching
    /// <c>#if</c>/<c>#ifdef</c>/<c>#ifndef</c>. Nested preprocessor
    /// conditionals are correctly handled via a stack.
    /// </summary>
    internal static class EndifCommentProcessor
    {
        /// <summary>Maximum supported nesting depth.</summary>
        private const int MaxNesting = 32;

        /// <summary>
        /// Processes the source text and appends <c>// &lt;macro&gt;</c>
        /// comments after each <c>#endif</c>.
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <returns>The processed source string.</returns>
        public static string AppendEndifComments(string source)
        {
            string[] lines = source.Split('\n');
            var stack = new Stack<string>(MaxNesting);
            var result = new StringBuilder(source.Length);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();

                if (trimmed.StartsWith("#ifdef"))
                {
                    string macro = ExtractMacroName(trimmed, "#ifdef");

                    if (macro != null)
                    {
                        stack.Push(macro);
                    }
                }
                else if (trimmed.StartsWith("#ifndef"))
                {
                    string macro = ExtractMacroName(trimmed, "#ifndef");

                    if (macro != null)
                    {
                        stack.Push(macro);
                    }
                }
                else if (trimmed.StartsWith("#if") &&
                    !trimmed.StartsWith("#ifdef"))
                {
                    // This is a bare #if (not #ifdef / #ifndef)
                    string macro = ExtractMacroName(trimmed, "#if");

                    if (macro != null)
                    {
                        stack.Push(macro);
                    }
                }
                else if (trimmed.StartsWith("#endif"))
                {
                    if (stack.Count > 0)
                    {
                        string macro = stack.Pop();
                        // Determine whether #endif already carries a // comment
                        int endifStart = trimmed.IndexOf("#endif");
                        string afterEndif = trimmed.Substring(endifStart + 6);

                        if (!afterEndif.Contains("//"))
                        {
                            line = line.TrimEnd() + "  // " + macro;
                        }
                    }
                }

                // #else / #elif intentionally ignored – they do not affect the stack.

                if (i > 0)
                {
                    result.Append('\n');
                }

                result.Append(line);
            }

            return result.ToString();
        }

        /// <summary>
        /// Extracts the macro name from a preprocessor conditional directive.
        /// </summary>
        /// <param name="trimmed">The line trimmed of leading whitespace.</param>
        /// <param name="directive">"#if", "#ifdef" or "#ifndef".</param>
        /// <returns>The macro name, or <c>null</c> if none could be extracted.</returns>
        private static string ExtractMacroName(string trimmed, string directive)
        {
            int pos = directive.Length;
            // Skip whitespace after the directive keyword

            while (pos < trimmed.Length && char.IsWhiteSpace(trimmed[pos]))
            {
                pos++;
            }

            if (pos >= trimmed.Length)
            {
                return null;
            }

            // #ifdef / #ifndef – the next token is the macro name directly

            if (directive != "#if")
            {
                return ReadIdentifier(trimmed, ref pos);
            }

            // #if – skip unary operators and opening parentheses

            while (pos < trimmed.Length &&
                (trimmed[pos] == '!' || trimmed[pos] == '('))
            {
                pos++;
            }

            // Skip whitespace after operators

            while (pos < trimmed.Length && char.IsWhiteSpace(trimmed[pos]))
            {
                pos++;
            }

            if (pos >= trimmed.Length)
            {
                return null;
            }

            // Handle defined(...) – extract the identifier inside the parentheses

            if (pos + 7 <= trimmed.Length &&
                trimmed.Substring(pos, 7) == "defined")
            {
                pos += 7;

                while (pos < trimmed.Length &&
                    (char.IsWhiteSpace(trimmed[pos]) || trimmed[pos] == '('))
                {
                    pos++;
                }

                return ReadIdentifier(trimmed, ref pos);
            }

            // Default: first identifier is the macro name
            return ReadIdentifier(trimmed, ref pos);
        }

        /// <summary>
        /// Reads a C/C++ identifier at the current position and advances <paramref name="pos"/>.
        /// </summary>
        private static string ReadIdentifier(string text, ref int pos)
        {
            int start = pos;

            while (pos < text.Length &&
                (char.IsLetterOrDigit(text[pos]) || text[pos] == '_'))
            {
                pos++;
            }

            if (pos > start)
            {
                return text.Substring(start, pos - start);
            }

            return null;
        }
    }
}
