using System;
using LafnyaToolkit.Core.IO;
using LafnyaToolkit.Core.Text;

namespace CppFormatter
{
    /// <summary>
    /// C++-specific line classifier derived from
    /// <see cref="LineClassifierBase"/>. Supplies the C++ block-start
    /// keyword set and overrides <see cref="IsBlockStartLine"/> and
    /// <see cref="IsBlockEndLine"/> to use the C++ keyword set
    /// (catch/class/do/else/enum/for/if/namespace/struct/switch/try/
    /// union/while) and the C++ block-end rule (treat "} else {" and
    /// "} catch (...)" as continuations rather than block ends).
    /// Stateless; the shared instance is exposed via
    /// <see cref="Instance"/>.
    /// </summary>
    internal sealed class CppLineClassifier : LineClassifierBase
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly CppLineClassifier Instance = new CppLineClassifier();

        private static readonly string[] CppBlockStartKeywords =
        {
            "namespace", "struct", "switch", "catch", "class", "while",
            "union", "enum", "else", "for", "try", "do", "if"
        };

        private CppLineClassifier()
        {
        }

        /// <inheritdoc />
        public override string[] BlockStartKeywords => CppBlockStartKeywords;

        /// <summary>
        /// Returns true for a line that starts with a C++ block-start
        /// keyword and does not end with a semicolon. Differs from the
        /// base implementation by handling the C++ keyword set and
        /// rejecting the lone <c>{</c> line.
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>True if the line introduces a brace-delimited block.</returns>
        public override bool IsBlockStartLine(string trimmed)
        {
            if (string.IsNullOrEmpty(trimmed) || trimmed == "{" || trimmed.EndsWith(";", StringComparison.Ordinal))
            {
                return false;
            }

            foreach (var kw in CppBlockStartKeywords)
            {
                if (TextUtils.StartsWithKeyword(trimmed, kw))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true for a line that ends a brace-delimited block.
        /// Treats "} else {" and "} catch (...)" as continuations into
        /// a new block (not a real block-end).
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>True if the line ends a brace-delimited block.</returns>
        public override bool IsBlockEndLine(string trimmed)
        {
            if (string.IsNullOrEmpty(trimmed) || trimmed[0] != '}')
            {
                return false;
            }

            string afterBrace = trimmed.Substring(1).TrimStart();

            if (afterBrace.Length == 0 || afterBrace == ";" || afterBrace.StartsWith("//", StringComparison.Ordinal) || afterBrace.StartsWith("/*", StringComparison.Ordinal))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(afterBrace, "else") || TextUtils.StartsWithKeyword(afterBrace, "catch"))
            {
                return false;
            }

            return true;
        }
    }
}
