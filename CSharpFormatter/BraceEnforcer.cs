using System.Collections.Generic;
using System.Text;

namespace CSharpFormatter
{
    /// <summary>
    /// Enforces mandatory curly braces for all control-flow statement
    /// bodies by wrapping single-statement bodies in a brace block.
    /// </summary>
    internal static class BraceEnforcer
    {
        /// <summary>
        /// Wraps single-statement bodies of if/else/for/while etc. with
        /// mandatory braces on the token stream.
        /// </summary>
        /// <param name="tokens">The token list.</param>
        /// <returns>The processed token list.</returns>
        public static List<Token> ApplyMandatoryBraces(List<Token> tokens)
        {
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
                    ProcessParenKeyword(text, isCode, i, 2, insertions);
                    continue;
                }

                if (TextUtils.MatchesWord(text, i, "for"))
                {
                    ProcessParenKeyword(text, isCode, i, 3, insertions);
                    continue;
                }

                if (TextUtils.MatchesWord(text, i, "foreach"))
                {
                    ProcessParenKeyword(text, isCode, i, 7, insertions);
                    continue;
                }

                if (TextUtils.MatchesWord(text, i, "while"))
                {
                    ProcessWhile(text, isCode, i, insertions);
                    continue;
                }

                if (TextUtils.MatchesWord(text, i, "do"))
                {
                    CollectBodyInsertions(text, isCode, i + 2, insertions);
                    continue;
                }

                if (TextUtils.MatchesWord(text, i, "lock"))
                {
                    ProcessParenKeyword(text, isCode, i, 4, insertions);
                    continue;
                }

                if (TextUtils.MatchesWord(text, i, "using"))
                {
                    ProcessParenKeyword(text, isCode, i, 5, insertions);
                    continue;
                }

                if (TextUtils.MatchesWord(text, i, "fixed"))
                {
                    ProcessParenKeyword(text, isCode, i, 5, insertions);
                    continue;
                }

                if (TextUtils.MatchesWord(text, i, "checked"))
                {
                    CollectOptionalParenBody(text, isCode, i + 7,
                        insertions);

                    continue;
                }

                if (TextUtils.MatchesWord(text, i, "unchecked"))
                {
                    CollectOptionalParenBody(text, isCode, i + 9,
                        insertions);

                    continue;
                }

                if (TextUtils.MatchesWord(text, i, "else"))
                {
                    ProcessElse(text, isCode, i, insertions);
                    continue;
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
        /// Processes a keyword followed by a mandatory parenthesised
        /// expression (if, for, foreach, lock, using, fixed).
        /// </summary>
        private static void ProcessParenKeyword(string text, bool[] isCode,
            int keywordPos, int keywordLen,
            List<TextUtils.Insertion> insertions)
        {
            int afterParen = SkipParen(text, isCode,
                keywordPos + keywordLen);

            if (afterParen < 0)
            {
                return;
            }

            CollectBodyInsertions(text, isCode, afterParen, insertions);
        }

        /// <summary>
        /// Processes a while keyword, skipping do-while patterns.
        /// </summary>
        private static void ProcessWhile(string text, bool[] isCode,
            int keywordPos, List<TextUtils.Insertion> insertions)
        {
            int afterParen = SkipParen(text, isCode, keywordPos + 5);

            if (afterParen < 0)
            {
                return;
            }

            int nextNonWs = TextUtils.SkipWhitespace(text, afterParen);

            if (nextNonWs < text.Length && isCode[nextNonWs] &&
                text[nextNonWs] == ';')
            {
                return;
            }

            CollectBodyInsertions(text, isCode, afterParen, insertions);
        }

        /// <summary>
        /// Processes an else keyword, skipping else-if chains.
        /// </summary>
        private static void ProcessElse(string text, bool[] isCode,
            int keywordPos, List<TextUtils.Insertion> insertions)
        {
            int afterElse = keywordPos + 4;
            int nextNonWs = TextUtils.SkipWhitespace(text, afterElse);

            if (TextUtils.MatchesWord(text, nextNonWs, "if"))
            {
                return;
            }

            CollectBodyInsertions(text, isCode, afterElse, insertions);
        }

        /// <summary>
        /// Replaces a single-statement body with a brace-wrapped block by
        /// appending insertion points to <paramref name="insertions"/>.
        /// </summary>
        private static void CollectBodyInsertions(string text,
            bool[] isCode, int startPos,
            List<TextUtils.Insertion> insertions)
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
            int j = i;
            int depth = 0;

            while (j < text.Length)
            {
                if (!isCode[j])
                {
                    j++;
                    continue;
                }

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

                j++;
            }

            if (j >= text.Length)
            {
                return;
            }

            int stmtEnd = j + 1;
            insertions.Add(new TextUtils.Insertion(stmtStart, "{\n"));
            insertions.Add(new TextUtils.Insertion(stmtEnd, "\n}"));
        }

        /// <summary>
        /// Skips an optional (expr) and then calls CollectBodyInsertions.
        /// Used for keywords like checked/unchecked that may be followed by
        /// either (expr) or directly by a block/statement.
        /// </summary>
        private static void CollectOptionalParenBody(string text,
            bool[] isCode, int start,
            List<TextUtils.Insertion> insertions)
        {
            int next = TextUtils.SkipWhitespace(text, start);

            if (next < text.Length && isCode[next] && text[next] == '(')
            {
                int afterParen = SkipParen(text, isCode, next);

                if (afterParen >= 0)
                {
                    CollectBodyInsertions(text, isCode, afterParen,
                        insertions);
                }

                return;
            }

            CollectBodyInsertions(text, isCode, start, insertions);
        }

        /// <summary>
        /// Skips a balanced pair of parentheses from the given position,
        /// returning the position after the closing paren; returns -1 if
        /// not found.
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
