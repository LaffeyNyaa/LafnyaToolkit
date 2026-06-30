using System.Collections.Generic;
using System.Text;

namespace JavaFormatter
{
    /// <summary>
    /// Wraps single-statement bodies of control flow keywords with mandatory braces.
    /// </summary>
    internal static class BraceEnforcer
    {
        /// <summary>
        /// Wraps single-statement bodies of control flow keywords with braces
        /// on the token stream.
        /// </summary>
        /// <param name="tokens">The token list.</param>
        /// <returns>The processed token list.</returns>
        public static List<Token> ApplyMandatoryBraces(List<Token> tokens)
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

                if (i > 0 && TextUtils.IsWordChar(text[i - 1]))
                {
                    continue;
                }

                CollectInsertionsForKeyword(text, isCode, i, insertions);
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
        /// Dispatches brace insertion for a single control flow keyword position
        /// using guard clauses instead of a deeply nested if/else chain.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="i">The keyword start position.</param>
        /// <param name="insertions">The insertion list to populate.</param>
        private static void CollectInsertionsForKeyword(string text,
            bool[] isCode, int i, List<Insertion> insertions)
        {
            if (TextUtils.MatchesWord(text, i, "if"))
            {
                CollectAfterParen(text, isCode, i + 2, insertions);
                return;
            }

            if (TextUtils.MatchesWord(text, i, "for"))
            {
                CollectAfterParen(text, isCode, i + 3, insertions);
                return;
            }

            if (TextUtils.MatchesWord(text, i, "while"))
            {
                int afterParen = SkipParen(text, isCode, i + 5);

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
                return;
            }

            if (TextUtils.MatchesWord(text, i, "do"))
            {
                CollectBodyInsertions(text, isCode, i + 2, insertions);
                return;
            }

            if (TextUtils.MatchesWord(text, i, "synchronized"))
            {
                CollectAfterParen(text, isCode, i + 12, insertions);
                return;
            }

            if (TextUtils.MatchesWord(text, i, "try"))
            {
                CollectBodyInsertions(text, isCode, i + 3, insertions);
                return;
            }

            if (TextUtils.MatchesWord(text, i, "else"))
            {
                int afterElse = i + 4;
                int nextNonWs = TextUtils.SkipWhitespace(text, afterElse);

                if (TextUtils.MatchesWord(text, nextNonWs, "if"))
                {
                    return;
                }

                CollectBodyInsertions(text, isCode, afterElse, insertions);
            }
        }

        /// <summary>
        /// Skips a parenthesised clause and collects body insertions for it.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="start">The position after the keyword.</param>
        /// <param name="insertions">The insertion list to populate.</param>
        private static void CollectAfterParen(string text, bool[] isCode,
            int start, List<Insertion> insertions)
        {
            int afterParen = SkipParen(text, isCode, start);

            if (afterParen < 0)
            {
                return;
            }

            CollectBodyInsertions(text, isCode, afterParen, insertions);
        }

        /// <summary>
        /// Replaces a single-statement body with a braced block, appending insertion points.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="startPos">The position after the controlling clause.</param>
        /// <param name="insertions">The insertion list to populate.</param>
        private static void CollectBodyInsertions(string text, bool[] isCode,
            int startPos, List<Insertion> insertions)
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
            insertions.Add(new Insertion(stmtStart, "{\n"));
            insertions.Add(new Insertion(stmtEnd, "\n}"));
        }

        /// <summary>
        /// Skips a balanced pair of parentheses starting at the given position,
        /// returning the position after the closing parenthesis; or -1 if not found.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="start">The position to start scanning.</param>
        /// <returns>The position after the closing paren, or -1.</returns>
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
