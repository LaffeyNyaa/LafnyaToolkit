using System.Collections.Generic;
using System.Linq;
using LafnyaToolkit.Core.Text;

namespace GDScriptFormatter
{
    /// <summary>
    /// A single top-level member block captured by
    /// <see cref="MemberReorderer"/>: the leading comments, the
    /// declaration line, and the body lines (setter/getter body or
    /// method body).
    /// </summary>
    internal struct MemberBlock
    {
        /// <summary>Comments and blank lines immediately preceding the declaration.</summary>
        public List<string> PrecedingLines;

        /// <summary>The member declaration line (e.g. var x = 1).</summary>
        public string DeclarationLine;

        /// <summary>Body lines at indent &gt; 0 (setter/getter body, continuation brackets, method body).</summary>
        public List<string> BodyLines;

        /// <summary>Classification group from <see cref="MemberClassifier.ClassifyMember"/>.</summary>
        public MemberGroup Group;
    }

    /// <summary>
    /// Physically reorders top-level class members to match the spec
    /// order (signal, enum, const, static var, @export, var, @onready,
    /// private, method). The reordering is stable within the same
    /// group.
    /// </summary>
    public sealed class MemberReorderer
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly MemberReorderer Instance = new MemberReorderer();

        private MemberReorderer()
        {
        }

        /// <summary>
        /// Reorders top-level class members in the given text to match
        /// the spec declaration order. File header lines (e.g. class_name,
        /// extends) are preserved at the top. Returns the original text
        /// when the members are already in spec order or when no members
        /// are found.
        /// </summary>
        /// <param name="text">The full source text.</param>
        /// <returns>The text with members reordered.</returns>
        public string ReorderMembers(string text)
        {
            var lines = TextUtils.SplitLines(text);
            int memberStart = 0;

            while (memberStart < lines.Count)
            {
                string trimmed = lines[memberStart].Trim();

                if (trimmed.Length == 0)
                {
                    memberStart++;
                    continue;
                }

                if (DeclarationClassifier.Instance.IsFileHeaderLine(trimmed))
                {
                    memberStart++;
                    continue;
                }

                break;
            }

            if (memberStart >= lines.Count)
            {
                return text;
            }

            var fileHeaderLines = new List<string>(memberStart);

            for (int i = 0; i < memberStart; i++)
            {
                fileHeaderLines.Add(lines[i]);
            }

            var blocks = new List<MemberBlock>();
            int idx = memberStart;

            while (idx < lines.Count)
            {
                var (leading, declLine, body, nextIdx) =
                    CollectMemberInfo(lines, idx);

                if (declLine == null)
                {
                    if (nextIdx >= lines.Count)
                    {
                        if (blocks.Count > 0)
                        {
                            blocks[blocks.Count - 1].BodyLines.AddRange(leading);
                        }

                        break;
                    }

                    idx = nextIdx + 1;
                    continue;
                }

                idx = nextIdx;

                string trimmedDecl = declLine.Trim();

                MemberGroup group =
                    MemberClassifier.Instance.ClassifyMember(trimmedDecl);

                blocks.Add(new MemberBlock
                {
                    PrecedingLines = leading,
                    DeclarationLine = declLine,
                    BodyLines = body,
                    Group = group
                });
            }

            for (int i = 0; i < blocks.Count - 1; i++)
            {
                string bTrimmed = blocks[i].DeclarationLine.Trim();

                if (bTrimmed.StartsWith("@") &&
                    !bTrimmed.StartsWith("@onready") &&
                    !bTrimmed.StartsWith("@export"))
                {
                    string nextTrimmed = blocks[i + 1].DeclarationLine.Trim();

                    if (nextTrimmed.StartsWith("var ") ||
                        nextTrimmed.StartsWith("func ") ||
                        nextTrimmed.StartsWith("signal ") ||
                        nextTrimmed.StartsWith("const ") ||
                        nextTrimmed.StartsWith("enum ") ||
                        nextTrimmed.StartsWith("static "))
                    {
                        MemberGroup nextGroup = MemberClassifier.Instance.ClassifyMember(
                            nextTrimmed);

                        blocks[i] = new MemberBlock
                        {
                            PrecedingLines = blocks[i].PrecedingLines,
                            DeclarationLine = blocks[i].DeclarationLine,
                            BodyLines = blocks[i].BodyLines,
                            Group = nextGroup
                        };
                    }
                }
            }

            bool alreadyOrdered = true;

            for (int i = 1; i < blocks.Count; i++)
            {
                if (blocks[i].Group < blocks[i - 1].Group)
                {
                    alreadyOrdered = false;
                    break;
                }
            }

            if (alreadyOrdered)
            {
                return text;
            }

            blocks = blocks.OrderBy(b => b.Group).ToList();
            var result = new List<string>(lines.Count);
            result.AddRange(fileHeaderLines);

            foreach (var block in blocks)
            {
                result.AddRange(block.PrecedingLines);
                result.Add(block.DeclarationLine);
                result.AddRange(block.BodyLines);
            }

            return string.Join("\n", result);
        }

