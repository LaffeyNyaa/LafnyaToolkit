using System.Collections.Generic;

using LafnyaToolkit.Core.Text;

namespace CppFormatter
{
    /// <summary>
    /// Computes whether each line lies inside an enum block.
    /// Lines inside an enum block suppress the backward continuation
    /// indicator scan so that enum member trailing commas do not
    /// force an extra indent on subsequent lines. Stateless; the
    /// shared instance is exposed via <see cref="Instance"/>.
    /// </summary>
    internal sealed class EnumBlockDetector
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly EnumBlockDetector Instance =
            new EnumBlockDetector();

        private EnumBlockDetector()
        {
        }

        /// <summary>
        /// Computes whether each line lies inside an enum block.
        /// </summary>
        public bool[] ComputeInEnumBlock(
            List<string> lines,
            string text,
            bool[] isCode
        )
        {
            var inEnumBlock = new bool[lines.Count];
            int[] lineStarts = CppTokenizer.Instance.ComputeLineStarts(lines);

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
                    1])) && TextUtils.MatchesWord(
                        text,
                        i,
                        "enum"
                    ))
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
                    if (lineStarts[i] > range.Key && lineStarts[i] <=
                        range.Value)
                    {
                        inEnumBlock[i] = true;
                    }
                }
            }

            return inEnumBlock;
        }
    }
}
