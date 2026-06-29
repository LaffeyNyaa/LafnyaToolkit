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
        /// Applies all formatting rules to a source string and returns the result. Line endings are
        /// normalized first, then tabs are normalized only in Code regions, then enums are expanded,
        /// and finally the tokenization is reused for re-indentation and line-length splitting.
        /// </summary>
        /// <param name="source">The original source string.</param>
        /// <returns>The formatted source string.</returns>
        public static string Format(string source)
        {
            if (source == null || source.Length == 0)
            {
                return source ?? string.Empty;
            }

            string text = source.Replace("\r\n", "\n").Replace("\r", "\n");

            var tabTokens = Tokenizer.Tokenize(text);
            bool[] tabMask = Tokenizer.BuildCodeMask(text, tabTokens);
            text = NormalizeTabs(text, tabMask);

            text = ExpandEnums(text);

            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);

            var lines = SplitLines(text);
            lines = Reindent(lines, text, tokens, isCode);
            // Compute continuation flags from the post-Reindent (pre-split)
            // line structure so that LineLengthProcessor can detect
            // continuation lines and avoid cascading indents when splitting
            // them (a continuation line split at parent+4 must keep its
            // segments at parent+4, not parent+8).
            string textForLimit = string.Join("\n", lines);
            var tokensForLimit = Tokenizer.Tokenize(textForLimit);
            bool[] isCodeForLimit = Tokenizer.BuildCodeMask(textForLimit,
                tokensForLimit);
            int[] lineStartsForLimit = ComputeLineStarts(lines);
            var lineInfoForLimit = ComputeLineInfo(lines, textForLimit,
                isCodeForLimit, lineStartsForLimit);
            var preSplitContinues = new bool[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                // Line i "continues to next" when line i+1 is detected as a
                // continuation (unclosed bracket from line i, or line i ends
                // with a continuation backslash).
                preSplitContinues[i] = i + 1 < lines.Count &&
                    lineInfoForLimit[i + 1].IsContinuation;
            }
            // Split long lines BEFORE applying blank-line rules so that the
            // preSplitContinues flags (computed above) stay aligned with the
            // line list. Running BlankLineProcessor first would insert blank
            // lines and shift indices, causing LineLengthProcessor to read
            // the wrong continuation flag for each line.
            lines = ApplyLineLengthLimit(lines, preSplitContinues);
            lines = ApplyBlankLineRules(lines);
            lines = CollapseBlankLines(lines);
            lines = TrimTrailingWhitespace(lines);
            string result = string.Join("\n", lines);
            result = EnsureSingleTrailingNewline(result);
            return result;
        }

        /// <summary>
        /// Replaces tabs with 4 spaces only at Code-region positions, preserving tabs inside string
        /// literals and comments so that string contents are never modified.
        /// </summary>
        /// <param name="text">The normalized text.</param>
        /// <param name="isCode">The code mask of the text.</param>
        /// <returns>The text with Code-region tabs expanded to 4 spaces.</returns>
        private static string NormalizeTabs(string text, bool[] isCode)
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
        /// triple-quoted strings preserve their original indentation. Reuses the caller-provided tokens
        /// and code mask instead of re-tokenizing.
        /// </summary>
        /// <param name="lines">The input lines.</param>
        /// <param name="text">The full text corresponding to the lines.</param>
        /// <param name="tokens">The tokenization of text (reused).</param>
        /// <param name="isCode">The code mask of text (reused).</param>
        /// <returns>The re-indented lines.</returns>
        private static List<string> Reindent(List<string> lines, string text,
            List<Token> tokens, bool[] isCode)
        {
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
        /// Continuation detection is based on parenthesis, square bracket, and brace depth. A line
        /// ending with a trailing { (BraceTerminated) does NOT increment the bracket depth — its
        /// body is indented via the stack — so that block-style dicts are not double-indented.
        /// Inline-open dicts/braces (e.g. "var m = {k: v,") DO increment the depth so that
        /// subsequent continuation lines are detected and preserved as continuations.
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

                if (i > 0 && EndsWithBackslash(text, isCode,
                    lineStarts[i - 1], lines[i - 1].Length))
                {
                    info[i].IsContinuation = true;
                }

                info[i].ColonTerminated = false;
                info[i].BraceTerminated = false;
                info[i].IsCloseBrace = false;

                int firstCodeIdx = -1;
                int lastCodeIdx = -1;

                if (trimmed.Length > 0)
                {
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

                    // Skip the trailing { on BraceTerminated lines — it is
                    // handled by the stack via BraceTerminated, so counting
                    // it here would double-indent the body of a block-style
                    // dict.
                    if (info[i].BraceTerminated && ci == lastCodeIdx)
                    {
                        continue;
                    }

                    if (c == '(' || c == '[' || c == '{')
                    {
                        parenBracketDepth++;
                    }
                    else if (c == ')' || c == ']' || c == '}')
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
        /// Determines whether a preceding comment line is attached to the current declaration.
        /// Doc comment lines (starting with ##) are always force-attached to a following declaration
        /// regardless of whether a blank line originally separated them. Single-# comments are
        /// attached only when no blank line originally separated them.
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

            if (prevTrimmed.StartsWith("##"))
            {
                return true;
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
                (trimmed.Contains("var") || trimmed.Contains("func")))
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

            if (StartsWithKeyword(trimmed, "static var"))
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
        /// Extracts the member name from a member declaration. Handles static-prefixed declarations
        /// (static var, static func) by stripping the leading "static " before applying the keyword rules.
        /// </summary>
        private static string ExtractMemberName(string trimmed)
        {
            if (trimmed.StartsWith("static "))
            {
                string rest = trimmed.Substring("static ".Length).TrimStart();

                if (rest.StartsWith("var "))
                {
                    return ExtractNameAfter(rest, "var ");
                }

                if (rest.StartsWith("func "))
                {
                    return ExtractNameAfter(rest, "func ");
                }
            }

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
        /// <paramref name="lineContinuesNext"/> flags whether each line ends with a continuation
        /// indicator; when a line is itself a continuation of the previous line, its split
        /// segments reuse the line's current indent (no extra level) so that splitting a
        /// continuation line does not cascade into deeper indents on a second pass.
        /// </summary>
        /// <param name="lines">The current lines.</param>
        /// <param name="lineContinuesNext">Per-line flags indicating whether the line ends with
        /// a continuation indicator; entry i corresponds to line i. May be null when
        /// continuation detection is not available.</param>
        /// <returns>The lines with long lines split.</returns>
        private static List<string> ApplyLineLengthLimit(List<string> lines,
            bool[] lineContinuesNext)
        {
            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.Length <= MaxLineLength)
                {
                    result.Add(line);
                    continue;
                }

                // If this line is itself a continuation of the previous line
                // (previous line ends with a continuation indicator), the
                // continuation indent equals this line's current indent — do
                // NOT add another indent level. Otherwise, continuation
                // segments are indented one level deeper than the statement
                // base indent (handled by passing null to SplitLongLine).
                bool isContinuation = lineContinuesNext != null &&
                    i > 0 && i - 1 < lineContinuesNext.Length &&
                    lineContinuesNext[i - 1];
                string fixedContIndent;
                if (isContinuation)
                {
                    int indentLen = 0;
                    while (indentLen < line.Length &&
                        line[indentLen] == ' ')
                    {
                        indentLen++;
                    }
                    fixedContIndent = line.Substring(0, indentLen);
                }
                else
                {
                    fixedContIndent = null;
                }

                var split = SplitLongLine(line, fixedContIndent);
                result.AddRange(split);
            }

            return result;
        }

        /// <summary>
        /// Recursively splits a line so each segment is at most 80 characters. Splitting priority:
        /// unclosed-bracket comma split; closed-bracket comma split (commas inside already-balanced
        /// brackets); top-level equals wrapping; otherwise leave the line unchanged.
        /// <paramref name="fixedContIndent"/> is the fixed continuation indent reused across all
        /// continuation segments so that 3+ segment splits do not cascade; pass null on the first
        /// call to trigger computation from the original line's indent.
        /// </summary>
        /// <param name="line">The line to split.</param>
        /// <param name="fixedContIndent">The fixed continuation indent, or null to compute from
        /// the line's indent on the first split.</param>
        /// <returns>The list of split segments.</returns>
        private static List<string> SplitLongLine(string line,
            string fixedContIndent)
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
            // On the first call (fixedContIndent == null), compute the fixed
            // continuation indent from the original line's indent. This indent
            // is reused for ALL continuation segments so that 3+ segment
            // splits do not cascade (parent+4 for every continuation line,
            // matching Reindent's behaviour for continuation lines).
            string contIndent = fixedContIndent ?? (indent +
                new string(' ', IndentSize));
            var tokens = Tokenizer.Tokenize(line);
            bool[] isCode = Tokenizer.BuildCodeMask(line, tokens);

            int bracketDepth = 0;

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

            bool hasUnclosedBracket = bracketDepth > 0;

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
                        res.AddRange(SplitLongLine(rest, contIndent));
                        return res;
                    }
                }
            }

            if (!hasUnclosedBracket)
            {
                int breakAt = FindCommaBreakInBrackets(line, isCode, indentLen);

                if (breakAt > 0 && breakAt < line.Length)
                {
                    string first = line.Substring(0, breakAt).TrimEnd();

                    if (first.Length > 0 && first.Length <= MaxLineLength &&
                        first.Length < line.Length)
                    {
                        string rest = contIndent +
                            line.Substring(breakAt).TrimStart();
                        var res = new List<string> { first };
                        res.AddRange(SplitLongLine(rest, contIndent));
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
                    // The close paren must sit at contIndent (parent+1) so
                    // that Reindent — which treats any line inside an open
                    // paren as a continuation and indents it parent+1 —
                    // produces the same indent on a second pass, keeping the
                    // split idempotent.
                    string closeLine = contIndent + ")";

                    var rhsSplit = SplitLongLine(rhsCont, contIndent);
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
                            res3.AddRange(SplitLongLine(rest2, contIndent));
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
        /// Determines whether the line occupying [lineStart, lineStart+lineLength) in text ends with
        /// a continuation backslash that is located in a Code region. Backslashes inside comments or
        /// string literals do not trigger continuation. A doubled backslash (\\) in Code is treated
        /// as a non-continuation to preserve prior behavior.
        /// </summary>
        /// <param name="text">The full text.</param>
        /// <param name="isCode">The code mask of text.</param>
        /// <param name="lineStart">The starting offset of the line in text.</param>
        /// <param name="lineLength">The length of the line (excluding the line terminator).</param>
        /// <returns>True if the line ends with a Code-region continuation backslash.</returns>
        private static bool EndsWithBackslash(string text, bool[] isCode,
            int lineStart, int lineLength)
        {
            int lastIdx = -1;

            for (int i = lineStart + lineLength - 1; i >= lineStart; i--)
            {
                if (i >= text.Length)
                {
                    continue;
                }

                char c = text[i];

                if (c != ' ' && c != '\t')
                {
                    lastIdx = i;
                    break;
                }
            }

            if (lastIdx < 0)
            {
                return false;
            }

            if (lastIdx >= isCode.Length || !isCode[lastIdx])
            {
                return false;
            }

            if (text[lastIdx] != '\\')
            {
                return false;
            }

            if (lastIdx > lineStart && text[lastIdx - 1] == '\\' &&
                lastIdx - 1 < isCode.Length && isCode[lastIdx - 1])
            {
                return false;
            }

            return true;
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
