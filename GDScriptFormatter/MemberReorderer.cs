using System.Collections.Generic;
using System.Linq;

namespace GDScriptFormatter
{
    /// <summary>
    /// Represents a single top-level member block for reordering.
    /// </summary>
    internal struct MemberBlock
    {
        /// <summary>Comments and blank lines immediately preceding the declaration.</summary>
        public List<string> PrecedingLines;

        /// <summary>The member declaration line (e.g. var x = 1).</summary>
        public string DeclarationLine;

        /// <summary>Body lines at indent > 0 (setter/getter body, continuation brackets, method body).</summary>
        public List<string> BodyLines;

        /// <summary>Classification group from ClassifyMember.</summary>
        public MemberGroup Group;
    }

    /// <summary>
    /// Physically reorders top-level class members to match the spec order.
    /// </summary>
    internal static class MemberReorderer
    {
        /// <summary>
        /// Collects one member block (leading lines, declaration line, body lines)
        /// starting at <paramref name="startIdx"/>, without using ref parameters.
        /// </summary>
        /// <returns>
        /// A tuple of (leading lines, declaration line, body lines, next index).
        /// When no member is found (trailing comments or an indented top-level
        /// line), <c>declLine</c> is <c>null</c> and <c>body</c> is <c>null</c>.
        /// </returns>
        private static (List<string> leading, string declLine, List<string>
            body,
            int nextIdx) CollectMemberInfo(List<string> lines, int startIdx)
        {
            int idx = startIdx;
            var leading = new List<string>();
            // 1. Collect leading blank / comment lines.

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
                // Trailing blanks/comments — no member follows.
                return (leading, null, null, idx);
            }

            if (IndentationProcessor.LineIndentLevel(lines[idx]) > 0)
            {
                // Unexpected indented line — not a top-level member.
                return (leading, null, null, idx);
            }

            // 2. Collect the declaration line.
            string declLine = lines[idx];
            idx++;
            // Merge bare annotation on its own line (e.g. @onready\nvar x).
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

            // 3. Collect body lines (indented content, closing brackets
            //    at indent 0, and blank lines before such lines).
            var body = new List<string>();

            while (idx < lines.Count)
            {
                int bodyIndent =
                    IndentationProcessor.LineIndentLevel(lines[idx]);

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
                        int peekIndent = IndentationProcessor
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

        internal static string ReorderMembers(string text)
        {
            var lines = TextUtils.SplitLines(text);
            // Find where the member section starts — skip file headers
            // (@tool, @icon, @static_unload, class_name, extends, ## doc)
            int memberStart = 0;

            while (memberStart < lines.Count)
            {
                string trimmed = lines[memberStart].Trim();

                if (trimmed.Length == 0)
                {
                    memberStart++;
                    continue;
                }

                if (DeclarationClassifier.IsFileHeaderLine(trimmed))
                {
                    memberStart++;
                    continue;
                }

                break;
            }

            if (memberStart >= lines.Count)
            {
                return text; // no members found
            }

            // Preserve file header lines at the top.
            var fileHeaderLines = new List<string>(memberStart);

            for (int i = 0; i < memberStart; i++)
            {
                fileHeaderLines.Add(lines[i]);
            }

            // Extract member blocks from the member section.
            var blocks = new List<MemberBlock>();
            int idx = memberStart;

            while (idx < lines.Count)
            {
                var (leading, declLine, body, nextIdx) =
                    CollectMemberInfo(lines, idx);

                if (declLine == null)
                {
                    // No member found — trailing comments or an indented skip.

                    if (nextIdx >= lines.Count)
                    {
                        // Trailing comments/blanks with no member.
                        // Attach them to the last block if any.

                        if (blocks.Count > 0)
                        {
                            blocks[blocks.Count - 1].BodyLines.AddRange(
                                leading);
                        }

                        break;
                    }

                    // Indented line at top level — skip to avoid data loss.
                    idx = nextIdx + 1;
                    continue;
                }

                idx = nextIdx;

                string trimmedDecl = declLine.Trim();

                MemberGroup group =
                    MemberClassifier.ClassifyMember(trimmedDecl);

                blocks.Add(new MemberBlock
                {
                    PrecedingLines = leading,
                        DeclarationLine = declLine,
                        BodyLines = body,
                        Group = group
                });
            }

            // Post-process: match standalone annotation blocks (lines starting
            // with @ that are NOT @onready/@export, e.g. @warning_ignore,
            // @rpc) with the group of their following declaration block.
            // This ensures that when members are reordered, annotations
            // stay attached to their corresponding declarations instead of
            // being treated as separate "methods" group (group 8) blocks.

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
                        MemberGroup nextGroup = MemberClassifier.ClassifyMember(
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

            // Check if blocks are already in spec order — if so, no reorder.
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

            // Sort blocks by group. Use a stable sort so that blocks with
            // the same group retain their original file order (e.g. all
            // private vars appear in the order they were declared).
            blocks = blocks.OrderBy(b => b.Group).ToList();
            // Reassemble: file headers + reordered blocks
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
    }
}
