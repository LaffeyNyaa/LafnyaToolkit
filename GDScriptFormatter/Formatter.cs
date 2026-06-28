using System.Collections.Generic;
using System.Text;

namespace GDScriptFormatter
{
    /// <summary>
    /// Core implementation that applies all GDScript formatting rules.
    /// </summary>
    internal static class Formatter
    {
        /// <summary>4 spaces per indentation level.</summary>
        private const int IndentSize = 4;
        /// <summary>Maximum line length.</summary>
        private const int MaxLineLength = 80;

        /// <summary>
        /// Applies all formatting rules to a source string and returns the result.
        /// </summary>
        /// <param name="source">The original source string.</param>
        /// <returns>The formatted source string.</returns>
        public static string Format(string source)
        {
            var tokens = Tokenizer.Tokenize(source);
            string text = Tokenizer.Reconstruct(tokens);
            text = ExpandEnums(text);
            text = text.Replace("\t", "    ");
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
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
        /// Expands a single-line enum so each member occupies its own line, with a trailing comma after the last member.
        /// </summary>
        private static string ExpandEnums(string text)
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

                int afterEnum = i + 4;

                if (afterEnum < text.Length && IsWordChar(text[afterEnum]))
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
                    sb.Append(new string(' ', IndentSize));
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
        /// Splits text into lines.
        /// </summary>
        private static List<string> SplitLines(string text)
        {
            return new List<string>(text.Split('\n'));
        }

        /// <summary>
        /// Colon-based indentation recalculation: infers block depth stack from original indentation,
        /// colon-terminated code lines (not inside brackets) open a new block, bracket depth &gt; 0 or
        /// previous line ending with \ indicates a continuation line (indented one extra level). Lines inside
        /// triple-quoted strings preserve their original indentation.
        /// </summary>
        private static List<string> Reindent(List<string> lines, string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            bool[] preserveIndent = ComputePreserveIndent(lines, tokens);
            var lineStarts = ComputeLineStarts(lines);
            var lineInfo = ComputeLineInfo(lines, text, isCode, lineStarts);
            int[] depths = ComputeDepthsFromStack(lines, lineInfo);

            var result = new List<string>(lines.Count);

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

                if (lineInfo[i].IsContinuation && baseDepth > 0)
                {
                    baseDepth++;
                }

                result.Add(new string(' ', baseDepth * IndentSize) + content);
            }

            return result;
        }

        /// <summary>
        /// Computes the starting offset of each line.
        /// </summary>
        private static int[] ComputeLineStarts(List<string> lines)
        {
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

            return lineStarts;
        }

        /// <summary>
        /// Per-line analysis information: whether the line ends with colon/brace, whether it is a continuation, and its original indentation depth.
        /// </summary>
        private struct LineAnalysis
        {
            /// <summary>Whether the line ends with a colon (not inside brackets).</summary>
            public bool ColonTerminated;
            /// <summary>Whether the line ends with { (not inside a string/comment) and the brace is not closed on the same line.</summary>
            public bool BraceTerminated;
            /// <summary>Whether the line starts with } (close-brace line).</summary>
            public bool IsCloseBrace;
            /// <summary>Whether the line is a continuation (bracket depth &gt; 0 or the previous line ended with \).</summary>
            public bool IsContinuation;
            /// <summary>The line's original indentation level (leading spaces / IndentSize).</summary>
            public int OriginalDepth;
        }

