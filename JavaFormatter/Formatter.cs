using System.Collections.Generic;
using System.Text;

namespace JavaFormatter
{
    /// <summary>
    /// Applies all Java formatting rules to source code.
    /// </summary>
    internal static class Formatter
    {
        /// <summary>Each indent level uses 4 spaces.</summary>
        private const int IndentSize = 4;
        /// <summary>Maximum length of a single line.</summary>
        private const int MaxLineLength = 80;

        /// <summary>
        /// Applies all formatting rules to the source string and returns the result.
        /// </summary>
        /// <param name="source">The raw source code string.</param>
        /// <param name="targetRoot">The target root directory path (used by ImportSorter).</param>
        /// <returns>The formatted source code string.</returns>
        public static string Format(string source, string targetRoot)
        {
            var tokens = Tokenizer.Tokenize(source);
            tokens = ApplyMandatoryBraces(tokens);
            string text = Tokenizer.Reconstruct(tokens);
            text = FormatEnums(text);
            text = ImportSorter.Sort(text, targetRoot);
            text = text.Replace("\t", "    ");
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = MoveOpenBraceToOwnLine(text);
            var lines = SplitLines(text);
            lines = Reindent(lines, text);
            lines = ApplyBlankLineRules(lines);
            lines = CollapseBlankLines(lines);
            lines = TrimTrailingWhitespace(lines);
            lines = ApplyLineLengthLimit(lines);
            string result = string.Join("\n", lines);
            result = EnsureSingleTrailingNewline(result);
            return result;
        }

        /// <summary>
        /// Wraps single-statement bodies of control flow keywords with braces
        /// on the token stream.
        /// </summary>
        /// <param name="tokens">The token list.</param>
        /// <returns>The processed token list.</returns>
        private static List<Token> ApplyMandatoryBraces(List<Token> tokens)
        {
            string text = Tokenizer.Reconstruct(tokens);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            var insertions = new List<Insertion>();

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                if (i > 0 && IsWordChar(text[i - 1]))
                {
                    continue;
                }

                if (MatchesWord(text, i, "if"))
                {
                    int afterParen = SkipParen(text, isCode, i + 2);

                    if (afterParen >= 0)
                    {
                        CollectBodyInsertions(text, isCode, afterParen,
                            insertions);
                    }
                }

                else if (MatchesWord(text, i, "for"))
                {
                    int afterParen = SkipParen(text, isCode, i + 3);

                    if (afterParen >= 0)
                    {
                        CollectBodyInsertions(text, isCode, afterParen,
                            insertions);
                    }
                }

                else if (MatchesWord(text, i, "while"))
                {
                    int afterParen = SkipParen(text, isCode, i + 5);

                    if (afterParen >= 0)
                    {
                        int nextNonWs = SkipWhitespace(text, afterParen);

                        if (nextNonWs < text.Length && isCode[nextNonWs] &&
                            text[nextNonWs] == ';')
                        {
                            continue;
                        }

                        CollectBodyInsertions(text, isCode, afterParen,
                            insertions);
                    }
                }

                else if (MatchesWord(text, i, "do"))
                {
                    CollectBodyInsertions(text, isCode, i + 2, insertions);
                }

                else if (MatchesWord(text, i, "synchronized"))
                {
                    int afterParen = SkipParen(text, isCode, i + 12);

                    if (afterParen >= 0)
                    {
                        CollectBodyInsertions(text, isCode, afterParen,
                            insertions);
                    }
                }

                else if (MatchesWord(text, i, "try"))
                {
                    CollectBodyInsertions(text, isCode, i + 3, insertions);
                }

                else if (MatchesWord(text, i, "else"))
                {
                    int afterElse = i + 4;
                    int nextNonWs = SkipWhitespace(text, afterElse);

                    if (MatchesWord(text, nextNonWs, "if"))
                    {
                        continue;
                    }

                    CollectBodyInsertions(text, isCode, afterElse, insertions);
                }
            }

