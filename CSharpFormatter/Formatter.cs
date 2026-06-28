using System.Collections.Generic;
using System.Text;

namespace CSharpFormatter
{
    /// <summary>
    /// Core implementation that applies all C# formatting rules.
    /// </summary>
    internal static class Formatter
    {
        /// <summary>Number of spaces per indentation level.</summary>
        private const int IndentSize = 4;
        /// <summary>Maximum allowed line length.</summary>
        private const int MaxLineLength = 80;
        /// <summary>
        /// Applies all formatting rules to the source string and returns
        /// the result.
        /// </summary>
        /// <param name="source">The original source code string.</param>
        /// <param name="rootNamespace">The root namespace of the current
        /// module.</param>
        /// <returns>The formatted source code string.</returns>
        public static string Format(string source, string rootNamespace)
        {
            var tokens = Tokenizer.Tokenize(source);
            tokens = ApplyMandatoryBraces(tokens);
            string text = Tokenizer.Reconstruct(tokens);
            text = FormatEnums(text);
            text = FormatPropertyAccessors(text);
            text = UsingSorter.Sort(text, rootNamespace);
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
        /// Wraps single-statement bodies of if/else/for/while etc. with
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

                else if (MatchesWord(text, i, "foreach"))
                {
                    int afterParen = SkipParen(text, isCode, i + 7);

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

                else if (MatchesWord(text, i, "lock"))
                {
                    int afterParen = SkipParen(text, isCode, i + 4);

                    if (afterParen >= 0)
                    {
                        CollectBodyInsertions(text, isCode, afterParen,
                            insertions);
                    }
                }

                else if (MatchesWord(text, i, "using"))
                {
                    int afterParen = SkipParen(text, isCode, i + 5);

                    if (afterParen >= 0)
                    {
                        CollectBodyInsertions(text, isCode, afterParen,
                            insertions);
                    }
                }

                else if (MatchesWord(text, i, "fixed"))
                {
                    int afterParen = SkipParen(text, isCode, i + 5);

                    if (afterParen >= 0)
                    {
                        CollectBodyInsertions(text, isCode, afterParen,
                            insertions);
                    }
                }

                else if (MatchesWord(text, i, "checked"))
                {
                    CollectOptionalParenBody(text, isCode, i + 7, insertions);
                }

                else if (MatchesWord(text, i, "unchecked"))
                {
                    CollectOptionalParenBody(text, isCode, i + 9, insertions);
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
        /// Replaces a single-statement body with a brace-wrapped block by
        /// appending insertion points to <paramref name="insertions"/>.
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
        /// Skips an optional (expr) and then calls CollectBodyInsertions.
        /// Used for keywords like checked/unchecked that may be followed by
        /// either (expr) or directly by a block/statement.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="start">The position after the keyword to start
        /// scanning from.</param>
        /// <param name="insertions">The insertion point list.</param>
        private static void CollectOptionalParenBody(string text, bool[] isCode,
            int start, List<Insertion> insertions)
        {
            int next = SkipWhitespace(text, start);

            if (next < text.Length && isCode[next] && text[next] == '(')
            {
                int afterParen = SkipParen(text, isCode, next);

                if (afterParen >= 0)
                {
                    CollectBodyInsertions(text, isCode, afterParen,
                        insertions);
                }
            }

            else
            {
                CollectBodyInsertions(text, isCode, start, insertions);
            }
        }

        /// <summary>
        /// Skips a balanced pair of parentheses from the given position,
        /// returning the position after the closing paren; returns -1 if
        /// not found.
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
        /// Finds all enum declarations and expands their members
        /// onto separate lines.
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
        /// Expands single-line property accessor blocks into multi-line form.
        /// Supports recursive expansion of nested accessor blocks
        /// (e.g., get { return x; } set { y = value; }).
        /// </summary>
        private static string FormatPropertyAccessors(string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            var replacements = new List<Replacement>();

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i] || text[i] != '{')
                {
                    continue;
                }

                int depth = 1;
                int j = i + 1;
                bool hasNewline = false;

                while (j < text.Length)
                {
                    if (text[j] == '\n')
                    {
                        hasNewline = true;
                        break;
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
                                break;
                            }
                        }
                    }

                    j++;
                }

                if (depth != 0 || hasNewline)
                {
                    continue;
                }

                int braceEnd = j;
                string content = text.Substring(i + 1, braceEnd - i - 1);

                if (!IsAccessorContent(content))
                {
                    continue;
                }

                string replacement = ExpandAccessors(content, 1);
                replacements.Add(new Replacement(i + 1, braceEnd,
                    replacement));
            }