        /// <summary>
        /// Analyzes per-line properties: colon/brace termination, continuation, original indentation depth.
        /// Continuation detection is based only on parentheses and square bracket depth (brace depth does not affect continuation,
        /// because brace lines are handled via the stack through the BraceTerminated flag).
        /// </summary>
        private static LineAnalysis[] ComputeLineInfo(List<string> lines,
            string text, bool[] isCode, int[] lineStarts)
        {
            var info = new LineAnalysis[lines.Count];
            int parenBracketDepth = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                int leadingSpaces = line.Length - trimmed.Length;
                int origDepth = leadingSpaces / IndentSize;

                info[i].OriginalDepth = origDepth;
                info[i].IsContinuation = parenBracketDepth > 0;

                if (i > 0 && EndsWithBackslash(lines[i - 1]))
                {
                    info[i].IsContinuation = true;
                }

                info[i].ColonTerminated = false;
                info[i].BraceTerminated = false;
                info[i].IsCloseBrace = false;

                if (trimmed.Length > 0)
                {
                    int firstCodeIdx = -1;
                    int lastCodeIdx = -1;
                    int lineEnd = lineStarts[i] + line.Length;

                    for (int ci = lineStarts[i]; ci < lineEnd &&
                        ci < isCode.Length; ci++)
                    {
                        if (isCode[ci])
                        {
                            if (firstCodeIdx < 0)
                            {
                                firstCodeIdx = ci;
                            }

                            lastCodeIdx = ci;
                        }
                    }

                    if (firstCodeIdx >= 0 && text[firstCodeIdx] == '}')
                    {
                        info[i].IsCloseBrace = true;
                    }

                    if (lastCodeIdx >= 0 && parenBracketDepth == 0)
                    {
                        if (text[lastCodeIdx] == ':')
                        {
                            info[i].ColonTerminated = true;
                        }
                    }

                    if (lastCodeIdx >= 0 && text[lastCodeIdx] == '{')
                    {
                        info[i].BraceTerminated = true;
                    }
                }

                for (int ci = lineStarts[i];
                    ci < lineStarts[i] + line.Length && ci < isCode.Length;
                    ci++)
                {
                    if (!isCode[ci])
                    {
                        continue;
                    }

                    char c = text[ci];

                    if (c == '(' || c == '[')
                    {
                        parenBracketDepth++;
                    }
                    else if (c == ')' || c == ']')
                    {
                        if (parenBracketDepth > 0)
                        {
                            parenBracketDepth--;
                        }
                    }
                }
            }

            return info;
        }

        /// <summary>
        /// Stack-based indentation computation from original indentation depth: colon-terminated lines and brace-terminated lines
        /// open a new block, indenting subsequent lines by +1; close-brace lines and returning to shallower indentation pop blocks.
        /// </summary>
        private static int[] ComputeDepthsFromStack(List<string> lines,
            LineAnalysis[] lineInfo)
        {
            int[] depths = new int[lines.Count];
            var stack = new List<int>();

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();

                if (trimmed.Length == 0)
                {
                    depths[i] = stack.Count;
                    continue;
                }

                int origDepth = lineInfo[i].OriginalDepth;

                if (lineInfo[i].IsCloseBrace)
                {
                    if (stack.Count > 0)
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }

                    depths[i] = stack.Count;
                    continue;
                }

                while (stack.Count > 0 && origDepth < stack[stack.Count - 1])
                {
                    stack.RemoveAt(stack.Count - 1);
                }

                depths[i] = stack.Count;