            if (insertions.Count == 0)
            {
                return tokens;
            }

            insertions.Sort((a, b) => a.Position.CompareTo(b.Position));
            var sb = new StringBuilder(text.Length + insertions.Count * 8);
            int pos = 0;

            foreach (var ins in insertions)
            {
                sb.Append(text, pos, ins.Position - pos);
                sb.Append(ins.Text);
                pos = ins.Position;
            }

            sb.Append(text, pos, text.Length - pos);
            return Tokenizer.Tokenize(sb.ToString());
        }

        /// <summary>
        /// Replaces a single-statement body with a braced block, appending insertion points.
        /// </summary>
        private static void CollectBodyInsertions(string text, bool[] isCode,
            int startPos, List<Insertion> insertions)
        {
            int i = SkipWhitespace(text, startPos);

            if (i >= text.Length)
            {
                return;
            }

            if (isCode[i] && text[i] == '{')
            {
                return;
            }

            int stmtStart = i;
            int j = i;
            int depth = 0;

            while (j < text.Length)
            {
                if (isCode[j])
                {
                    char c = text[j];

                    if (c == '(' || c == '[')
                    {
                        depth++;
                    }

                    else if (c == ')' || c == ']')
                    {
                        if (depth > 0)
                        {
                            depth--;
                        }
                    }

                    else if (c == ';' && depth == 0)
                    {
                        break;
                    }
                }

                j++;
            }

            if (j >= text.Length)
            {
                return;
            }

            int stmtEnd = j + 1;
            insertions.Add(new Insertion(stmtStart, "{\n"));
            insertions.Add(new Insertion(stmtEnd, "\n}"));
        }

        /// <summary>
        /// Finds all enum declarations and expands members to multiple lines.
        /// </summary>
        private static string FormatEnums(string text)
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

                if (i > 0 && IsWordChar(text[i - 1]))
                {
                    continue;
                }

                if (!MatchesWord(text, i, "enum"))
                {
                    continue;
                }

                if (i + 4 < text.Length && IsWordChar(text[i + 4]))
                {
                    continue;
                }

                int braceStart = FindOpenBrace(text, isCode, i + 4);

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
                    sb.Append(new string(' ', IndentSize));
                    sb.Append(members[k].Trim());

                    if (k < members.Count - 1)
                    {
                        sb.Append(',');
                    }

