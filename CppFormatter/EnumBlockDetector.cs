using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Computes whether each line lies inside an enum block.
    /// Lines inside an enum block suppress the backward continuation
    /// indicator scan so that enum member trailing commas do not force
    /// an extra indent on subsequent lines.
    /// </summary>
    internal static class EnumBlockDetector
    {
        /// <summary>
        /// Computes whether each line lies inside an enum block.
        /// </summary>
        internal static bool[] ComputeInEnumBlock(List<string> lines,
            string text, bool[] isCode)
        {
            var inEnumBlock = new bool[lines.Count];
            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

            var enumRanges = new List<KeyValuePair<int, int>>();
            int depth = 0;
            int enumDepth = -1;
            int enumStart = -1;
            bool pendingEnum = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = text[i];

                if (c == 'e' && (i == 0 || !TextUtils.IsWordChar(text[i -
                    1])) &&
                    TextUtils.MatchesWord(text, i, "enum"))
                {
                    pendingEnum = true;
                }

                if (c == '{')
                {
                    if (pendingEnum)
                    {
                        enumStart = i;
                        enumDepth = depth + 1;
                        pendingEnum = false;
                    }

                    depth++;
                }
                else if (c == '}')
                {
                    depth--;

                    if (depth < 0)
                    {
                        depth = 0;
                    }

                    if (enumDepth >= 0 && depth < enumDepth)
                    {
                        enumRanges.Add(new KeyValuePair<int, int>(enumStart,
                            i));

                        enumStart = -1;
                        enumDepth = -1;
                    }
                }
                else if (c == ';')
                {
                    pendingEnum = false;
                }
            }

            foreach (var range in enumRanges)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    // Use <= for range.Value so that a line starting exactly
                    // at the closing brace position (e.g. "};" on its own
                    // line) is still treated as inside the enum block.
                    // Without this, the backward continuation indicator scan
                    // would see the preceding enum member's trailing comma
                    // as a continuation and incorrectly indent "};" by one
                    // extra level on every formatting pass.

                    if (lineStarts[i] > range.Key &&
                        lineStarts[i] <= range.Value)
                    {
                        inEnumBlock[i] = true;
                    }
                }
            }

            return inEnumBlock;
        }
    }
}