                if (lineInfo[i].ColonTerminated ||
                    lineInfo[i].BraceTerminated)
                {
                    stack.Add(stack.Count + 1);
                }
            }

            return depths;
        }

        /// <summary>
        /// Determines whether each line is inside a triple-quoted string (non-first line), where original indentation must be preserved.
        /// </summary>
        private static bool[] ComputePreserveIndent(List<string> lines,
            List<Token> tokens)
        {
            var preserveIndent = new bool[lines.Count];
            var lineStarts = ComputeLineStarts(lines);
            int tokenPos = 0;

            foreach (var token in tokens)
            {
                int tokenStart = tokenPos;
                int tokenEnd = tokenPos + token.Text.Length;

                if (token.Kind == TokenKind.TripleString)
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
        /// Ensures the correct number of blank lines above and below blocks/declarations, including:
        /// - one blank line above and below code blocks and multi-line statements
        /// - two blank lines above and below func/nested class declarations (only at the same indentation depth)
        /// - one blank line after file-level header lines
        /// - one blank line between different variable groups
        /// - comments attached to the following declaration
        /// </summary>
        private static List<string> ApplyBlankLineRules(List<string> lines)
        {
            var nonBlank = new List<NonBlankEntry>(lines.Count);
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
                int indent = LineIndentLevel(line);
                nonBlank.Add(new NonBlankEntry(hadBlankAbove, line, indent));
                prevWasBlank = false;
                isFirst = false;
            }

            var result = new List<string>(nonBlank.Count);
            var resultIndents = new List<int>(nonBlank.Count);

            for (int i = 0; i < nonBlank.Count; i++)
            {
                string line = nonBlank[i].Line;
                string trimmed = line.Trim();
                int lineIndent = nonBlank[i].Indent;
                int wantBlankAbove = 0;

                if (result.Count > 0)
                {
                    string prevTrimmed = result[result.Count - 1].Trim();
                    int prevIndent = resultIndents[resultIndents.Count - 1];
                    wantBlankAbove = ComputeDesiredBlanksAbove(
                        prevTrimmed, trimmed, nonBlank, i,
                        prevIndent, lineIndent);
                }

                int currentBlanksAbove = CountTrailingBlanks(result);

                while (currentBlanksAbove < wantBlankAbove)
                {
                    result.Add(string.Empty);
                    resultIndents.Add(-1);
                    currentBlanksAbove++;
                }

                while (currentBlanksAbove > wantBlankAbove)
                {
                    result.RemoveAt(result.Count - 1);
                    resultIndents.RemoveAt(resultIndents.Count - 1);
                    currentBlanksAbove--;
                }

                result.Add(line);
                resultIndents.Add(lineIndent);
            }

            return result;
        }

        /// <summary>
        /// Computes the indentation level of a line (leading spaces / IndentSize).
        /// </summary>
        private static int LineIndentLevel(string line)
        {
            int spaces = 0;

            while (spaces < line.Length && line[spaces] == ' ')
            {
                spaces++;
            }

            return spaces / IndentSize;
        }

        /// <summary>
        /// Computes how many blank lines should appear above the current line.
        /// </summary>
        private static int ComputeDesiredBlanksAbove(string prevTrimmed,
            string curTrimmed, List<NonBlankEntry> nonBlank, int curIdx,
            int prevIndent, int curIndent)
        {
            if (curTrimmed.Length == 0)
            {
                return 0;
            }

            if (prevTrimmed.Length == 0)
            {
                return 0;
            }

            if (IsAttachedComment(prevTrimmed, curTrimmed, nonBlank, curIdx))
            {
                return 0;
            }

            bool sameIndent = prevIndent == curIndent;
            bool deeperThanPrev = curIndent > prevIndent;
            bool shallowerThanPrev = curIndent < prevIndent;
            int want = 0;

            if (sameIndent && IsFuncOrClassDecl(curTrimmed))
            {
                want = 2;
            }
            else if (sameIndent && IsFuncOrClassDecl(prevTrimmed) &&
                !IsFuncOrClassDecl(curTrimmed))
            {
                want = 2;
            }
            else if (IsBlockStartLine(curTrimmed) && !IsSameGroup(
                prevTrimmed, curTrimmed) && sameIndent)
            {
                want = 1;
            }
            else if (IsBlockStartLine(curTrimmed) && !deeperThanPrev &&
                prevTrimmed.Length > 0 && prevTrimmed != ":" &&
                !EndsWithColon(prevTrimmed))
            {
                want = 1;
            }

            if (want == 0 && sameIndent && IsTopLevelMember(prevTrimmed) &&
                IsTopLevelMember(curTrimmed) &&
                !IsSameGroup(prevTrimmed, curTrimmed))
            {
                want = 1;
            }

            if (want == 0 && IsFileHeaderLine(prevTrimmed) &&
                !IsFileHeaderLine(curTrimmed) && !deeperThanPrev)
            {
                want = 1;
            }

            return want;
        }

        /// <summary>
        /// Determines whether a preceding comment line is attached to the current declaration (originally no blank line between them).
        /// </summary>
        private static bool IsAttachedComment(string prevTrimmed,
            string curTrimmed, List<NonBlankEntry> nonBlank, int curIdx)
        {
            if (!prevTrimmed.StartsWith("#"))
            {
                return false;
            }

            if (!IsDeclarationLine(curTrimmed))
            {
                return false;
            }

            if (!nonBlank[curIdx].HadBlankAbove)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a line is a declaration line (func/class/signal/enum/const/var/annotation).
        /// </summary>
        private static bool IsDeclarationLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (StartsWithKeyword(trimmed, "func"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "class ") &&
                !StartsWithKeyword(trimmed, "class_name"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "signal"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "enum"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "const"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "var"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "static"))
            {
                return true;
            }

            if (trimmed.StartsWith("@"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a line is a file-level header line (@tool/@icon/@static_unload/class_name/extends/## doc).
        /// </summary>
        private static bool IsFileHeaderLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (StartsWithKeyword(trimmed, "@tool"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "@icon"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "@static_unload"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "class_name"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "extends"))
            {
                return true;
            }

            if (trimmed.StartsWith("##"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a line is a func or nested class declaration.
        /// </summary>
        private static bool IsFuncOrClassDecl(string trimmed)
        {
            if (StartsWithKeyword(trimmed, "func"))
            {
                return true;
            }

            if (trimmed.StartsWith("class ") && !trimmed.StartsWith("class_name"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a line is a block-start line (a code line ending with a colon).
        /// </summary>
        private static bool IsBlockStartLine(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (EndsWithColon(trimmed))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a line ends with a colon (excluding colons inside strings/comments — the caller
        /// already validates with code mask in Reindent; this is a text heuristic only).
        /// </summary>
        private static bool EndsWithColon(string s)
        {
            string t = s.TrimEnd();

            if (t.Length == 0)
            {
                return false;
            }

            if (t[t.Length - 1] == ':')
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a line is a top-level class member (signal/enum/const/var/func/static/@export/@onready).
        /// </summary>
        private static bool IsTopLevelMember(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (StartsWithKeyword(trimmed, "signal"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "enum"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "const"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "static") &&
                trimmed.Contains("var"))
            {
                return true;
            }

            if (trimmed.StartsWith("@export"))
            {
                return true;
            }

            if (trimmed.StartsWith("@onready"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "var"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "func"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether two top-level members belong to the same variable group.
        /// </summary>
        private static bool IsSameGroup(string a, string b)
        {
            return ClassifyMember(a) == ClassifyMember(b);
        }

        /// <summary>
        /// Classifies a top-level member into a group (first-match-wins).
        /// </summary>
        private static int ClassifyMember(string trimmed)
        {
            if (StartsWithKeyword(trimmed, "signal"))
            {
                return 0;
            }

            if (StartsWithKeyword(trimmed, "enum"))
            {
                return 1;
            }

            if (StartsWithKeyword(trimmed, "const"))
            {
                return 2;
            }

            if (StartsWithKeyword(trimmed, "static") &&
                trimmed.Contains("var"))
            {
                return 3;
            }

            if (trimmed.StartsWith("@export"))
            {
                return 4;
            }

            if (trimmed.StartsWith("@onready"))
            {
                return 5;
            }

            string name = ExtractMemberName(trimmed);

            if (name.StartsWith("_"))
            {
                return 6;
            }

            return 7;
        }

        /// <summary>
        /// Extracts the member name from a member declaration.
        /// </summary>
        private static string ExtractMemberName(string trimmed)
        {
            if (trimmed.StartsWith("var "))
            {
                return ExtractNameAfter(trimmed, "var ");
            }

            if (trimmed.StartsWith("func "))
            {
                return ExtractNameAfter(trimmed, "func ");
            }

            if (trimmed.StartsWith("signal "))
            {
                return ExtractNameAfter(trimmed, "signal ");
            }

            if (trimmed.StartsWith("const "))
            {
                return ExtractNameAfter(trimmed, "const ");
            }

            if (trimmed.StartsWith("@"))
            {
                int spaceIdx = trimmed.IndexOf(' ');

                if (spaceIdx >= 0 && spaceIdx + 1 < trimmed.Length)
                {
                    string rest = trimmed.Substring(spaceIdx + 1);

                    if (rest.StartsWith("var "))
                    {
                        return ExtractNameAfter(rest, "var ");
                    }

                    if (rest.StartsWith("func "))
                    {
                        return ExtractNameAfter(rest, "func ");
                    }
                }
            }

            return "";
        }

        /// <summary>
        /// Extracts NAME from a string of the form "keyword NAME".
        /// </summary>
        private static string ExtractNameAfter(string s, string prefix)
        {
            int start = prefix.Length;

            while (start < s.Length && s[start] == ' ')
            {
                start++;
            }

            int end = start;

            while (end < s.Length && IsWordChar(s[end]))
            {
                end++;
            }

            if (end > start)
            {
                return s.Substring(start, end - start);
            }

            return "";
        }

        /// <summary>
        /// Counts the number of consecutive blank lines at the end of result.
        /// </summary>
        private static int CountTrailingBlanks(List<string> result)
        {
            int count = 0;

            for (int j = result.Count - 1; j >= 0; j--)
            {
                if (result[j].Trim().Length == 0)
                {
                    count++;
                }
                else
                {
                    break;
                }
            }

            return count;
        }

        /// <summary>
        /// Collapses runs of 3 or more consecutive blank lines into 2 (func/class context) or 1.
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

                    if (blankRun <= 2)
                    {
                        result.Add(string.Empty);
                    }
                }
                else
                {
                    if (blankRun > 2)
                    {
                        while (result.Count > 0 &&
                            result[result.Count - 1].Trim().Length == 0)
                        {
                            result.RemoveAt(result.Count - 1);
                        }

                        string trimmed = line.Trim();
                        bool nearFuncClass = IsFuncOrClassDecl(trimmed);

                        if (!nearFuncClass && result.Count > 0)
                        {
                            string prevTrim = result[result.Count - 1].Trim();

                            if (IsFuncOrClassDecl(prevTrim))
                            {
                                nearFuncClass = true;
                            }
                        }

                        result.Add(string.Empty);

                        if (nearFuncClass)
                        {
                            result.Add(string.Empty);
                        }
                    }

                    blankRun = 0;
                    result.Add(line);
                }
            }

            return result;
        }

        /// <summary>
        /// Trims trailing whitespace from each line.
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
        /// Splits lines exceeding 80 characters: split after commas inside already-open brackets;
        /// for assignment statements, wrap the RHS in (...) then split; leave the line unchanged if no safe split point is found.
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
        /// Recursively splits a line so each segment is at most 80 characters.
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

            int bracketDepth = 0;
            bool hasUnclosedBracket = false;

            for (int ci = indentLen; ci < line.Length; ci++)
            {
                if (!isCode[ci])
                {
                    continue;
                }

                char c = line[ci];

                if (c == '(' || c == '[' || c == '{')
                {
                    bracketDepth++;
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (bracketDepth > 0)
                    {
                        bracketDepth--;
                    }
                }
            }

            if (bracketDepth > 0)
            {
                hasUnclosedBracket = true;
            }

            if (hasUnclosedBracket)
            {
                int breakAt = FindCommaBreakInBrackets(line, isCode, indentLen);

                if (breakAt > 0 && breakAt < line.Length)
                {
                    string first = line.Substring(0, breakAt).TrimEnd();
                    string rest = contIndent +
                        line.Substring(breakAt).TrimStart();

                    if (first.Length > 0 && first.Length < line.Length)
                    {
                        var res = new List<string> { first };
                        res.AddRange(SplitLongLine(rest));
                        return res;
                    }
                }
            }

            int eqPos = FindTopLevelEquals(line, isCode, indentLen);

            if (eqPos >= 0)
            {
                string beforeEq = line.Substring(0, eqPos).TrimEnd();
                string afterEq = line.Substring(eqPos + 1).TrimStart();

                if (afterEq.Length > 0 && !afterEq.StartsWith("("))
                {
                    string firstLine = beforeEq + " = (";
                    string rhsCont = contIndent + afterEq;
                    string closeLine = indent + ")";

                    var rhsSplit = SplitLongLine(rhsCont);
                    var res2 = new List<string> { firstLine };
                    res2.AddRange(rhsSplit);
                    res2.Add(closeLine);
                    return res2;
                }

                if (afterEq.StartsWith("("))
                {
                    int breakAt2 = FindCommaBreakInBrackets(
                        line, isCode, eqPos + 1);

                    if (breakAt2 > 0 && breakAt2 < line.Length)
                    {
                        string first2 = line.Substring(0, breakAt2).TrimEnd();
                        string rest2 = contIndent +
                            line.Substring(breakAt2).TrimStart();

                        if (first2.Length > 0 && first2.Length < line.Length)
                        {
                            var res3 = new List<string> { first2 };
                            res3.AddRange(SplitLongLine(rest2));
                            return res3;
                        }
                    }
                }
            }

            return new List<string> { line };
        }

        /// <summary>
        /// Finds a safe break point after a comma inside brackets.
        /// </summary>
        private static int FindCommaBreakInBrackets(string line,
            bool[] isCode, int startIdx)
        {
            int best = -1;
            int depth = 0;

            for (int i = startIdx; i < line.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = line[i];

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
                }
                else if (c == ',' && depth > 0)
                {
                    int bp = i + 1;

                    if (bp <= MaxLineLength)
                    {
                        best = bp;
                    }
                    else if (best < 0)
                    {
                        best = bp;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Finds the position of a top-level (outside brackets) assignment equals sign in a line (excluding ==, !=, &lt;=, &gt;=).
        /// </summary>
        private static int FindTopLevelEquals(string line, bool[] isCode,
            int startIdx)
        {
            int depth = 0;

            for (int i = startIdx; i < line.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = line[i];

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
                }
                else if (c == '=' && depth == 0)
                {
                    if (i > 0 && isCode[i - 1])
                    {
                        char prev = line[i - 1];

                        if (prev == '=' || prev == '!' || prev == '<' ||
                            prev == '>' || prev == '+' || prev == '-' ||
                            prev == '*' || prev == '/')
                        {
                            continue;
                        }
                    }

                    if (i + 1 < line.Length && isCode[i + 1] &&
                        line[i + 1] == '=')
                    {
                        continue;
                    }

                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Ensures the file ends with exactly one newline character.
        /// </summary>
        private static string EnsureSingleTrailingNewline(string text)
        {
            string trimmed = text.TrimEnd('\n', '\r');
            return trimmed + "\n";
        }

        /// <summary>
        /// Determines whether a line ends with a backslash (continuation marker).
        /// </summary>
        private static bool EndsWithBackslash(string line)
        {
            string t = line.TrimEnd();

            if (t.Length > 0 && t[t.Length - 1] == '\\')
            {
                if (t.Length >= 2 && t[t.Length - 2] == '\\')
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the text at the given position matches the specified word (must be preceded/followed by non-word characters or boundaries).
        /// </summary>
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

            if (pos + word.Length < text.Length &&
                IsWordChar(text[pos + word.Length]))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether a character is a word character (letter, digit, underscore).
        /// </summary>
        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        /// <summary>
        /// Skips whitespace characters and returns the next non-whitespace position.
        /// </summary>
        private static int SkipWhitespace(string text, int pos)
        {
            while (pos < text.Length &&
                (text[pos] == ' ' || text[pos] == '\t' ||
                text[pos] == '\n' || text[pos] == '\r'))
            {
                pos++;
            }

            return pos;
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
        /// Determines whether a string starts with the specified keyword (followed by a non-word character or end of string).
        /// </summary>
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

        /// <summary>
        /// Non-blank line entry: records whether there was a blank line above in the original and the indentation level.
        /// </summary>
        private struct NonBlankEntry
        {
            /// <summary>Whether a blank line existed above this line in the original input.</summary>
            public bool HadBlankAbove;
            /// <summary>The line text.</summary>
            public string Line;
            /// <summary>The indentation level.</summary>
            public int Indent;

            public NonBlankEntry(bool hadBlankAbove, string line, int indent)
            {
                HadBlankAbove = hadBlankAbove;
                Line = line;
                Indent = indent;
            }
        }

        /// <summary>
        /// Replacement entry: replaces [Start, End) with NewText.
        /// </summary>
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
