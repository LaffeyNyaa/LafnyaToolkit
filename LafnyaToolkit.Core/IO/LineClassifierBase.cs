using System;

namespace LafnyaToolkit.Core.IO
{
    /// <summary>
    /// Abstract base for language-specific line classifiers. Provides
    /// shared character-predicate helpers; derived classes supply the
    /// block-start keyword set and language-specific predicates
    /// (continuation indicator, statement-ending detection, etc.).
    /// </summary>
    public abstract class LineClassifierBase
    {
        /// <summary>
        /// Default continuation indicators shared across most C-family
        /// languages. Derived classes may override or extend.
        /// </summary>
        public const string DefaultContinuationIndicators = ",+-*/%()=<>&|?:.";

        /// <summary>
        /// Keywords that introduce a brace-delimited block, in the order the
        /// derived class wants them checked.
        /// </summary>
        public abstract string[] BlockStartKeywords { get; }

        /// <summary>
        /// Determines whether <paramref name="c"/> is a continuation
        /// indicator (operator or delimiter that suggests a wrapped line).
        /// </summary>
        /// <param name="c">The character to test.</param>
        /// <returns>True if the character is a continuation indicator.</returns>
        public virtual bool IsContinuationIndicator(char c)
        {
            return DefaultContinuationIndicators.IndexOf(c) >= 0;
        }

        /// <summary>
        /// Determines whether a trimmed line is a block-start line: it
        /// begins with a block-start keyword and does not end with a
        /// semicolon.
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>True if the line introduces a brace-delimited block.</returns>
        public virtual bool IsBlockStartLine(string trimmed)
        {
            if (string.IsNullOrEmpty(trimmed) || trimmed == "{" || trimmed.EndsWith(";", StringComparison.Ordinal))
            {
                return false;
            }

            foreach (var kw in BlockStartKeywords)
            {
                if (LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(trimmed, kw))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a trimmed line is a block-end line: it begins
        /// with '}' and is not followed by "else" or "catch" (which would
        /// indicate the start of a new block).
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>True if the line ends a brace-delimited block.</returns>
        public virtual bool IsBlockEndLine(string trimmed)
        {
            if (string.IsNullOrEmpty(trimmed) || trimmed[0] != '}')
            {
                return false;
            }

            string afterBrace = trimmed.Substring(1).TrimStart();

            if (afterBrace.Length == 0 || afterBrace == ";")
            {
                return true;
            }

            if (afterBrace.StartsWith("//", StringComparison.Ordinal)
                || afterBrace.StartsWith("/*", StringComparison.Ordinal))
            {
                return true;
            }

            if (LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(afterBrace, "else")
                || LafnyaToolkit.Core.Text.TextUtils.StartsWithKeyword(afterBrace, "catch"))
            {
                return false;
            }

            return true;
        }
    }
}