        /// <summary>
        /// Collects one member block (leading lines, declaration line,
        /// body lines) starting at <paramref name="startIdx"/>.
        /// </summary>
        /// <param name="lines">The full line list.</param>
        /// <param name="startIdx">The starting line index.</param>
        /// <returns>A tuple of (leading lines, declaration line, body lines, next index).
        /// When no member is found (trailing comments or an indented top-level line),
        /// <c>declLine</c> is <c>null</c> and <c>body</c> is <c>null</c>.</returns>
        private (List<string> leading, string declLine, List<string> body,
            int nextIdx) CollectMemberInfo(List<string> lines, int startIdx)
        {
            int idx = startIdx;
            var leading = new List<string>();

            while (idx < lines.Count)
            {
                string trimmed = lines[idx].Trim();

                if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                {
                    leading.Add(lines[idx]);
                    idx++;
                }
                else
                {
                    break;
                }
            }

            if (idx >= lines.Count)
            {
                return (leading, null, null, idx);
            }

            if (IndentationProcessor.Instance.LineIndentLevel(lines[idx]) > 0)
            {
                return (leading, null, null, idx);
            }

            string declLine = lines[idx];
            idx++;
            string bareTrimmed = declLine.Trim();

            if ((bareTrimmed == "@onready" || bareTrimmed == "@export") &&
                idx < lines.Count)
            {
                string nextTrimmed = lines[idx].Trim();

                if (nextTrimmed.StartsWith("var ") ||
                    nextTrimmed.StartsWith("func "))
                {
                    declLine = bareTrimmed + " " + nextTrimmed;
                    idx++;
                }
            }

            var body = new List<string>();

            while (idx < lines.Count)
            {
                int bodyIndent =
                    IndentationProcessor.Instance.LineIndentLevel(lines[idx]);

                string bodyTrimmed = lines[idx].Trim();

                if (bodyIndent > 0)
                {
                    body.Add(lines[idx]);
                    idx++;
                }
                else if (bodyTrimmed.Length > 0 &&
                    (bodyTrimmed[0] == ')' || bodyTrimmed[0] == ']' ||
                    bodyTrimmed[0] == '}'))
                {
                    body.Add(lines[idx]);
                    idx++;
                }
                else if (bodyTrimmed.Length == 0)
                {
                    int peek = idx + 1;
                    int nextNonBlank = -1;

                    while (peek < lines.Count)
                    {
                        string peekTrim = lines[peek].Trim();

                        if (peekTrim.Length == 0) { peek++; continue; }
                        nextNonBlank = peek;
                        break;
                    }

                    if (nextNonBlank >= 0)
                    {
                        string peekTrim = lines[nextNonBlank].Trim();
                        int peekIndent = IndentationProcessor.Instance
                            .LineIndentLevel(lines[nextNonBlank]);

                        if (peekTrim.Length > 0 &&
                            (peekTrim[0] == ')' || peekTrim[0] == ']' ||
                            peekTrim[0] == '}'))
                        {
                            body.Add(lines[idx]);
                            idx++;
                            continue;
                        }

                        if (peekIndent > 0)
                        {
                            body.Add(lines[idx]);
                            idx++;
                            continue;
                        }
                    }

                    break;
                }
                else
                {
                    break;
                }
            }

            return (leading, declLine, body, idx);
        }
    }
}
