using System.Collections.Generic;
using System.Text;

namespace CppFormatter
{
    /// <summary>
    /// Core implementation that applies all C++ formatting rules.
    /// </summary>
    internal static class Formatter
    {
        /// <summary>Indentation uses 4 spaces per level.</summary>
        private const int IndentSize = 4;
        /// <summary>Maximum length of a single line.</summary>
        private const int MaxLineLength = 80;
        /// <summary>Two-char operators whose break point sits right after
        /// the operator. Excludes &lt;&lt;/&gt;&gt; (stream ops need
        /// IsStreamOpContext) and single-char ops.</summary>
        private static readonly string[] TwoCharBreakOps =
            { "==", "!=", "<=", ">=", "=>", "+=", "-=", "&&", "||" };
        /// <summary>Keywords that introduce a brace-delimited block.</summary>
        private static readonly string[] BlockStartKeywords =
            { "namespace", "struct", "switch", "catch", "class", "while",
              "union", "enum", "else", "for", "try", "do", "if" };

        /// <summary>
        /// Applies all formatting rules to the source string and returns the result.
        /// </summary>
        /// <param name="source">The original source string.</param>
        /// <returns>The formatted source string.</returns>
        public static string Format(string source)
        {
            var tokens = Tokenizer.Tokenize(source);
            tokens = ApplyMandatoryBraces(tokens);
            string text = Tokenizer.Reconstruct(tokens);
            text = FormatEnums(text);
            text = IncludeSorter.Sort(text);
            text = text.Replace("\t", "    ");
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = MoveOpenBraceToPreviousLine(text);
            text = MergeDoWhileCloseBrace(text);
            tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            var lines = SplitLines(text);
            lines = Reindent(lines, text, tokens, isCode);
            lines = TrimNamespaceBodyBlankLines(lines, text, tokens, isCode);
            // Compute continuation flags from the post-Reindent (pre-split)
            // line structure so that LineLengthProcessor can detect
            // continuation lines and avoid cascading indents when splitting
            // them (a continuation line split at parent+4 must keep its
            // segments at parent+4, not parent+8).
            string textForLimit = string.Join("\n", lines);
            var tokensForLimit = Tokenizer.Tokenize(textForLimit);
            bool[] isCodeForLimit = Tokenizer.BuildCodeMask(textForLimit,
                tokensForLimit);
            int[] lineStartsForLimit = Tokenizer.ComputeLineStarts(lines);
            var preSplitContinues = new bool[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                preSplitContinues[i] = IsContinuationIndicator(lines[i],
                    lineStartsForLimit[i], textForLimit, isCodeForLimit);
            }
            // Split long lines BEFORE applying blank-line rules so that the
            // preSplitContinues flags (computed above) stay aligned with the
            // line list. Running BlankLineProcessor first would insert blank
            // lines and shift indices, causing LineLengthProcessor to read
            // the wrong continuation flag for each line.
            lines = ApplyLineLengthLimit(lines, textForLimit,
                preSplitContinues);
            string textForBlank = string.Join("\n", lines);
            lines = ApplyBlankLineRules(lines, textForBlank);
            string textForCollapse = string.Join("\n", lines);
            lines = CollapseBlankLines(lines, textForCollapse);
            string textForTrim = string.Join("\n", lines);
            lines = TrimTrailingWhitespace(lines, textForTrim);
            string result = string.Join("\n", lines);
            result = EnsureSingleTrailingNewline(result);
            return result;
        }

        /// <summary>
        /// Wraps single-statement bodies of if/else/for/while/do-while/switch with
        /// mandatory braces on the token stream.
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
                    CollectDoWhileBodyInsertions(text, isCode, i,
                        insertions);
                }

                else if (MatchesWord(text, i, "switch"))
                {
                    CollectSwitchBodyInsertions(text, isCode, i,
                        insertions);
                }

                else if (MatchesWord(text, i, "else"))
                {
                    int afterElse = i + 4;
                    int nextNonWs = SkipWhitespace(text, afterElse);

                    if (MatchesWord(text, nextNonWs, "if"))
                    {
                        continue;
                    }

                    CollectBodyInsertions(text, isCode, afterElse,
                        insertions);
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
            int stmtEnd = ScanStatementEnd(text, isCode, i);

            if (stmtEnd < 0)
            {
                return;
            }

            insertions.Add(new Insertion(stmtStart, "{\n"));
            insertions.Add(new Insertion(stmtEnd, "\n}"));
        }

