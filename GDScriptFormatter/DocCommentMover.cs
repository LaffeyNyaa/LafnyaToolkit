using System.Collections.Generic;

namespace GDScriptFormatter
{
    /// <summary>
    /// Moves leading ## doc-comment blocks from the top of the file to
    /// immediately after the last file header line (class_name, extends,
    /// @tool, @icon, @static_unload).
    /// </summary>
    internal static class DocCommentMover
    {
        /// <summary>
        /// Moves leading ## doc-comment blocks from the top of the file to
        /// immediately after the last file header line (class_name, extends,
        /// @tool, @icon, @static_unload). Returns the text unchanged if no
        /// leading ## comments or no file headers are found.
        /// </summary>
        /// <param name="text">The line-ending-normalized text.</param>
        /// <returns>The text with leading ## doc comments moved after the
        /// last file header, or the original text if no move is needed.
        /// </returns>
        internal static string MoveFileDocComments(string text)
        {
            var lines = TextUtils.SplitLines(text);

            var moveIndices = new SortedSet<int>();
            int lastDocLineIdx = -1;
            int scanEnd = lines.Count;

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();

                if (trimmed.StartsWith("##"))
                {
                    if (lastDocLineIdx >= 0)
                    {
                        for (int b = lastDocLineIdx + 1; b < i; b++)
                        {
                            moveIndices.Add(b);
                        }
                    }

                    moveIndices.Add(i);
                    lastDocLineIdx = i;
                }
                else if (trimmed.Length == 0)
                {
                }
                else
                {
                    scanEnd = i;
                    break;
                }
            }

            if (lastDocLineIdx < 0)
            {
                return text;
            }

            int lastFileHeaderIdx = -1;

            for (int j = scanEnd; j < lines.Count; j++)
            {
                string trimmed = lines[j].Trim();

                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (DeclarationClassifier.IsFileHeaderLine(trimmed) &&
                    !trimmed.StartsWith("##"))
                {
                    lastFileHeaderIdx = j;
                }
                else
                {
                    break;
                }
            }

            if (lastFileHeaderIdx < 0)
            {
                return text;
            }

            var newLines = new List<string>(lines.Count);

            for (int k = 0; k < lines.Count; k++)
            {
                if (moveIndices.Contains(k))
                {
                    continue;
                }

                newLines.Add(lines[k]);

                if (k == lastFileHeaderIdx)
                {
                    foreach (int idx in moveIndices)
                    {
                        newLines.Add(lines[idx]);
                    }
                }
            }

            return string.Join("\n", newLines);
        }
    }
}
