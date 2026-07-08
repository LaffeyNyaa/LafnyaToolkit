using System;
using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Formats constructor initializer lists according to the C++ style guide:
    /// - The colon ':' must be on a separate line after the closing ')' of the constructor signature
    /// - The colon is indented at the base indentation level of the constructor (not continuation indent)
    /// - Initializer list members are indented one additional level from the colon
    /// </summary>
    internal static class ConstructorInitializerProcessor
    {
        /// <summary>
        /// Reformats constructor initializer lists to ensure proper indentation.
        /// Detects lines starting with ':' that are part of constructor initializer lists
        /// and adjusts their indentation to align with the constructor's base indentation.
        /// </summary>
        /// <param name="lines">The line list after indentation processing.</param>
        /// <returns>The processed line list with correct initializer list indentation.</returns>
        internal static List<string> Format(List<string> lines)
        {
            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                // Check if this line starts with ':' (potential constructor initializer list)

                if (trimmed.StartsWith(":") &&
                    !IsAccessSpecifierOrLabel(trimmed))
                {
                    // Find the constructor signature's base indentation by scanning backward
                    int baseIndent = FindConstructorBaseIndent(lines, i);

                    if (baseIndent >= 0)
                    {
                        // Apply base indentation to the colon line
                        // The colon should be at base indent, content after colon gets +1 indent
                        string afterColon = trimmed.Substring(1).TrimStart();

                        if (afterColon.Length > 0)
                        {
                            // Colon line with content: indent colon at base, content at base+1
                            result.Add(new string(' ', baseIndent) + ": " +
                                afterColon);
                        }
                        else
                        {
                            // Just colon (rare case): indent at base
                            result.Add(new string(' ', baseIndent) + ":");
                        }

                        continue;
                    }
                }

                // Check if this is a continuation line in the initializer list
                // (starts with member initializer like "member_name_(...)")

                if (i > 0 && IsInitializerContinuationLine(lines, i, result))
                {
                    int baseIndent =
                        FindConstructorBaseIndentFromPrevious(lines, i, result);

                    if (baseIndent >= 0)
                    {
                        // Continuation lines get base+1 indent (same as content after colon)
                        result.Add(new string(' ', baseIndent +
                            TextUtils.IndentSize) + trimmed);

                        continue;
                    }
                }

                result.Add(line);
            }

            return result;
        }

        /// <summary>
        /// Determines whether a line starting with ':' is an access specifier or label,
        /// not a constructor initializer list colon.
        /// </summary>
        private static bool IsAccessSpecifierOrLabel(string trimmed)
        {
            // Access specifiers: public:, private:, protected:

            if (TextUtils.IsAccessSpecifier(trimmed))
            {
                return true;
            }

            // Case labels: case ...:

            if (TextUtils.StartsWithKeyword(trimmed, "case"))
            {
                return true;
            }

            // Default label: default:

            if (trimmed.StartsWith("default:"))
            {
                return true;
            }

            // Pure identifier label (identifier followed by :)
            // This is handled by checking if it's a pure identifier

            if (trimmed.EndsWith(":") && trimmed.Length > 1)
            {
                string beforeColon = trimmed.Substring(0, trimmed.Length -
                    1).Trim();

                if (IsPureIdentifier(beforeColon))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a string is a pure C++ identifier.
        /// </summary>
        private static bool IsPureIdentifier(string s)
        {
            if (s.Length == 0)
            {
                return false;
            }

            if (!char.IsLetter(s[0]) && s[0] != '_')
            {
                return false;
            }

            foreach (char c in s)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Finds the base indentation level of the constructor by scanning backward
        /// from the colon line to find the constructor signature start.
        /// </summary>
        private static int FindConstructorBaseIndent(List<string> lines,
            int colonLineIndex)
        {
            // Scan backward to find the closing ')' of the constructor signature
            // The colon should come immediately after the line with ')'

            for (int scanIdx = colonLineIndex - 1; scanIdx >= 0; scanIdx--)
            {
                string scanLine = lines[scanIdx];
                string scanTrimmed = scanLine.TrimStart();

                if (scanTrimmed.Length == 0)
                {
                    continue;
                }

                // Check if this line ends with ')' (closing of constructor parameter list)

                if (scanTrimmed.EndsWith(")"))
                {
                    // Found the closing parenthesis line
                    // Now find the constructor signature start line (the line with the constructor name)
                    return FindConstructorStartIndent(lines, scanIdx);
                }

                // If we hit a line ending with ';' or '{', this is not a constructor initializer

                if (scanTrimmed.EndsWith(";") || scanTrimmed.EndsWith("{"))
                {
                    break;
                }

                // If we hit a block start keyword, stop scanning

                if (TextUtils.IsBlockStartLine(scanTrimmed))
                {
                    break;
                }
            }

            return -1;
        }

        /// <summary>
        /// Finds the indentation of the constructor signature start line
        /// by scanning backward from the ')' closing line.
        /// </summary>
        private static int FindConstructorStartIndent(List<string> lines,
            int closingParenLine)
        {
            // Scan backward to find the line that starts the constructor signature
            // (the line containing the constructor name and opening '(')
            // We look for the first line that looks like a constructor signature,
            // which typically contains a qualified name with '::' or starts with a class name.
            int startLine = -1;

            for (int scanIdx = closingParenLine; scanIdx >= 0; scanIdx--)
            {
                string scanLine = lines[scanIdx];
                string scanTrimmed = scanLine.TrimStart();

                if (scanTrimmed.Length == 0)
                {
                    continue;
                }

                // Check if this line looks like a constructor signature
                // Constructor signatures typically:
                // - Contain '::' (qualified name like Class::Class)
                // - Start with template declaration
                // - Start with explicit keyword
                // - Or the first '(' is preceded by what looks like a constructor name

                if (LooksLikeConstructorSignature(scanTrimmed))
                {
                    startLine = scanIdx;
                    break;
                }

                // If we hit a block boundary, stop

                if (scanTrimmed.EndsWith(";") ||
                    TextUtils.IsBlockStartLine(scanTrimmed))
                {
                    break;
                }
            }

            if (startLine >= 0)
            {
                // Return the indentation of the constructor signature start line
                return lines[startLine].Length -
                    lines[startLine].TrimStart().Length;
            }

            // Fallback: use the indentation of the closing ')' line minus continuation
            // But we need to be careful - the closing ')' line might have continuation indent
            // We should use the minimum indent we find in the parameter list
            return FindMinimumIndentInParamList(lines, closingParenLine);
        }

        /// <summary>
        /// Finds the minimum indentation in the parameter list area,
        /// which corresponds to the constructor signature base indent.
        /// </summary>
        private static int FindMinimumIndentInParamList(List<string> lines,
            int closingParenLine)
        {
            int minIndent = lines[closingParenLine].Length -
                lines[closingParenLine].TrimStart().Length;

            // Scan backward to find the line with minimum indent (the signature start)

            for (int scanIdx = closingParenLine - 1; scanIdx >= 0; scanIdx--)
            {
                string scanLine = lines[scanIdx];
                string scanTrimmed = scanLine.TrimStart();

                if (scanTrimmed.Length == 0)
                {
                    continue;
                }

                int indent = scanLine.Length - scanLine.TrimStart().Length;

                if (indent < minIndent)
                {
                    minIndent = indent;
                }

                // Special handling: if we hit a line that starts with 'public:', 'private:', or 'protected:',
                // its indent should be the base indent for the constructor.
                // Access specifiers are at the same level as the class keyword.

                if (TextUtils.IsAccessSpecifier(scanTrimmed))
                {
                    // Access specifier indent is the base indent for member declarations
                    minIndent = indent;
                    break;
                }

                // If we hit a class/struct keyword line, its content indent is +1 from its own indent

                if (TextUtils.StartsWithKeyword(scanTrimmed, "class") ||
                    TextUtils.StartsWithKeyword(scanTrimmed, "struct"))
                {
                    // The class line itself might be at namespace level (0 or base indent)
                    // Content inside class is at class indent + 1
                    // If we found a smaller indent earlier, use it
                    // Otherwise, the content indent is class line indent + IndentSize

                    if (scanLine.Contains("{"))
                    {
                        // Inline class definition: content is at class indent + IndentSize
                        int classIndent = indent;

                        minIndent = Math.Min(minIndent, classIndent +
                            TextUtils.IndentSize);
                    }

                    break;
                }

                // Stop when we hit a block boundary

                if (scanTrimmed.EndsWith(";") ||
                    TextUtils.IsBlockStartLine(scanTrimmed))
                {
                    break;
                }
            }

            return minIndent;
        }

        /// <summary>
        /// Determines whether a line looks like a constructor signature
        /// (contains qualified name with :: or has constructor-like pattern).
        /// </summary>
        private static bool LooksLikeConstructorSignature(string trimmed)
        {
            // Constructor patterns typically have:
            // 1. Class::Class(...) - qualified name with same name before and after ::
            // 2. Template<...> Class::Class(...) - template + qualified name
            // 3. explicit Class::Class(...) - explicit keyword + qualified name
            // 4. Class(...) - simple class name in class definition context
            // Check for :: pattern (qualified name)

            if (trimmed.Contains("::"))
            {
                return true;
            }

            // Check for template declaration

            if (trimmed.StartsWith("template"))
            {
                return true;
            }

            // Check for explicit keyword

            if (TextUtils.StartsWithKeyword(trimmed, "explicit"))
            {
                return true;
            }

            // Check for simple constructor pattern: ClassName(...)
            // This is a constructor in class definition context where there's no ::
            int parenPos = trimmed.IndexOf('(');

            if (parenPos > 0)
            {
                string beforeParen = trimmed.Substring(0, parenPos).TrimEnd();
                // Check if it's an identifier that could be a class name
                // (typically starts with capital letter or is a known naming pattern)

                if (IsPureIdentifier(beforeParen))
                {
                    // Could be a constructor name (Class::Class or just Class)
                    // We need additional context to confirm, but this is a good heuristic
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Counts occurrences of a character in a string.
        /// </summary>
        private static int CountChar(string s, char c)
        {
            int count = 0;

            foreach (char ch in s)
            {
                if (ch == c)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Determines whether the current line is a continuation of the initializer list
        /// (a member initializer that follows the colon line or previous continuation).
        /// </summary>
        private static bool IsInitializerContinuationLine(List<string> lines,
            int currentIdx,
            List<string> processedResult)
        {
            // A continuation line in initializer list:
            // - Starts with member_name_(...) or similar pattern
            // - Follows a colon line or another continuation line ending with comma
            string trimmed = lines[currentIdx].TrimStart();
            // Must look like a member initializer (identifier followed by parentheses or braces)

            if (!LooksLikeMemberInitializer(trimmed))
            {
                return false;
            }

            // Check if previous processed line was colon or continuation ending with comma

            if (processedResult.Count == 0)
            {
                return false;
            }

            string prevProcessed = processedResult[processedResult.Count - 1];
            string prevTrimmed = prevProcessed.TrimStart();
            // Previous line must be:
            // - Starting with ':' (colon line)
            // - Ending with ',' (continuation with comma)
            // - Or ending with '{' (brace initializer)
            return prevTrimmed.StartsWith(":") ||
                prevTrimmed.EndsWith(",") ||
                prevTrimmed.EndsWith("{");
        }

        /// <summary>
        /// Determines whether a line looks like a member initializer
        /// (identifier followed by parentheses or braces for initialization).
        /// </summary>
        private static bool LooksLikeMemberInitializer(string trimmed)
        {
            // Member initializer patterns:
            // - member_(value)
            // - member_{value}
            // - member_(std::move(value))
            // etc.

            if (trimmed.Length == 0)
            {
                return false;
            }

            // Find first '(' or '{' that follows an identifier
            int parenPos = trimmed.IndexOf('(');
            int bracePos = trimmed.IndexOf('{');
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

            // Check if the part before '(' or '{' is an identifier (possibly with trailing underscore)
            string beforeInit = trimmed.Substring(0, initPos);
            // Allow identifiers with common member name patterns
            // (letters, digits, underscores, ending with underscore is common for members)
            return IsPureIdentifier(beforeInit) ||
                (beforeInit.EndsWith("_") && beforeInit.Length > 1 &&
                IsPureIdentifier(beforeInit.Substring(0, beforeInit.Length -
                1)));
        }

        /// <summary>
        /// Finds the constructor base indent by looking at previous processed lines.
        /// </summary>
        private static int FindConstructorBaseIndentFromPrevious(List<string>
            lines, int currentIdx,
            List<string> processedResult)
        {
            // Scan backward through processed result to find colon line

            for (int scanIdx = processedResult.Count - 1; scanIdx >= 0; scanIdx-
                -)
            {
                string scanLine = processedResult[scanIdx];
                string scanTrimmed = scanLine.TrimStart();

                if (scanTrimmed.StartsWith(":") &&
                    !IsAccessSpecifierOrLabel(scanTrimmed))
                {
                    // Found colon line - return its indentation
                    return scanLine.Length - scanLine.TrimStart().Length;
                }

                // Stop if we hit a statement boundary

                if (scanTrimmed.EndsWith(";") || scanTrimmed.EndsWith("{") ||
                    scanTrimmed.EndsWith("}") ||
                    TextUtils.IsBlockStartLine(scanTrimmed))
                {
                    break;
                }
            }

            return -1;
        }
    }
}
