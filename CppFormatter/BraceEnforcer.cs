using System;
using System.Collections.Generic;
using System.Text;

using LafnyaToolkit.Core.Text;
using LafnyaToolkit.Core.Tokenization;

namespace CppFormatter
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
                    { "while", new WhileRule() },
                    { "do", new DoRule() },
                    { "switch", new SwitchRule() },
                    { "else", new ElseRule() }
            };
        }

        /// <summary>
        /// Wraps single-statement bodies of if/else/for/while/do-while/
        /// switch with mandatory braces on the token stream.
        /// </summary>
        /// <param name="tokens">The token list.</param>
        /// <returns>The processed token list.</returns>
        public List<Token> ApplyMandatoryBraces(List<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return tokens;
            }

            string text = CppTokenizer.Instance.Reconstruct(tokens);
            bool[] isCode = CppTokenizer.Instance.BuildCodeMask(text, tokens);
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
            return CppTokenizer.Instance.Tokenize(sb.ToString());
        }

        /// <summary>
        /// Replaces a single-statement body with a braced block by
        /// appending insertion points. The opening <c>{</c> is inserted
        /// at the statement start and the closing <c>}</c> just after
        /// the statement's terminating semicolon.
        /// </summary>
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
            int stmtEnd = ScanStatementEnd(text, isCode, i);

            if (stmtEnd < 0)
            {
                return;
            }

            insertions.Add(new Insertion(stmtStart, "{\n"));
            insertions.Add(new Insertion(stmtEnd, "\n}"));
        }

        /// <summary>
        /// Scans a statement starting from <paramref name="startPos"/>,
        /// tracking bracket depth, and returns the position immediately
        /// after the first semicolon encountered at depth 0, or -1 if
        /// no such semicolon is found.
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
        /// Skips a balanced pair of parentheses from the given position;
        /// returns the position after the closing <c>)</c> or -1 if not
        /// well-formed.
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
                int i = TextUtils.SkipWhitespace(text, keywordPos + 2);

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

                if (w >= text.Length || !TextUtils.MatchesWord(text, w,
                    "while"))
                {
                    return;
                }

                insertions.Add(new Insertion(stmtStart, "{\n"));
                insertions.Add(new Insertion(w, "\n} "));
            }
        }

        private sealed class SwitchRule : IBraceEnforcerRule
        {
            public void Apply(string text, bool[] isCode, int keywordPos, List<
                Insertion> insertions)
            {
                int afterParen = SkipParen(text, isCode, keywordPos + 6);

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
    }
}
