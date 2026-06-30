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
        /// Physically reorders top-level class members to match the spec order
        /// (signal → enum → const → static var → @export → regular var →
        /// @onready → private → methods). Members already in spec order are
        /// left unchanged. Multi-line members (var with setter, continuation
        /// brackets, method bodies) are moved as a unit. Comments immediately
        /// preceding a member stay attached through the reorder.
        /// </summary>
        private static List<string> CollectLeadingLines(List<string> lines,
            ref int idx)
        {
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

            return leading;
        }

        private static List<string> CollectBodyLines(List<string> lines,
            ref int idx)
        {
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

                        int peekIndent =
                            IndentationProcessor.LineIndentLevel(lines[nextNonBlank]);

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

            return body;
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
                var leading = CollectLeadingLines(lines, ref idx);

                if (idx >= lines.Count)
                {
                    // Trailing comments/blanks with no member — leave as trailing.
                    // Attach them to the last block if any, otherwise discard.
                    // (The CollapseBlankLines / TrimTrailingWhitespace passes
                    // will clean up excess trailing whitespace.)

                    if (blocks.Count > 0)
                    {
                        blocks[blocks.Count - 1].BodyLines.AddRange(leading);
                    }

                    break;
                }

                // Check if this line is a top-level member declaration
                // (at indent 0).
                int lineIndent = IndentationProcessor.LineIndentLevel(
                    lines[idx]);

                if (lineIndent > 0)
                {
                    // Not a top-level member — something unusual.
                    // Skip it to avoid data loss.
                    leading.Add(lines[idx]);
                    idx++;
                    continue;
                }

                string declLine = lines[idx];
                idx++;
                // Handle bare annotations on their own line
                // (e.g., @onready\nvar x). If the declaration line
                // is just "@onready" or "@export" without a var/func
                // on the same line, merge it with the next declaration
                // line so that the member keeps its annotation.
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

                var body = CollectBodyLines(lines, ref idx);

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
