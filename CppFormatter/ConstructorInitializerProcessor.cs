using System;
using System.Collections.Generic;
using LafnyaToolkit.Core.Text;

namespace CppFormatter
{
    /// <summary>
    /// Formats constructor initializer lists according to the C++
    /// style guide:
    /// <list type="bullet">
    /// <item><description>The colon ':' must be on a separate line after the closing ')' of the constructor signature</description></item>
    /// <item><description>The colon is indented at the base indentation level of the constructor (not continuation indent)</description></item>
    /// <item><description>Initializer list members are indented one additional level from the colon</description></item>
    /// </list>
    /// Stateless; the shared instance is exposed via <see cref="Instance"/>.
    /// </summary>
    internal sealed class ConstructorInitializerProcessor
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly ConstructorInitializerProcessor Instance = new ConstructorInitializerProcessor();

        private ConstructorInitializerProcessor()
        {
        }

        /// <summary>
        /// Reformats constructor initializer lists to ensure proper
        /// indentation. Detects lines starting with ':' that are part of
        /// constructor initializer lists and adjusts their indentation
        /// to align with the constructor's base indentation.
        /// </summary>
        /// <param name="lines">The line list after indentation processing.</param>
        /// <returns>The processed line list with correct initializer list indentation.</returns>
        public List<string> Format(List<string> lines)
        {
            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();

                if (trimmed.StartsWith(":") && !IsAccessSpecifierOrLabel(trimmed))
                {
                    int baseIndent = FindConstructorBaseIndent(lines, i);

                    if (baseIndent >= 0)
                    {
                        string afterColon = trimmed.Substring(1).TrimStart();

                        if (afterColon.Length > 0)
                        {
                            result.Add(new string(' ', baseIndent) + ": " + afterColon);
                        }
                        else
                        {
                            result.Add(new string(' ', baseIndent) + ":");
                        }

                        continue;
                    }
                }

                if (i > 0 && IsInitializerContinuationLine(lines, i, result))
                {
                    int baseIndent = FindConstructorBaseIndentFromPrevious(lines, i, result);

                    if (baseIndent >= 0)
                    {
                        result.Add(new string(' ', baseIndent + TextUtils.IndentSize) + trimmed);
                        continue;
                    }
                }

                result.Add(line);
            }