        /// <summary>
        /// Wraps a do-while single-statement body with braces; the closing } is placed on the same line as while.
        /// </summary>
        private static void CollectDoWhileBodyInsertions(string text,
            bool[] isCode, int doPos, List<Insertion> insertions)
        {
            int i = SkipWhitespace(text, doPos + 2);

            if (i >= text.Length)
            {
                return;
            }

            if (isCode[i] && text[i] == '{')
            {
                return;
            }

            int stmtStart = i;
            int stmtEnd = ScanStatementEnd(text, isCode, i);

            if (stmtEnd < 0)
            {
                return;
            }

            int w = SkipWhitespace(text, stmtEnd);

            if (w >= text.Length || !MatchesWord(text, w, "while"))
            {
                return;
            }

            insertions.Add(new Insertion(stmtStart, "{\n"));
            insertions.Add(new Insertion(w, "\n} "));
        }

        /// <summary>
        /// Wraps a switch single-statement body with braces.
        /// </summary>
        private static void CollectSwitchBodyInsertions(string text,
            bool[] isCode, int switchPos, List<Insertion> insertions)
        {
            int afterParen = SkipParen(text, isCode, switchPos + 6);

            if (afterParen < 0)
            {
                return;
            }

            int i = SkipWhitespace(text, afterParen);

            if (i >= text.Length)
            {
                return;
            }

            if (isCode[i] && text[i] == '{')
            {
                return;
            }

            CollectBodyInsertions(text, isCode, afterParen, insertions);
        }

