using System.Collections.Generic;
using System.Text;

namespace CppFormatter
{
    /// <summary>
    /// Enforces mandatory curly braces for all control-flow statement
    /// bodies by wrapping single-statement bodies in a brace block.
    /// </summary>
    internal static class BraceEnforcer
    {
        /// <summary>
        /// Wraps single-statement bodies of if/else/for/while/do-while/switch with
        /// mandatory braces on the token stream.
        /// </summary>
        /// <param name="tokens">The token list.</param>
        /// <returns>The processed token list.</returns>
        internal static List<Token> ApplyMandatoryBraces(List<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return tokens;
            }

            string text = Tokenizer.Reconstruct(tokens);
            bool[] isCode = Tokenizer.BuildCodeMask(text, tokens);
            var insertions = new List<TextUtils.Insertion>();

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                if (i > 0 && TextUtils.IsWordChar(text[i - 1]))
                {
                    continue;
                }

                if (TextUtils.MatchesWord(text, i, "if"))
                {
                    int afterParen = SkipParen(text, isCode, i + 2);

                    if (afterParen >= 0)
                    {
                        CollectBodyInsertions(text, isCode, afterParen,
                            insertions);
                    }
                }
                else if (TextUtils.MatchesWord(text, i, "for"))
                {
                    int afterParen = SkipParen(text, isCode, i + 3);

                    if (afterParen >= 0)
                    {
                        CollectBodyInsertions(text, isCode, afterParen,
                            insertions);
                    }
                }
                else if (TextUtils.MatchesWord(text, i, "while"))
                {
                    int afterParen = SkipParen(text, isCode, i + 5);

                    if (afterParen >= 0)
                    {
                        int nextNonWs = TextUtils.SkipWhitespace(text,
                            afterParen);

                        if (nextNonWs < text.Length && isCode[nextNonWs] &&
                            text[nextNonWs] == ';')
                        {
                            continue;
                        }

                        CollectBodyInsertions(text, isCode, afterParen,
                            insertions);
                    }
                }
                else if (TextUtils.MatchesWord(text, i, "do"))
                {
                    CollectDoWhileBodyInsertions(text, isCode, i,
                        insertions);
                }
                else if (TextUtils.MatchesWord(text, i, "switch"))
                {
                    CollectSwitchBodyInsertions(text, isCode, i,
                        insertions);
                }
                else if (TextUtils.MatchesWord(text, i, "else"))
                {
                    int afterElse = i + 4;
                    int nextNonWs = TextUtils.SkipWhitespace(text, afterElse);

                    if (TextUtils.MatchesWord(text, nextNonWs, "if"))
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
            int startPos, List<TextUtils.Insertion> insertions)
        {
            int i = TextUtils.SkipWhitespace(text, startPos);

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

            insertions.Add(new TextUtils.Insertion(stmtStart, "{\n"));
            insertions.Add(new TextUtils.Insertion(stmtEnd, "\n}"));
        }

        /// <summary>
        /// Wraps a do-while single-statement body with braces; the closing } is placed on the same line as while.
        /// </summary>
        private static void CollectDoWhileBodyInsertions(string text,
            bool[] isCode, int doPos, List<TextUtils.Insertion> insertions)
        {
            int i = TextUtils.SkipWhitespace(text, doPos + 2);

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

            int w = TextUtils.SkipWhitespace(text, stmtEnd);

            if (w >= text.Length || !TextUtils.MatchesWord(text, w, "while"))
            {
                return;
            }

            insertions.Add(new TextUtils.Insertion(stmtStart, "{\n"));
            insertions.Add(new TextUtils.Insertion(w, "\n} "));
        }

        /// <summary>
        /// Wraps a switch single-statement body with braces.
        /// </summary>
        private static void CollectSwitchBodyInsertions(string text,
            bool[] isCode, int switchPos,
            List<TextUtils.Insertion> insertions)
        {
            int afterParen = SkipParen(text, isCode, switchPos + 6);

            if (afterParen < 0)
            {
                return;
            }

            int i = TextUtils.SkipWhitespace(text, afterParen);

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
            int i = TextUtils.SkipWhitespace(text, start);

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
    }
}