            return ApplyReplacements(text, replacements);
        }

        /// <summary>
        /// Recursively expands accessor content into multi-line form. Each
        /// accessor of the form keyword { block } is expanded into keyword,
        /// {, the block content (recursively), }; accessors of the form
        /// keyword; or keyword => expr; remain single-line. Non-accessor
        /// statements also remain single-line.
        /// </summary>
        /// <param name="content">The accessor block content (excluding the
        /// outer { }).</param>
        /// <param name="indentLevel">The current indentation level (1 means
        /// the first level inside the property body).</param>
        /// <returns>The expanded multi-line text (starting with \n).</returns>
        private static string ExpandAccessors(string content, int indentLevel)
        {
            var sb = new StringBuilder();
            sb.Append('\n');
            string indent = new string(' ', indentLevel * IndentSize);

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
                if (StartsWithKeyword(s, kw))
                {
                    return kw;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the position of the } that balances the first { in the
        /// string (does not handle parentheses inside string literals).
        /// </summary>
        /// <param name="s">The string to search, whose first character
        /// should be {.</param>
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
        /// Splits text into lines.
        /// </summary>
        private static List<string> SplitLines(string text)
        {
            return new List<string>(text.Split('\n'));
        }

        /// <summary>
        /// Moves a trailing { from the end of a line to its own line
        /// (Allman style). Splits only when there is non-whitespace content
        /// before the {, to avoid incorrectly splitting an already
        /// standalone indented { into an empty line.
        /// </summary>
        private static string MoveOpenBraceToOwnLine(string text)
        {
            string[] lines = text.Split('\n');
            var result = new List<string>(lines.Length + 16);

            foreach (var line in lines)
            {
                string trimmedEnd = line.TrimEnd();

                if (trimmedEnd.Length > 1 && trimmedEnd[trimmedEnd.Length - 1]
                == '{')
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
        /// Recomputes leading whitespace for each line according to nesting
        /// depth. Lines that fall entirely inside a VerbatimString or
        /// MultiLineComment token (i.e., not the first line of the token)
        /// retain their original leading whitespace to avoid corrupting
        /// string/comment content.
        /// </summary>
        private static List<string> Reindent(List<string> lines, string text)
        {
            var tokens = Tokenizer.Tokenize(text);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
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

                if (caseBody[i])
                {
                    baseDepth++;
                }

                result.Add(new string(' ', baseDepth * IndentSize) + content);
            }

            return result;
        }

        /// <summary>
        /// Determines whether the specified line ends with a continuation
        /// indicator (i.e., the statement is not yet complete, so the next
        /// line should be treated as a continuation). Continuation lines
        /// receive one extra indentation level during re-indentation to
        /// preserve the continuation indentation already set by
        /// ApplyLineLengthLimit, ensuring idempotence: continuation
        /// indentation produced by the first split is not erased by Reindent
        /// on a second run. A line is only treated as a continuation when
        /// the trailing indicator character lies within a code region; if the
        /// last character falls inside a string/comment token (e.g., `:` at
        /// the end of a comment line), returns false to avoid incorrectly
        /// indenting the next line.
        /// </summary>
        /// <param name="line">The line text to inspect.</param>
        /// <param name="lineStart">The starting offset of this line within
        /// <paramref name="text"/>.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">A code mask of the same length as
        /// <paramref name="text"/>.</param>
        /// <returns>true if the line ends with a continuation indicator
        /// within a code region; otherwise false.</returns>
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
        /// Computes whether each line is inside an enum block (i.e., between
        /// the { and } belonging to the enum keyword, excluding the { line
        /// and } line). Enum member lines ending with , should not be
        /// recognized as continuation indicators, otherwise subsequent
        /// members would be incorrectly indented one extra level.
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
        /// Computes which lines inside a switch block belong to a case body
        /// (i.e., need one extra indentation level beyond the case label).
        /// Case label lines (e.g., case X: or default:) are not themselves
        /// marked; lines following them until the next case/default label
        /// or the closing } of the switch are case body. Supports nested
        /// switch: when processing the outer switch, inner switch block case
        /// label detection is skipped, but lines of the inner switch block
        /// are still marked as case body by the outer processing.
        /// </summary>
        /// <param name="lines">The split line list.</param>
        /// <param name="text">The full source text.</param>
        /// <param name="isCode">A code mask of the same length as text.</param>
        /// <returns>A boolean array; true means the line is a case body
        /// (requires +1 indentation).</returns>
        private static bool[] ComputeCaseScope(List<string> lines, string text,
            bool[] isCode)
        {
            var caseBody = new bool[lines.Count];
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

                    string trimmed = lines[li].Trim();

                    if (!inInner && IsCaseLabelLine(trimmed))
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
        /// Determines whether a (trimmed) line is a switch case/default label
        /// line: starts with the case or default keyword (word boundary)
        /// and ends with :.
        /// </summary>
        /// <param name="trimmed">The line text with leading and trailing
        /// whitespace removed.</param>
        /// <returns>true if the line is a case/default label line; otherwise
        /// false.</returns>
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
        /// Computes whether each line should preserve its original leading
        /// whitespace: returns true iff the line's starting position lies
        /// inside a VerbatimString or MultiLineComment token (i.e., not the
        /// first line of that token). Using lineStart for the check correctly
        /// handles token-ending lines (e.g., line3"; at the end of a verbatim
        /// string): these lines still start inside the token and need their
        /// original leading whitespace preserved to avoid corrupting string
        /// content.
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
        /// Ensures exactly one blank line above and below blocks/declarations
        /// (applying the start/end exceptions). First strips all blank lines
        /// while recording a "had blank line above in original input" flag,
        /// then re-adds blank lines where required by rules, thereby both
        /// adding missing blank lines and removing excess ones for idempotence.
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

                    if (!wantBlankAbove && IsUsingDirective(trimmed) &&
                        IsUsingDirective(prevTrimmed) && nonBlank[i].Key)
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
        /// Strips trailing whitespace from each line.
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
        /// Splits lines exceeding 80 characters at safe token boundaries;
        /// continuation lines are indented one extra level.
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
        /// Recursively splits a single line so that each segment does not
        /// exceed 80 characters; splits only at Code token boundaries,
        /// never breaking inside a String/VerbatimString/Char/Comment/
        /// Preprocessor token. If no safe break point is found (e.g., the
        /// entire line is an excessively long string literal), the line is
        /// returned unchanged.
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
        /// Finds a safe break point within a Code token: prefers the largest
        /// break point not exceeding 80 characters; if no such point exists,
        /// returns the first break point beyond 80 characters (which still
        /// avoids breaking inside strings/comments). Breaks after operators
        /// such as , ; + - * / % &lt; &gt; = == != &lt;= &gt;= =&gt; += -= &amp;&amp; ||,
        /// avoiding breaks inside string literals.
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

                else if (c == '=' && i + 1 < line.Length &&
                    line[i + 1] == '>')
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
        /// Determines whether position <paramref name="i"/> in the line is
        /// in a binary operator context: i.e., the preceding non-whitespace
        /// character is ), ], an identifier character, _, or ". Used to
        /// exclude unary operators (e.g., -5, *p) and generics (e.g.,
        /// List&lt;T&gt;) from being treated as break points.
        /// </summary>
        /// <param name="line">The line text.</param>
        /// <param name="i">The current operator position.</param>
        /// <param name="startIdx">The starting scan position.</param>
        /// <returns>true if in a binary operator context; otherwise
        /// false.</returns>
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

        /// <summary>
        /// Determines whether content is property accessor content: after
        /// splitting, each part starts with an accessor keyword
        /// (get/set/init/add/remove) at a word boundary. Nested { } blocks
        /// are allowed.
        /// </summary>
        /// <param name="content">The content to inspect.</param>
        /// <returns>true if the content is accessor content; otherwise
        /// false.</returns>
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
        /// Splits content at accessor/statement boundaries: splits at ; at
        /// depth 0 or at } close (when depth returns to 0). Tracks
        /// ( [ { ) ] } depth to correctly handle nested blocks.
        /// </summary>
        /// <param name="content">The content to split.</param>
        /// <returns>A list of split parts (trimmed).</returns>
        private static List<string> SplitAccessors(string content)
        {
            var parts = new List<string>();
            int i = 0;

            while (i < content.Length)
            {
                while (i < content.Length && char.IsWhiteSpace(content[i]))
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

            if (StartsWithKeyword(trimmed, "using") && !trimmed.Contains("("))
            {
                return false;
            }

            string[] keywords =
                {
                "namespace", "interface", "unchecked", "finally", "foreach",
                    "checked", "struct", "switch", "catch", "class", "while",
                    "unsafe", "using", "enum", "else", "for", "try", "do",
                    "if", "lock", "fixed"
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
        /// Determines whether a (trimmed) line is a block end line:
        /// exactly } or };.
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
        /// Determines whether a (trimmed) line is a using directive.
        /// </summary>
        private static bool IsUsingDirective(string trimmed)
        {
            if (trimmed.StartsWith("using "))
            {
                return true;
            }

            if (trimmed.StartsWith("using\t"))
            {
                return true;
            }

            return trimmed == "using";
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