        /// <summary>
        /// Scans a statement starting from startPos, tracking bracket depth,
        /// and stops at the first semicolon encountered at depth 0. Returns
        /// the position immediately after that semicolon, or -1 if no such
        /// semicolon is found.
        /// </summary>
        private static int ScanStatementEnd(string text, bool[] isCode,
            int startPos)
        {
            int j = startPos;
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
                        return j + 1;
                    }
                }

                j++;
            }

            return -1;
        }

        /// <summary>
        /// Skips a balanced pair of parentheses from the given position; returns the position after the closing ) or -1 if not well-formed.
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

        /// <summary>
        /// Finds all enum declarations and expands their members into multiple lines. Supports enum class/enum struct.
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

                int afterEnum = i + 4;
                int nextNonWs = SkipWhitespace(text, afterEnum);

                if (MatchesWord(text, nextNonWs, "class"))
                {
                    afterEnum = nextNonWs + 5;
                }

                else if (MatchesWord(text, nextNonWs, "struct"))
                {
                    afterEnum = nextNonWs + 6;
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
        /// Splits text by lines.
        /// </summary>
        private static List<string> SplitLines(string text)
        {
            return new List<string>(text.Split('\n'));
        }

        /// <summary>
        /// Merges a { that sits on its own line back onto the previous line
        /// (K&amp;R style). Only merges when { is alone on its line and lies in a
        /// code region; braces inside string literals or comments are left
        /// untouched.
        /// </summary>
        private static string MoveOpenBraceToPreviousLine(string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            string[] lines = text.Split('\n');
            var result = new List<string>(lines.Length);
            int pos = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                bool merged = false;

                if (trimmed == "{" && i > 0 && result.Count > 0)
                {
                    int bracePos = pos + lines[i].IndexOf('{');
                    bool isCodeBrace = bracePos < isCode.Length &&
                        isCode[bracePos];

                    if (isCodeBrace)
                    {
                        string prev = result[result.Count - 1].TrimEnd();

                        if (prev.Length > 0)
                        {
                            result[result.Count - 1] = prev + " {";
                            merged = true;
                        }
                    }
                }

                if (!merged)
                {
                    result.Add(lines[i]);
                }

                if (i < lines.Length - 1)
                {
                    pos += lines[i].Length + 1;
                }
            }

            return string.Join("\n", result);
        }

        /// <summary>
        /// Merges a lone closing brace that terminates a do-while body with the
        /// following while line, producing K&amp;R style "} while (cond);". Only
        /// braces in code regions are considered; braces inside strings or
        /// comments are left untouched.
        /// </summary>
        private static string MergeDoWhileCloseBrace(string text)
        {
            string[] lines = text.Split('\n');
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            var result = new List<string>(lines.Length);
            var merged = new bool[lines.Length];
            int pos = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                int lineStart = pos;

                if (i < lines.Length - 1)
                {
                    pos += lines[i].Length + 1;
                }

                if (merged[i])
                {
                    continue;
                }

                string trimmed = lines[i].Trim();

                if ((trimmed == "}" || trimmed == "};") &&
                    i + 1 < lines.Length)
                {
                    int braceOffset = lines[i].IndexOf('}');
                    int bracePos = lineStart + braceOffset;

                    if (bracePos < isCode.Length && isCode[bracePos])
                    {
                        int openBracePos = FindMatchingOpenBrace(text, isCode,
                            bracePos);

                        if (openBracePos >= 0 &&
                            IsDoKeywordBefore(text, isCode, openBracePos))
                        {
                            int j = i + 1;

                            while (j < lines.Length &&
                                lines[j].Trim().Length == 0)
                            {
                                j++;
                            }

                            if (j < lines.Length &&
                                StartsWithKeyword(lines[j].Trim(), "while"))
                            {
                                result.Add(lines[i].TrimEnd() + " " +
                                    lines[j].Trim());
                                merged[j] = true;
                                continue;
                            }
                        }
                    }
                }

                result.Add(lines[i]);
            }

            return string.Join("\n", result);
        }

        /// <summary>
        /// Finds the matching open brace for a close brace at closePos by
        /// scanning backward through code regions only. Returns -1 if no
        /// match is found.
        /// </summary>
        private static int FindMatchingOpenBrace(string text, bool[] isCode,
            int closePos)
        {
            int depth = 1;
            int i = closePos - 1;

            while (i >= 0)
            {
                if (isCode[i])
                {
                    if (text[i] == '}')
                    {
                        depth++;
                    }
                    else if (text[i] == '{')
                    {
                        depth--;

                        if (depth == 0)
                        {
                            return i;
                        }
                    }
                }

                i--;
            }

            return -1;
        }

        /// <summary>
        /// Determines whether the keyword "do" immediately precedes the open
        /// brace at openBracePos, ignoring any whitespace between them.
        /// </summary>
        private static bool IsDoKeywordBefore(string text, bool[] isCode,
            int openBracePos)
        {
            int i = openBracePos - 1;

            while (i >= 0 && (text[i] == ' ' || text[i] == '\t' ||
                text[i] == '\n' || text[i] == '\r'))
            {
                i--;
            }

            if (i < 1)
            {
                return false;
            }

            int doStart = i - 1;

            if (doStart >= isCode.Length || !isCode[doStart])
            {
                return false;
            }

            return MatchesWord(text, doStart, "do");
        }

        /// <summary>
        /// Recomputes leading whitespace for each line based on nesting depth.
        /// Lines fully inside a VerbatimString or MultiLineComment token (but not the first line
        /// of such a token) preserve their original leading whitespace to avoid damaging
        /// string/comment content.
        /// </summary>
        private static List<string> Reindent(List<string> lines, string text,
            List<Token> tokens, bool[] isCode)
        {
            int[] depths = new int[lines.Count];
            bool[] preserveIndent = ComputePreserveIndent(lines, tokens);
            bool[] inEnumBlock = ComputeInEnumBlock(lines, text, isCode);
            bool[] caseBody = ComputeCaseScope(lines, text, isCode);
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
            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

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

                if (i > 0 && !inEnumBlock[i] && !caseBody[i] &&
                    IsContinuationIndicator(lines[i - 1], lineStarts[i - 1],
                    text, isCode))
                {
                    baseDepth++;
                }

                if (caseBody[i])
                {
                    baseDepth++;
                }

                result.Add(new string(' ', baseDepth * IndentSize) + content);
            }

            return result;
        }

        /// <summary>
        /// Determines whether the given line ends with a continuation indicator.
        /// Scans backward for the last code-region non-whitespace character so
        /// that trailing comments do not mask the real indicator. Recognized
        /// operators: <c>,</c>, <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>,
        /// <c>%</c>, <c>(</c>, <c>=</c>, <c>?</c>, <c>&lt;</c>, <c>&gt;</c>,
        /// <c>:</c> (unless a label), <c>&amp;&amp;</c>, <c>||</c>.
        /// </summary>
        private static bool IsContinuationIndicator(string line, int lineStart,
            string text, bool[] isCode)
        {
            int lastCodeIdx = LastCodeCharIndex(line, lineStart, text,
                isCode);

            if (lastCodeIdx < 0)
            {
                return false;
            }

            char last = line[lastCodeIdx];

            if (last == ',' || last == '+' || last == '-' || last == '*' ||
                last == '/' || last == '%' || last == '(' || last == '=' ||
                last == '?' || last == '<' || last == '>')
            {
                return true;
            }

            if (last == ':')
            {
                return !IsLabelLine(line.Substring(0, lastCodeIdx + 1));
            }

            if (lastCodeIdx < 1)
            {
                return false;
            }

            int prevTextPos = lineStart + lastCodeIdx - 1;

            if (prevTextPos < 0 || prevTextPos >= isCode.Length ||
                !isCode[prevTextPos])
            {
                return false;
            }

            string last2 = line.Substring(lastCodeIdx - 1, 2);
            return last2 == "&&" || last2 == "||";
        }

        /// <summary>
        /// Finds the index of the last non-whitespace code-region character in
        /// the line. Scans backward from the end of <paramref name="line"/>,
        /// skipping positions whose corresponding <paramref name="isCode"/>
        /// entry is false and skipping space/tab characters. Correctly handles
        /// trailing comments (e.g., <c>code, // comment</c>).
        /// </summary>
        private static int LastCodeCharIndex(string line, int lineStart,
            string text, bool[] isCode)
        {
            for (int i = line.Length - 1; i >= 0; i--)
            {
                int textPos = lineStart + i;

                if (textPos < 0 || textPos >= isCode.Length ||
                    !isCode[textPos])
                {
                    continue;
                }

                char c = line[i];

                if (c == ' ' || c == '\t')
                {
                    continue;
                }

                return i;
            }

            return -1;
        }

        /// <summary>
        /// Determines whether a line that ends with ':' is a label line
        /// (access specifier, default label, case label, or plain identifier
        /// label) rather than a ternary-operator continuation.
        /// The input is fully trimmed (both leading and trailing) to handle
        /// re-indented lines that carry leading whitespace.
        /// </summary>
        private static bool IsLabelLine(string line)
        {
            string trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed == "public:" || trimmed == "private:" ||
                trimmed == "protected:")
            {
                return true;
            }

            if (trimmed == "default:")
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "case"))
            {
                return true;
            }

            if (trimmed.EndsWith(":") && trimmed.Length > 1)
            {
                string label = trimmed.Substring(0, trimmed.Length - 1);

                if (IsPureIdentifier(label))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a string is a pure C++ identifier: starting with
        /// a letter or underscore and containing only letters, digits, or
        /// underscores.
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
        /// Computes whether each line lies inside an enum block.
        /// </summary>
        private static bool[] ComputeInEnumBlock(List<string> lines,
            string text, bool[] isCode)
        {
            var inEnumBlock = new bool[lines.Count];
            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

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
        /// Computes which lines within a switch block belong to a case body (indented one extra level beyond the case label).
        /// </summary>
        private static bool[] ComputeCaseScope(List<string> lines, string text,
            bool[] isCode)
        {
            var caseBody = new bool[lines.Count];
            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

            var switchRanges = new List<KeyValuePair<int, int>>();
            var braceStack = new Stack<KeyValuePair<bool, int>>();
            bool pendingSwitch = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = text[i];

                if (c == 's' && (i == 0 || !IsWordChar(text[i - 1])) &&
                    MatchesWord(text, i, "switch"))
                {
                    pendingSwitch = true;
                }

                if (c == '{')
                {
                    braceStack.Push(new KeyValuePair<bool, int>(pendingSwitch,
                        i));
                    pendingSwitch = false;
                }

                else if (c == '}')
                {
                    if (braceStack.Count > 0)
                    {
                        var top = braceStack.Pop();

                        if (top.Key)
                        {
                            switchRanges.Add(new KeyValuePair<int, int>(
                                top.Value, i));
                        }
                    }
                }

                else if (c == ';')
                {
                    pendingSwitch = false;
                }
            }

            switchRanges.Sort((a, b) => a.Key.CompareTo(b.Key));

            foreach (var range in switchRanges)
            {
                int braceStart = range.Key;
                int braceEnd = range.Value;
                var innerRanges = new List<KeyValuePair<int, int>>();

                foreach (var r in switchRanges)
                {
                    if (r.Key > braceStart && r.Value < braceEnd)
                    {
                        innerRanges.Add(r);
                    }
                }

                bool inCaseBody = false;

                for (int li = 0; li < lines.Count; li++)
                {
                    int ls = lineStarts[li];

                    if (ls <= braceStart || ls >= braceEnd)
                    {
                        continue;
                    }

                    int lineEndPos = ls + lines[li].Length;

                    if (braceEnd >= ls && braceEnd < lineEndPos)
                    {
                        inCaseBody = false;
                        continue;
                    }

                    bool inInner = false;

                    foreach (var ir in innerRanges)
                    {
                        if (ls > ir.Key && ls < ir.Value)
                        {
                            inInner = true;
                            break;
                        }
                    }

                    if (inInner)
                    {
                        continue;
                    }

                    string trimmed = lines[li].Trim();

                    if (IsCaseLabelLine(trimmed))
                    {
                        inCaseBody = true;
                    }

                    else if (inCaseBody)
                    {
                        caseBody[li] = true;
                    }
                }
            }

            return caseBody;
        }

        /// <summary>
        /// Determines whether a line is a case/default label line for a switch.
        /// </summary>
        private static bool IsCaseLabelLine(string trimmed)
        {
            if (trimmed.Length == 0 || !trimmed.EndsWith(":"))
            {
                return false;
            }

            return StartsWithKeyword(trimmed, "case") ||
                StartsWithKeyword(trimmed, "default");
        }

        /// <summary>
        /// Computes whether each line should preserve its original leading whitespace.
        /// </summary>
        private static bool[] ComputePreserveIndent(List<string> lines,
            List<Token> tokens)
        {
            var preserveIndent = new bool[lines.Count];
            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

            int tokenPos = 0;

            foreach (var token in tokens)
            {
                int tokenStart = tokenPos;
                int tokenEnd = tokenPos + token.Text.Length;

                if (token.Kind == TokenKind.VerbatimString ||
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
        /// Removes blank lines immediately after the opening { and immediately before the closing } of a namespace body.
        /// </summary>
        private static List<string> TrimNamespaceBodyBlankLines(
            List<string> lines, string text, List<Token> tokens,
            bool[] isCode)
        {
            var result = new List<string>(lines.Count);

            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

            var nsBlocks = new List<KeyValuePair<int, int>>();
            int braceDepth = 0;
            var braceStack = new Stack<int>();
            bool pendingNamespace = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = text[i];

                if (c == 'n' && (i == 0 || !IsWordChar(text[i - 1])) &&
                    MatchesWord(text, i, "namespace"))
                {
                    pendingNamespace = true;
                }

                if (c == '{')
                {
                    if (pendingNamespace)
                    {
                        nsBlocks.Add(new KeyValuePair<int, int>(i, -1));
                        braceStack.Push(nsBlocks.Count - 1);
                    }
                    else
                    {
                        braceStack.Push(-1);
                    }

                    pendingNamespace = false;
                    braceDepth++;
                }

                else if (c == '}')
                {
                    braceDepth--;

                    if (braceStack.Count > 0)
                    {
                        int idx = braceStack.Pop();

                        if (idx >= 0 && idx < nsBlocks.Count)
                        {
                            nsBlocks[idx] = new KeyValuePair<int, int>(
                                nsBlocks[idx].Key, i);
                        }
                    }

                    pendingNamespace = false;
                }

                else if (c == ';')
                {
                    pendingNamespace = false;
                }
            }

            var removeSet = new HashSet<int>();

            foreach (var block in nsBlocks)
            {
                if (block.Value < 0)
                {
                    continue;
                }

                int openBracePos = block.Key;
                int closeBracePos = block.Value;

                for (int li = 0; li < lines.Count; li++)
                {
                    int ls = lineStarts[li];

                    if (ls <= openBracePos || ls >= closeBracePos)
                    {
                        continue;
                    }

                    if (lines[li].Trim().Length != 0)
                    {
                        continue;
                    }

                    int nextNonBlank = li + 1;

                    while (nextNonBlank < lines.Count &&
                        lines[nextNonBlank].Trim().Length == 0)
                    {
                        nextNonBlank++;
                    }

                    bool isAfterOpenBrace = li > 0 &&
                        lineStarts[li - 1] + lines[li - 1].Length <=
                        openBracePos + 1;

                    if (isAfterOpenBrace)
                    {
                        removeSet.Add(li);
                    }

                    bool isBeforeCloseBrace = nextNonBlank <
                        lines.Count &&
                        lineStarts[nextNonBlank] >= closeBracePos;

                    if (isBeforeCloseBrace)
                    {
                        removeSet.Add(li);
                    }
                }
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (!removeSet.Contains(i))
                {
                    result.Add(lines[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Ensures exactly one blank line above and below blocks/declarations
        /// (with start/end-of-parent-block exceptions). Lines entirely inside a
        /// multi-line string or comment token are preserved verbatim and never
        /// stripped or regenerated by the blank-line rules.
        /// </summary>
        private static List<string> ApplyBlankLineRules(List<string> lines,
            string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] protectedLines = Tokenizer.ComputeProtectedLines(text,
                tokens, lines.Count);
            var nonBlank = new List<KeyValuePair<bool, string>>(lines.Count);
            bool prevWasBlank = false;
            bool isFirst = true;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                bool isProtected = i < protectedLines.Length &&
                    protectedLines[i];

                if (line.Trim().Length == 0 && !isProtected)
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

                    if (!wantBlankAbove && IsIncludeDirective(trimmed) &&
                        IsIncludeDirective(prevTrimmed) && nonBlank[i].Key)
                    {
                        wantBlankAbove = true;
                    }
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
        /// Collapses 3 or more consecutive blank lines into 1. Lines entirely
        /// inside a multi-line string or comment token are preserved verbatim
        /// and never participate in blank-line collapsing.
        /// </summary>
        private static List<string> CollapseBlankLines(List<string> lines,
            string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] protectedLines = Tokenizer.ComputeProtectedLines(text,
                tokens, lines.Count);
            var result = new List<string>(lines.Count);
            int blankRun = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                if (i < protectedLines.Length && protectedLines[i])
                {
                    result.Add(lines[i]);
                    blankRun = 0;
                    continue;
                }

                if (lines[i].Trim().Length == 0)
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
                    result.Add(lines[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Trims trailing whitespace from each line. Lines whose last character
        /// lies inside a multi-line string or comment token are preserved
        /// verbatim to avoid damaging raw string contents.
        /// </summary>
        private static List<string> TrimTrailingWhitespace(List<string> lines,
            string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);
            bool[] endsInside = Tokenizer.ComputeLineEndsInsideToken(text,
                tokens, lineStarts, lines);
            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                if (i < endsInside.Length && endsInside[i])
                {
                    result.Add(lines[i]);
                }
                else
                {
                    result.Add(lines[i].TrimEnd());
                }
            }

            return result;
        }

        /// <summary>
        /// Splits lines exceeding 80 characters at safe token boundaries;
        /// continuation lines are indented one extra level. Lines entirely
        /// inside a multi-line string or comment token are preserved verbatim
        /// and never split.
        /// <paramref name="lineContinuesNext"/> flags whether each line ends
        /// with a continuation indicator; when a line is itself a continuation
        /// of the previous line, its split segments reuse the line's current
        /// indent (no extra level) so that splitting a continuation line does
        /// not cascade into deeper indents on a second pass.
        /// </summary>
        private static List<string> ApplyLineLengthLimit(List<string> lines,
            string text, bool[] lineContinuesNext)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] protectedLines = Tokenizer.ComputeProtectedLines(text,
                tokens, lines.Count);
            var result = new List<string>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                if (i < protectedLines.Length && protectedLines[i])
                {
                    result.Add(lines[i]);
                    continue;
                }

                string line = lines[i];

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
        /// Recursively splits a single line so that each segment does not
        /// exceed 80 characters. <paramref name="fixedContIndent"/> is the
        /// fixed continuation indent reused across all continuation segments
        /// so that 3+ segment splits do not cascade; pass null on the first
        /// call to trigger computation from the original line's indent.
        /// </summary>
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
            // matching Reindent's behaviour).
            if (fixedContIndent == null)
            {
                fixedContIndent = indent + new string(' ', IndentSize);
            }

            var tokens = Tokenizer.Tokenize(line);
            bool[] isCode = Tokenizer.BuildCodeMask(line, tokens);
            int breakAt = FindSafeBreakPoint(line, isCode, indentLen);

            if (breakAt < 0 || breakAt >= line.Length)
            {
                return new List<string> { line };
            }

            string first = line.Substring(0, breakAt).TrimEnd();
            string rest = fixedContIndent + line.Substring(breakAt).TrimStart();

            if (first.Length == 0 || first.Length >= line.Length)
            {
                return new List<string> { line };
            }

            var result = new List<string> { first };
            result.AddRange(SplitLongLine(rest, fixedContIndent));
            return result;
        }

        /// <summary>
        /// Finds a safe break point within Code tokens. Additionally supports &lt;&lt; and &gt;&gt; beyond the C# break point set.
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

                if (i + 1 < line.Length)
                {
                    string pair = line.Substring(i, 2);
                    foreach (var op in TwoCharBreakOps)
                    {
                        if (pair == op)
                        {
                            bp = i + 2;
                            i++;
                            break;
                        }
                    }
                }

                if (bp < 0 && c == '<' && i + 1 < line.Length &&
                    line[i + 1] == '<' &&
                    IsStreamOpContext(line, i, startIdx))
                {
                    bp = i + 2;
                    i++;
                }

                else if (bp < 0 && c == '>' && i + 1 < line.Length &&
                    line[i + 1] == '>' &&
                    IsStreamOpContext(line, i, startIdx))
                {
                    bp = i + 2;
                    i++;
                }

                else if (bp < 0 && c == ',')
                {
                    bp = i + 1;
                }

                else if (bp < 0 && c == ';')
                {
                    if (i + 1 < line.Length)
                    {
                        bp = i + 1;
                    }
                }

                else if (bp < 0 && i > startIdx &&
                    IsBinaryOpContext(line, i, startIdx) &&
                    (c == '+' || c == '-' || c == '*' || c == '/' ||
                    c == '%' || c == '<' || c == '>'))
                {
                    bp = i + 1;
                }

                else if (bp < 0 && c == '=' && i > startIdx &&
                    IsBinaryOpContext(line, i, startIdx) &&
                    (i + 1 >= line.Length || (line[i + 1] != '=' &&
                    line[i + 1] != '>')))
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
        /// Determines whether a position with &lt;&lt; or &gt;&gt; is in a stream operator context:
        /// i.e., the preceding non-whitespace character is ), ], an identifier character, _, " or '.
        /// This avoids breaking inside template parameter lists (e.g., vector&lt;vector&lt;int&gt;&gt;).
        /// </summary>
        private static bool IsStreamOpContext(string line, int i,
            int startIdx)
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
                pc == '_' || pc == '"' || pc == '\'';
        }

        /// <summary>
        /// Determines whether the position at line[i] is in a binary operator context.
        /// </summary>
        private static bool IsBinaryOpContext(string line, int i,
            int startIdx)
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
        /// Ensures the file ends with exactly one newline character.
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

        /// <summary>
        /// Determines whether a line starts with a block-start keyword.
        /// C++ keyword set: catch/class/do/else/enum/for/if/namespace/struct/
        /// switch/try/union/while.
        /// </summary>
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

            foreach (var kw in BlockStartKeywords)
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
        /// Determines whether a line is a block end line: exactly } or };.
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
        /// Determines whether a line is an #include directive.
        /// </summary>
        private static bool IsIncludeDirective(string trimmed)
        {
            return trimmed.StartsWith("#include");
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