            return result;
        }

        /// <summary>
        /// Determines whether a line starting with ':' is an access
        /// specifier, case label, default label, or pure identifier
        /// label rather than a constructor initializer list colon.
        /// </summary>
        private bool IsAccessSpecifierOrLabel(string trimmed)
        {
            if (CppTextUtils.Instance.IsAccessSpecifier(trimmed))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "case"))
            {
                return true;
            }

            if (trimmed.StartsWith("default:"))
            {
                return true;
            }

            if (trimmed.EndsWith(":") && trimmed.Length > 1)
            {
                string beforeColon = trimmed.Substring(0, trimmed.Length - 1).Trim();

                if (TextUtils.IsPureIdentifier(beforeColon))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the base indentation level of the constructor by
        /// scanning backward from the colon line to find the
        /// constructor signature start.
        /// </summary>
        private int FindConstructorBaseIndent(List<string> lines, int colonLineIndex)
        {
            for (int scanIdx = colonLineIndex - 1; scanIdx >= 0; scanIdx--)
            {
                string scanLine = lines[scanIdx];
                string scanTrimmed = scanLine.TrimStart();

                if (scanTrimmed.Length == 0)
                {
                    continue;
                }

                if (scanTrimmed.EndsWith(")"))
                {
                    return FindConstructorStartIndent(lines, scanIdx);
                }

                if (scanTrimmed.EndsWith(";") || scanTrimmed.EndsWith("{"))
                {
                    break;
                }

                if (CppLineClassifier.Instance.IsBlockStartLine(scanTrimmed))
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
        private int FindConstructorStartIndent(List<string> lines, int closingParenLine)
        {
            int startLine = -1;

            for (int scanIdx = closingParenLine; scanIdx >= 0; scanIdx--)
            {
                string scanLine = lines[scanIdx];
                string scanTrimmed = scanLine.TrimStart();

                if (scanTrimmed.Length == 0)
                {
                    continue;
                }

                if (LooksLikeConstructorSignature(scanTrimmed))
                {
                    startLine = scanIdx;
                    break;
                }

                if (scanTrimmed.EndsWith(";") || CppLineClassifier.Instance.IsBlockStartLine(scanTrimmed))
                {
                    break;
                }
            }

            if (startLine >= 0)
            {
                return lines[startLine].Length - lines[startLine].TrimStart().Length;
            }

            return FindMinimumIndentInParamList(lines, closingParenLine);
        }

        /// <summary>
        /// Finds the minimum indentation in the parameter list area,
        /// which corresponds to the constructor signature base indent.
        /// </summary>
        private int FindMinimumIndentInParamList(List<string> lines, int closingParenLine)
        {
            int minIndent = lines[closingParenLine].Length - lines[closingParenLine].TrimStart().Length;

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

                if (CppTextUtils.Instance.IsAccessSpecifier(scanTrimmed))
                {
                    minIndent = indent;
                    break;
                }

                if (TextUtils.StartsWithKeyword(scanTrimmed, "class") || TextUtils.StartsWithKeyword(scanTrimmed, "struct"))
                {
                    if (scanLine.Contains("{"))
                    {
                        int classIndent = indent;
                        minIndent = Math.Min(minIndent, classIndent + TextUtils.IndentSize);
                    }

                    break;
                }

                if (scanTrimmed.EndsWith(";") || CppLineClassifier.Instance.IsBlockStartLine(scanTrimmed))
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
            if (trimmed.Contains("::"))
            {
                return true;
            }

            if (trimmed.StartsWith("template"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "explicit"))
            {
                return true;
            }

            int parenPos = trimmed.IndexOf('(');

            if (parenPos > 0)
            {
                string beforeParen = trimmed.Substring(0, parenPos).TrimEnd();

                if (TextUtils.IsPureIdentifier(beforeParen))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the current line is a continuation of the
        /// initializer list (a member initializer that follows the
        /// colon line or previous continuation).
        /// </summary>
        private bool IsInitializerContinuationLine(List<string> lines, int currentIdx, List<string> processedResult)
        {
            string trimmed = lines[currentIdx].TrimStart();

            if (!CppTextUtils.Instance.LooksLikeMemberInitializer(trimmed))
            {
                return false;
            }

            if (processedResult.Count == 0)
            {
                return false;
            }

            string prevProcessed = processedResult[processedResult.Count - 1];
            string prevTrimmed = prevProcessed.TrimStart();
            return prevTrimmed.StartsWith(":") || prevTrimmed.EndsWith(",") || prevTrimmed.EndsWith("{");
        }

        /// <summary>
        /// Finds the constructor base indent by looking at previous
        /// processed lines.
        /// </summary>
        private int FindConstructorBaseIndentFromPrevious(List<string> lines, int currentIdx, List<string> processedResult)
        {
            for (int scanIdx = processedResult.Count - 1; scanIdx >= 0; scanIdx--)
            {
                string scanLine = processedResult[scanIdx];
                string scanTrimmed = scanLine.TrimStart();

                if (scanTrimmed.StartsWith(":") && !IsAccessSpecifierOrLabel(scanTrimmed))
                {
                    return scanLine.Length - scanLine.TrimStart().Length;
                }

                if (scanTrimmed.EndsWith(";") || scanTrimmed.EndsWith("{") || scanTrimmed.EndsWith("}") || CppLineClassifier.Instance.IsBlockStartLine(scanTrimmed))
                {
                    break;
                }
            }

            return -1;
        }
    }
}
