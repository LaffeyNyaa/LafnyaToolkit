using System;
using System.Collections.Generic;
using System.Text;

using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace CSharpFormatter
{
    /// <summary>
    /// Enforces mandatory curly braces for all control-flow statement
    /// bodies by wrapping single-statement bodies in a brace block.
    /// Dispatches per-keyword work to a dictionary of
    /// <see cref="IBraceEnforcerRule"/> strategies instead of a long
    /// if/else chain; adding a new keyword is a matter of registering
    /// a new rule.
    /// </summary>
    internal sealed class BraceEnforcer
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly BraceEnforcer Instance = new BraceEnforcer();

        private readonly Dictionary<string, IBraceEnforcerRule> rules;
        private BraceEnforcer()
        {
            rules = new Dictionary<string, IBraceEnforcerRule>
                (StringComparer.Ordinal)
            {
                { "if", new IfRule() },
                    { "for", new ForRule() },
                    { "foreach", new ForEachRule() },
                    { "while", new WhileRule() },
                    { "do", new DoRule() },
                    { "lock", new LockRule() },
                    { "using", new UsingRule() },
                    { "fixed", new FixedRule() },
                    { "checked", new CheckedRule() },
                    { "unchecked", new UncheckedRule() },
                    { "else", new ElseRule() }
            };
        }

        /// <summary>
        /// Wraps single-statement bodies of if/else/for/foreach/while/
        /// do-while/lock/using/fixed/checked/unchecked with mandatory
        /// braces on the token stream.
        /// </summary>
        /// <param name="tokens">The token list.</param>
        /// <returns>The processed token list.</returns>
        public List<Token> ApplyMandatoryBraces(List<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return tokens;
            }

            string text = CSharpTokenizer.Instance.Reconstruct(tokens);

            bool[] isCode = CSharpTokenizer.Instance.BuildCodeMask(text,
                tokens);

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

                foreach (var pair in rules)
                {
                    if (TextUtils.MatchesWord(text, i, pair.Key))
                    {
                        pair.Value.Apply(text, isCode, i, insertions);
                        break;
                    }
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
            return CSharpTokenizer.Instance.Tokenize(sb.ToString());
        }

        /// <summary>
        /// Replaces a single-statement body with a brace-wrapped block
        /// by appending insertion points to <paramref name="insertions"/>.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="startPos">The position to start scanning from.</param>
        /// <param name="insertions">The insertion list to populate.</param>
        private static void CollectBodyInsertions(string text,
            bool[] isCode, int startPos,
            List<Insertion> insertions)
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
                else if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }

                    if (depth == 0)
                    {
                        j++;
                        break;
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
        /// Skips a balanced pair of parentheses from the given position,
        /// returning the position after the closing paren; returns -1
        /// if not found.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="start">The position to start scanning from.</param>
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

        private sealed class IfRule : IBraceEnforcerRule
        {
            public void Apply(string text, bool[] isCode, int keywordPos, List<
                Insertion> insertions)
            {
                int afterParen = SkipParen(text, isCode, keywordPos + 2);

                if (afterParen >= 0)
                {
                    CollectBodyInsertions(text, isCode, afterParen, insertions);
                }
            }
        }

        private sealed class ForRule : IBraceEnforcerRule
        {
            public void Apply(string text, bool[] isCode, int keywordPos, List<
                Insertion> insertions)
            {
                int afterParen = SkipParen(text, isCode, keywordPos + 3);

                if (afterParen >= 0)
                {
                    CollectBodyInsertions(text, isCode, afterParen, insertions);
                }
            }
        }

        private sealed class ForEachRule : IBraceEnforcerRule
        {
            public void Apply(string text, bool[] isCode, int keywordPos, List<
                Insertion> insertions)
            {
                int afterParen = SkipParen(text, isCode, keywordPos + 7);

                if (afterParen >= 0)
                {
                    CollectBodyInsertions(text, isCode, afterParen, insertions);
                }
            }
        }

        private sealed class WhileRule : IBraceEnforcerRule
        {
            public void Apply(string text, bool[] isCode, int keywordPos, List<
                Insertion> insertions)
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
        }

        private sealed class DoRule : IBraceEnforcerRule
        {
            public void Apply(string text, bool[] isCode, int keywordPos, List<
                Insertion> insertions)
            {
                CollectBodyInsertions(text, isCode, keywordPos + 2, insertions);
            }
        }

        private sealed class LockRule : IBraceEnforcerRule
        {
            public void Apply(string text, bool[] isCode, int keywordPos, List<
                Insertion> insertions)
            {
                int afterParen = SkipParen(text, isCode, keywordPos + 4);

                if (afterParen >= 0)
                {
                    CollectBodyInsertions(text, isCode, afterParen, insertions);
                }
            }
        }

        private sealed class UsingRule : IBraceEnforcerRule
        {
            public void Apply(string text, bool[] isCode, int keywordPos, List<
                Insertion> insertions)
            {
                int afterParen = SkipParen(text, isCode, keywordPos + 5);

                if (afterParen >= 0)
                {
                    CollectBodyInsertions(text, isCode, afterParen, insertions);
                }
            }
        }

        private sealed class FixedRule : IBraceEnforcerRule
        {
            public void Apply(string text, bool[] isCode, int keywordPos, List<
                Insertion> insertions)
            {
                int afterParen = SkipParen(text, isCode, keywordPos + 5);

                if (afterParen >= 0)
                {
                    CollectBodyInsertions(text, isCode, afterParen, insertions);
                }
            }
        }

        private sealed class CheckedRule : IBraceEnforcerRule
        {
            public void Apply(string text, bool[] isCode, int keywordPos, List<
                Insertion> insertions)
            {
                CollectOptionalParenBody(text, isCode, keywordPos + 7,
                    insertions);
            }
        }

        private sealed class UncheckedRule : IBraceEnforcerRule
        {
            public void Apply(string text, bool[] isCode, int keywordPos, List<
                Insertion> insertions)
            {
                CollectOptionalParenBody(text, isCode, keywordPos + 9,
                    insertions);
            }
        }

        private sealed class ElseRule : IBraceEnforcerRule
        {
            public void Apply(string text, bool[] isCode, int keywordPos, List<
                Insertion> insertions)
            {
                int afterElse = keywordPos + 4;
                int nextNonWs = TextUtils.SkipWhitespace(text, afterElse);

                if (TextUtils.MatchesWord(text, nextNonWs, "if"))
                {
                    return;
                }

                CollectBodyInsertions(text, isCode, afterElse, insertions);
            }
        }

        /// <summary>
        /// Skips an optional (expr) and then calls
        /// <see cref="CollectBodyInsertions"/>. Used for keywords like
        /// <c>checked</c>/<c>unchecked</c> that may be followed by
        /// either <c>(expr)</c> or directly by a block/statement.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="isCode">The code mask.</param>
        /// <param name="start">The position to start scanning from.</param>
        /// <param name="insertions">The insertion list to populate.</param>
        private static void CollectOptionalParenBody(string text,
            bool[] isCode, int start,
            List<Insertion> insertions)
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
    }
}