                    sb.Append('\n');
                }

                replacements.Add(new Replacement(braceStart + 1, braceEnd,
                    sb.ToString()));
            }

            return ApplyReplacements(text, replacements);
        }

        /// <summary>
        /// Splits text into lines.
        /// </summary>
        private static List<string> SplitLines(string text)
        {
            return new List<string>(text.Split('\n'));
        }

        /// <summary>
        /// Moves an open brace at the end of a line to its own line (Allman style).
        /// Only splits when there is non-whitespace content before the brace.
        /// </summary>
        private static string MoveOpenBraceToOwnLine(string text)
        {
            string[] lines = text.Split('\n');
            var result = new List<string>(lines.Length + 16);

            foreach (var line in lines)
            {
                string trimmedEnd = line.TrimEnd();

                if (trimmedEnd.Length > 1 && trimmedEnd[trimmedEnd.Length - 1] == '{')
                {
                    string beforeBrace = trimmedEnd.Substring(0,
                        trimmedEnd.Length - 1).TrimEnd();

                    if (beforeBrace.Length > 0)
                    {
                        result.Add(beforeBrace);
                        result.Add("{");
                    }

                    else
                    {
                        result.Add(line);
                    }
                }

                else
                {
                    result.Add(line);
                }
            }

            return string.Join("\n", result);
        }

        /// <summary>
        /// Recomputes leading whitespace for each line based on nesting depth.
        /// Lines entirely inside a TextBlock or MultiLineComment token (non-first line)
        /// preserve their original leading whitespace.
        /// </summary>
        private static List<string> Reindent(List<string> lines, string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            int[] depths = new int[lines.Count];
            bool[] preserveIndent = ComputePreserveIndent(lines, tokens);
            bool[] inEnumBlock = ComputeInEnumBlock(lines, text, isCode);
            int depth = 0;
            int lineIdx = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\n')
                {
                    lineIdx++;

                    if (lineIdx < depths.Length)
                    {
                        depths[lineIdx] = depth;
                    }

                    continue;
                }

                if (isCode[i] && c == '{')
                {
                    depth++;
                }

                else if (isCode[i] && c == '}')
                {
                    depth--;

                    if (depth < 0)
                    {
                        depth = 0;
                    }

                    if (lineIdx < depths.Length)
                    {
                        depths[lineIdx] = depth;
                    }
                }
            }

            var result = new List<string>(lines.Count);
            var lineStarts = new int[lines.Count];
            int p = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                lineStarts[i] = p;
                p += lines[i].Length;

                if (i < lines.Count - 1)
                {
                    p++;
                }
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (preserveIndent[i])
                {
                    result.Add(lines[i]);
                    continue;
                }

                string content = lines[i].TrimStart();

                if (content.Length == 0)
                {
                    result.Add(string.Empty);
                    continue;
                }

                int baseDepth = depths[i];

                if (i > 0 && !inEnumBlock[i] &&
                    IsContinuationIndicator(lines[i - 1], lineStarts[i - 1],
                    text, isCode))
                {
                    baseDepth++;
                }

                result.Add(new string(' ', baseDepth * IndentSize) + content);
            }

            return result;
        }

        /// <summary>
        /// Determines whether the specified line ends with a continuation indicator.
        /// Only returns true when the trailing indicator character is in a Code token region.
        /// </summary>
        /// <param name="line">The line text to check.</param>
        /// <param name="lineStart">The starting offset of this line in <paramref name="text"/>.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">The code mask of the same length as <paramref name="text"/>.</param>
        /// <returns>True if the line ends with a code-region continuation indicator; otherwise false.</returns>
        private static bool IsContinuationIndicator(string line, int lineStart,
            string text, bool[] isCode)
        {
            string trimmed = line.TrimEnd();

            if (trimmed.Length == 0)
            {
                return false;
            }

            int lastIdxInText = lineStart + trimmed.Length - 1;

            if (lastIdxInText < 0 || lastIdxInText >= isCode.Length ||
                !isCode[lastIdxInText])
            {
                return false;
            }

            char last = trimmed[trimmed.Length - 1];

            if (last == ',' || last == '+' || last == '(' || last == '=' ||
                last == '?')
            {
                return true;
            }

            if (trimmed.Length >= 2)
            {
                int secondLastIdxInText = lineStart + trimmed.Length - 2;

                if (secondLastIdxInText >= 0 &&
                    secondLastIdxInText < isCode.Length &&
                    isCode[secondLastIdxInText])
                {
                    string last2 = trimmed.Substring(trimmed.Length - 2);

                    if (last2 == "&&" || last2 == "||")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Computes whether each line is inside an enum block (between enum's { and }).
        /// Enum member lines ending with , should not be treated as continuation indicators.
        /// </summary>
        private static bool[] ComputeInEnumBlock(List<string> lines,
            string text, bool[] isCode)
        {
            var inEnumBlock = new bool[lines.Count];
            var lineStarts = new int[lines.Count];
            int pos = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                lineStarts[i] = pos;
                pos += lines[i].Length;

                if (i < lines.Count - 1)
                {
                    pos++;
                }
            }

            var enumRanges = new List<KeyValuePair<int, int>>();
            int depth = 0;
            int enumDepth = -1;
            int enumStart = -1;
            bool pendingEnum = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = text[i];

                if (c == 'e' && (i == 0 || !IsWordChar(text[i - 1])) &&
                    MatchesWord(text, i, "enum"))
                {
                    pendingEnum = true;
                }

                if (c == '{')
                {
                    if (pendingEnum)
                    {
                        enumStart = i;
                        enumDepth = depth + 1;
                        pendingEnum = false;
                    }

                    depth++;
                }

                else if (c == '}')
                {
                    depth--;

                    if (depth < 0)
                    {
                        depth = 0;
                    }

                    if (enumDepth >= 0 && depth < enumDepth)
                    {
                        enumRanges.Add(new KeyValuePair<int, int>(enumStart,
                            i));
                        enumStart = -1;
                        enumDepth = -1;
                    }
                }

                else if (c == ';')
                {
                    pendingEnum = false;
                }
            }

            foreach (var range in enumRanges)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lineStarts[i] > range.Key &&
                        lineStarts[i] < range.Value)
                    {
                        inEnumBlock[i] = true;
                    }
                }
            }

            return inEnumBlock;
        }

        /// <summary>
        /// Computes whether each line should preserve its original leading whitespace:
        /// true when the line starts inside a TextBlock or MultiLineComment token (non-first line).
        /// </summary>
        private static bool[] ComputePreserveIndent(List<string> lines,
            List<Token> tokens)
        {
            var preserveIndent = new bool[lines.Count];
            var lineStarts = new int[lines.Count];
            int pos = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                lineStarts[i] = pos;
                pos += lines[i].Length;

                if (i < lines.Count - 1)
                {
                    pos++;
                }
            }

            int tokenPos = 0;

            foreach (var token in tokens)
            {
                int tokenStart = tokenPos;
                int tokenEnd = tokenPos + token.Text.Length;

                if (token.Kind == TokenKind.TextBlock ||
                    token.Kind == TokenKind.MultiLineComment)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lineStarts[i] > tokenStart &&
                            lineStarts[i] < tokenEnd)
                        {
                            preserveIndent[i] = true;
                        }
                    }
                }

                tokenPos = tokenEnd;
            }

            return preserveIndent;
        }

        /// <summary>
        /// Ensures exactly one blank line above blocks, multi-line statements, and declarations
        /// (with exceptions for the beginning/end of file). Annotation lines (starting with @)
        /// do not get blank lines inserted above them. Consecutive import lines also do not get
        /// blank lines inserted between them.
        /// </summary>
        private static List<string> ApplyBlankLineRules(List<string> lines)
        {
            var nonBlank = new List<KeyValuePair<bool, string>>(lines.Count);
            bool prevWasBlank = false;
            bool isFirst = true;

            foreach (var line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    prevWasBlank = true;
                    continue;
                }

                bool hadBlankAbove = !isFirst && prevWasBlank;
                nonBlank.Add(new KeyValuePair<bool, string>(hadBlankAbove,
                    line));
                prevWasBlank = false;
                isFirst = false;
            }

            var result = new List<string>(nonBlank.Count);

            for (int i = 0; i < nonBlank.Count; i++)
            {
                string line = nonBlank[i].Value;
                string trimmed = line.Trim();
                bool isBlockStart = IsBlockStartLine(trimmed);
                bool wantBlankAbove = false;

                if (result.Count > 0)
                {
                    string prevTrimmed = result[result.Count - 1].Trim();

                    if (isBlockStart && prevTrimmed.Length > 0 && prevTrimmed !=
                        "{" && !EndsWithOpenBrace(prevTrimmed))
                    {
                        wantBlankAbove = true;
                    }

                    if (!wantBlankAbove && IsBlockEndLine(prevTrimmed) &&
                        trimmed.Length > 0 && trimmed != "}" &&
                        !trimmed.StartsWith("}"))
                    {
                        wantBlankAbove = true;
                    }

                    if (!wantBlankAbove && IsImportDirective(trimmed) &&
                        IsImportDirective(prevTrimmed) && nonBlank[i].Key)
                    {
                        wantBlankAbove = true;
                    }

                    if (!wantBlankAbove && IsImportDirective(trimmed) &&
                        prevTrimmed.StartsWith("package "))
                    {
                        wantBlankAbove = true;
                    }
                }

                if (wantBlankAbove && trimmed.StartsWith("@"))
                {
                    wantBlankAbove = false;
                }

                if (wantBlankAbove && IsDoWhileTail(trimmed))
                {
                    wantBlankAbove = false;
                }

                if (wantBlankAbove && IsBlockContinuation(trimmed))
                {
                    wantBlankAbove = false;
                }

                if (wantBlankAbove)
                {
                    result.Add(string.Empty);
                }

                result.Add(line);
            }

            return result;
        }

        /// <summary>
        /// Collapses 3 or more consecutive blank lines into 1.
        /// </summary>
        private static List<string> CollapseBlankLines(List<string> lines)
        {
            var result = new List<string>(lines.Count);
            int blankRun = 0;

            foreach (var line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    blankRun++;

                    if (blankRun <= 1)
                    {
                        result.Add(string.Empty);
                    }
                }

                else
                {
                    blankRun = 0;
                    result.Add(line);
                }
            }

            return result;
        }

        /// <summary>
        /// Removes trailing whitespace from each line.
        /// </summary>
        private static List<string> TrimTrailingWhitespace(List<string> lines)
        {
            var result = new List<string>(lines.Count);

            foreach (var line in lines)
            {
                result.Add(line.TrimEnd());
            }

            return result;
        }

        /// <summary>
        /// Splits lines exceeding 80 characters at safe break points; continuation lines
        /// are indented one level deeper than the statement base indent.
        /// </summary>
        private static List<string> ApplyLineLengthLimit(List<string> lines)
        {
            var result = new List<string>(lines.Count);

            foreach (var line in lines)
            {
                if (line.Length <= MaxLineLength)
                {
                    result.Add(line);
                    continue;
                }

                var split = SplitLongLine(line);
                result.AddRange(split);
            }

            return result;
        }

        /// <summary>
        /// Recursively splits a single line so each segment does not exceed 80 characters;
        /// only breaks at Code token boundaries. Never breaks inside String/TextBlock/Char/Comment
        /// tokens. If no safe break point is found, the original line is preserved.
        /// </summary>
        private static List<string> SplitLongLine(string line)
        {
            if (line.Length <= MaxLineLength)
            {
                return new List<string> { line };
            }

            int indentLen = 0;

            while (indentLen < line.Length && line[indentLen] == ' ')
            {
                indentLen++;
            }

            if (indentLen >= line.Length)
            {
                return new List<string> { line };
            }

            string indent = line.Substring(0, indentLen);
            string contIndent = indent + new string(' ', IndentSize);
            var tokens = Tokenizer.Tokenize(line);
            bool[] isCode = Tokenizer.BuildCodeMask(line, tokens);
            int breakAt = FindSafeBreakPoint(line, isCode, indentLen);

            if (breakAt < 0 || breakAt >= line.Length)
            {
                return new List<string> { line };
            }

            string first = line.Substring(0, breakAt).TrimEnd();
            string rest = contIndent + line.Substring(breakAt).TrimStart();

            if (first.Length == 0 || first.Length >= line.Length)
            {
                return new List<string> { line };
            }

            var result = new List<string> { first };
            result.AddRange(SplitLongLine(rest));
            return result;
        }

        /// <summary>
        /// Finds a safe break point within Code tokens: prefers the latest break point
        /// that does not exceed 80 characters; if none, returns the first break point
        /// beyond 80 characters. Breaks after operators: , ; + - * / % == != &lt; &gt;
        /// &lt;= &gt;= = += -= &amp;&amp; ||. Does NOT break at . or -&gt;.
        /// </summary>
        private static int FindSafeBreakPoint(string line, bool[] isCode,
            int startIdx)
        {
            int bestInRange = -1;
            int firstOutOfRange = -1;
            int i = startIdx;

            while (i < line.Length)
            {
                if (!isCode[i])
                {
                    i++;
                    continue;
                }

                char c = line[i];
                int bp = -1;

                if (c == '=' && i + 1 < line.Length && line[i + 1] == '=')
                {
                    bp = i + 2;
                    i++;
                }

                else if (c == '!' && i + 1 < line.Length &&
                    line[i + 1] == '=')
                {
                    bp = i + 2;
                    i++;
                }

                else if (c == '<' && i + 1 < line.Length &&
                    line[i + 1] == '=')
                {
                    bp = i + 2;
                    i++;
                }

                else if (c == '>' && i + 1 < line.Length &&
                    line[i + 1] == '=')
                {
                    bp = i + 2;
                    i++;
                }

                else if (c == '+' && i + 1 < line.Length &&
                    line[i + 1] == '=')
                {
                    bp = i + 2;
                    i++;
                }

                else if (c == '-' && i + 1 < line.Length &&
                    line[i + 1] == '=')
                {
                    bp = i + 2;
                    i++;
                }

                else if (c == '&' && i + 1 < line.Length &&
                    line[i + 1] == '&')
                {
                    bp = i + 2;
                    i++;
                }

                else if (c == '|' && i + 1 < line.Length &&
                    line[i + 1] == '|')
                {
                    bp = i + 2;
                    i++;
                }

                else if (c == ',')
                {
                    bp = i + 1;
                }

                else if (c == ';')
                {
                    if (i + 1 < line.Length)
                    {
                        bp = i + 1;
                    }
                }

                else if (i > startIdx && IsBinaryOpContext(line, i,
                    startIdx) && (c == '+' || c == '-' || c == '*' ||
                    c == '/' || c == '%' || c == '<' || c == '>'))
                {
                    bp = i + 1;
                }

                else if (c == '=' && i > startIdx &&
                    IsBinaryOpContext(line, i, startIdx) &&
                    (i + 1 >= line.Length || (line[i + 1] != '=')))
                {
                    bp = i + 1;
                }

                if (bp > 0)
                {
                    if (bp <= MaxLineLength)
                    {
                        bestInRange = bp;
                    }

                    else if (firstOutOfRange < 0)
                    {
                        firstOutOfRange = bp;
                    }
                }

                i++;
            }

            if (bestInRange > 0)
            {
                return bestInRange;
            }

            return firstOutOfRange;
        }

        /// <summary>
        /// Determines whether line[i] is in a binary operator context: the previous
        /// non-whitespace character is ), ], an identifier character, _, or ".
        /// Used to exclude unary operators and generic type parameters.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="i">The current operator position.</param>
        /// <param name="startIdx">The scan start position.</param>
        /// <returns>True if in a binary operator context; otherwise false.</returns>
        private static bool IsBinaryOpContext(string line, int i, int startIdx)
        {
            int prev = i - 1;

            while (prev >= startIdx && line[prev] == ' ')
            {
                prev--;
            }

            if (prev < startIdx)
            {
                return false;
            }

            char pc = line[prev];
            return pc == ')' || pc == ']' || char.IsLetterOrDigit(pc) ||
                pc == '_' || pc == '"';
        }

        /// <summary>
        /// Ensures the file ends with exactly one newline.
        /// </summary>
        private static string EnsureSingleTrailingNewline(string text)
        {
            string trimmed = text.TrimEnd('\n', '\r');
            return trimmed + "\n";
        }

        private static bool MatchesWord(string text, int pos, string word)
        {
            if (pos + word.Length > text.Length)
            {
                return false;
            }

            for (int i = 0; i < word.Length; i++)
            {
                if (text[pos + i] != word[i])
                {
                    return false;
                }
            }

            if (pos + word.Length < text.Length && IsWordChar(text[pos +
                word.Length]))
            {
                return false;
            }

            return true;
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private static int SkipWhitespace(string text, int pos)
        {
            while (pos < text.Length && (text[pos] == ' ' || text[pos] == '\t'
                || text[pos] == '\n' || text[pos] == '\r'))
            {
                pos++;
            }

            return pos;
        }

        /// <summary>
        /// Skips a balanced pair of parentheses starting at the given position,
        /// returning the position after the closing parenthesis; or -1 if not found.
        /// </summary>
        private static int SkipParen(string text, bool[] isCode, int start)
        {
            int i = SkipWhitespace(text, start);

            if (i >= text.Length || !isCode[i] || text[i] != '(')
            {
                return -1;
            }

            int depth = 1;
            i++;

            while (i < text.Length && depth > 0)
            {
                if (isCode[i])
                {
                    if (text[i] == '(')
                    {
                        depth++;
                    }

                    else if (text[i] == ')')
                    {
                        depth--;
                    }
                }

                if (depth > 0)
                {
                    i++;
                }
            }

            if (depth != 0)
            {
                return -1;
            }

            return i + 1;
        }

        private static int FindOpenBrace(string text, bool[] isCode, int start)
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

        private static bool IsBlockStartLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed == "{")
            {
                return false;
            }

            if (trimmed.EndsWith(";"))
            {
                return false;
            }

            if (trimmed.StartsWith("@"))
            {
                return false;
            }

            string[] keywords =
            {
                "package", "interface", "synchronized", "finally", "abstract",
                "implements", "extends", "throws", "class", "switch", "catch",
                "enum", "while", "else", "for", "try", "do", "if"
            };

            foreach (var kw in keywords)
            {
                if (StartsWithKeyword(trimmed, kw))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool StartsWithKeyword(string s, string kw)
        {
            if (!s.StartsWith(kw))
            {
                return false;
            }

            if (s.Length == kw.Length)
            {
                return true;
            }

            char next = s[kw.Length];
            return !char.IsLetterOrDigit(next) && next != '_';
        }

        private static bool EndsWithOpenBrace(string s)
        {
            string t = s.TrimEnd();
            return t.Length > 0 && t[t.Length - 1] == '{';
        }

        /// <summary>
        /// Determines whether the trimmed line is a block end line: exactly } or };.
        /// </summary>
        private static bool IsBlockEndLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed == "}")
            {
                return true;
            }

            if (trimmed == "};")
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the trimmed line is an import directive.
        /// </summary>
        private static bool IsImportDirective(string trimmed)
        {
            if (trimmed.StartsWith("import "))
            {
                return true;
            }

            if (trimmed.StartsWith("import\t"))
            {
                return true;
            }

            return trimmed == "import";
        }

        /// <summary>
        /// Determines whether the trimmed line is a do-while tail: starts with the
        /// "while" keyword and ends with ");".
        /// </summary>
        private static bool IsDoWhileTail(string trimmed)
        {
            if (!StartsWithKeyword(trimmed, "while"))
            {
                return false;
            }

            return trimmed.EndsWith(");");
        }

        /// <summary>
        /// Determines whether the trimmed line is a block continuation keyword:
        /// catch, finally, or else.
        /// </summary>
        private static bool IsBlockContinuation(string trimmed)
        {
            return StartsWithKeyword(trimmed, "catch") ||
                StartsWithKeyword(trimmed, "finally") ||
                StartsWithKeyword(trimmed, "else");
        }

        private struct Insertion
        {
            public int Position;
            public string Text;
            public Insertion(int position, string text)
            {
                Position = position;
                Text = text;
            }
        }

        private struct Replacement
        {
            public int Start;
            public int End;
            public string NewText;
            public Replacement(int start, int end, string newText)
            {
                Start = start;
                End = end;
                NewText = newText;
            }
        }
    }
}
