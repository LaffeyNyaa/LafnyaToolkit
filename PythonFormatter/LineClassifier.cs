using System;

using LafnyaToolkit.Core.Text;

namespace PythonFormatter
{
    /// <summary>
    /// Classifies Python source lines by structural role: block-start
    /// keywords (def, class, if, ...), block-continuation keywords
    /// (else, elif, except, finally, case), decorators, and import
    /// statements. All keyword detection uses whole-word matching to
    /// avoid false positives on identifiers like <c>if_flag</c>.
    /// </summary>
    internal sealed class LineClassifier
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly LineClassifier Instance = new LineClassifier();

        private static readonly string[] BlockStartKeywords =
            {
            "def", "class", "if", "elif", "else", "for", "while", "try",
            "except", "finally", "with", "match", "case", "async"
        };

        private static readonly string[] BlockContinuationKeywords =
            {
            "else", "elif", "except", "finally", "case"
        };

        private LineClassifier()
        {
        }

        /// <summary>
        /// Determines whether the trimmed line introduces a new block.
        /// Recognized forms: <c>def name(...):</c>, <c>class Name(...):</c>,
        /// <c>if cond:</c>, <c>elif cond:</c>, <c>else:</c>,
        /// <c>for x in y:</c>, <c>while cond:</c>, <c>try:</c>,
        /// <c>except X:</c>, <c>finally:</c>, <c>with x as y:</c>,
        /// <c>match x:</c>, <c>case pattern:</c>, <c>async def ...</c>,
        /// and <c>async with ...</c> / <c>async for ...</c>.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a block-start keyword line;
        /// otherwise false.</returns>
        public bool IsBlockStartLine(string trimmed)
        {
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            foreach (var kw in BlockStartKeywords)
            {
                if (TextUtils.StartsWithKeyword(trimmed, kw))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the trimmed line is a block-continuation
        /// keyword: <c>else</c>, <c>elif</c>, <c>except</c>,
        /// <c>finally</c>, or <c>case</c>. These lines are attached to
        /// the preceding block and should NOT trigger a blank line
        /// above them.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a block-continuation keyword;
        /// otherwise false.</returns>
        public bool IsBlockContinuationLine(string trimmed)
        {
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            foreach (var kw in BlockContinuationKeywords)
            {
                if (TextUtils.StartsWithKeyword(trimmed, kw))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the trimmed line is a top-level (class or
        /// module level) <c>def</c> or <c>class</c> declaration. In
        /// practice this is the same as <see cref="IsBlockStartLine"/>
        /// restricted to <c>def</c> and <c>class</c> keywords, but is
        /// exposed separately for clarity in the blank-line rules.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a top-level <c>def</c> or
        /// <c>class</c> declaration; otherwise false.</returns>
        public bool IsTopLevelDefClass(string trimmed)
        {
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "async"))
            {
                int after = 5;
                int space = trimmed.IndexOf(' ', after);

                if (space > after)
                {
                    string afterAsync = trimmed.Substring(after,
                        space - after).Trim();

                    return afterAsync == "def" || afterAsync == "class";
                }
            }

            return TextUtils.StartsWithKeyword(trimmed, "def") ||
                TextUtils.StartsWithKeyword(trimmed, "class");
        }

        /// <summary>
        /// Determines whether the trimmed line is a <c>def</c>
        /// declaration (including <c>async def</c>).
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a <c>def</c> or
        /// <c>async def</c>; otherwise false.</returns>
        public bool IsDefLine(string trimmed)
        {
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "def"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "async"))
            {
                string afterAsync = trimmed.Substring(5).TrimStart();
                return TextUtils.StartsWithKeyword(afterAsync, "def");
            }

            return false;
        }

        /// <summary>
        /// Determines whether the trimmed line is a decorator line
        /// (starts with <c>@</c> followed by an identifier or dotted
        /// path). The matrix-multiplication operator (e.g.
        /// <c>a @ b</c>) is excluded by requiring the <c>@</c> to be
        /// the very first non-whitespace character on the line.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a decorator; otherwise false.</returns>
        public bool IsDecoratorLine(string trimmed)
        {
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            if (!trimmed.StartsWith("@", StringComparison.Ordinal))
            {
                return false;
            }

            if (trimmed.Length == 1)
            {
                return false;
            }

            char next = trimmed[1];
            return char.IsLetter(next) || next == '_';
        }

        /// <summary>
        /// Determines whether the trimmed line is an import statement:
        /// <c>import x</c>, <c>import x.y</c>, <c>import x as y</c>,
        /// <c>from x import y</c>, or any of the <c>from x import
        /// (a, b, c)</c> parenthesized forms. Combined imports
        /// (<c>import os, sys</c>) are also matched; the import sorter
        /// is responsible for splitting them.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is an import statement; otherwise
        /// false.</returns>
        public bool IsImportStatement(string trimmed)
        {
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "import"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "from"))
            {
                // Match `from <module> import <name(s)>`: the
                // "import" keyword must appear as a whole word after
                // the module name (and may be parenthesized for
                // multi-line imports).
                return ContainsImportKeyword(trimmed);
            }

            return false;
        }

        /// <summary>
        /// Determines whether the given line contains the
        /// <c>import</c> keyword as a whole word, after the first
        /// word. Used to recognize <c>from x import y</c> statements
        /// regardless of how deeply the module path is nested
        /// (e.g. <c>from package.sub.module import name</c>).
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if <c>import</c> appears as a whole word
        /// after the first token.</returns>
        private static bool ContainsImportKeyword(string trimmed)
        {
            int i = 0;

            while (i < trimmed.Length && TextUtils.IsWordChar(trimmed[i]))
            {
                i++;
            }

            while (i < trimmed.Length)
            {
                if (TextUtils.IsWordChar(trimmed[i]) &&
                    !char.IsWhiteSpace(trimmed[i]))
                {
                    if (TextUtils.MatchesWord(trimmed, i, "import"))
                    {
                        return true;
                    }
                }

                i++;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the trimmed line is a single-line
        /// <c>pass</c>, <c>return</c>, <c>break</c>, or
        /// <c>continue</c> statement. Such lines are sometimes
        /// visually attached to a block boundary above or below and
        /// should not trigger blank-line insertions.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a pass/return/break/continue
        /// statement; otherwise false.</returns>
        public bool IsPassReturnBreakContinue(string trimmed)
        {
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            return trimmed == "pass" || trimmed == "return" ||
                trimmed == "break" || trimmed == "continue" ||
                trimmed.StartsWith("return ", StringComparison.Ordinal) ||
                trimmed.StartsWith("pass#", StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether the trimmed line is a docstring: a
        /// triple-quoted string literal as the entire line.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <returns>True if the line is a single-line docstring;
        /// otherwise false.</returns>
        public bool IsDocstringLine(string trimmed)
        {
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            string s = trimmed.Trim();
            int len = s.Length;

            if (len < 6)
            {
                return false;
            }

            return (s.StartsWith("\"\"\"", StringComparison.Ordinal) &&
                s.EndsWith("\"\"\"", StringComparison.Ordinal)) ||
                (s.StartsWith("'''", StringComparison.Ordinal) &&
                s.EndsWith("'''", StringComparison.Ordinal));
        }
    }
}
