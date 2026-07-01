using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Computes which lines within a switch block belong to a case body
    /// (indented one extra level beyond the case label).
    /// </summary>
    internal static class CaseScopeDetector
    {
        /// <summary>
        /// Computes which lines within a switch block belong to a case body
        /// (indented one extra level beyond the case label).
        /// </summary>
        internal static bool[] ComputeCaseScope(List<string> lines,
            string text, bool[] isCode)
        {
            var caseBody = new bool[lines.Count];
            int[] lineStarts = Tokenizer.ComputeLineStarts(lines);

            var switchRanges = new List<KeyValuePair<int, int>>();
            var braceStack = new Stack<KeyValuePair<bool, int>>();
            bool pendingSwitch = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (!isCode[i])
                {
                    continue;
                }

                char c = text[i];

                if (c == 's' && (i == 0 || !TextUtils.IsWordChar(text[i -
                    1])) &&
                    TextUtils.MatchesWord(text, i, "switch"))
                {
                    pendingSwitch = true;
                }

                if (c == '{')
                {
                    braceStack.Push(new KeyValuePair<bool, int>(pendingSwitch,
                        i));

                    pendingSwitch = false;
                }
                else if (c == '}')
                {
                    if (braceStack.Count > 0)
                    {
                        var top = braceStack.Pop();

                        if (top.Key)
                        {
                            switchRanges.Add(new KeyValuePair<int, int>(
                                top.Value, i));
                        }
                    }
                }
                else if (c == ';')
                {
                    pendingSwitch = false;
                }
            }

            switchRanges.Sort((a, b) => a.Key.CompareTo(b.Key));

            foreach (var range in switchRanges)
            {
                int braceStart = range.Key;
                int braceEnd = range.Value;
                var innerRanges = new List<KeyValuePair<int, int>>();

                foreach (var r in switchRanges)
                {
                    if (r.Key > braceStart && r.Value < braceEnd)
                    {
                        innerRanges.Add(r);
                    }
                }

                bool inCaseBody = false;

                for (int li = 0; li < lines.Count; li++)
                {
                    int ls = lineStarts[li];

                    if (ls <= braceStart || ls >= braceEnd)
                    {
                        continue;
                    }

                    int lineEndPos = ls + lines[li].Length;

                    if (braceEnd >= ls && braceEnd < lineEndPos)
                    {
                        inCaseBody = false;
                        continue;
                    }

                    bool inInner = false;

                    foreach (var ir in innerRanges)
                    {
                        if (ls > ir.Key && ls < ir.Value)
                        {
                            inInner = true;
                            break;
                        }
                    }

                    if (inInner)
                    {
                        continue;
                    }

                    string trimmed = lines[li].Trim();

                    if (IsCaseLabelLine(trimmed))
                    {
                        inCaseBody = true;
                    }
                    else if (inCaseBody)
                    {
                        caseBody[li] = true;
                    }
                }
            }

            return caseBody;
        }

        /// <summary>
        /// Determines whether a line is a case/default label line for a switch.
        /// </summary>
        internal static bool IsCaseLabelLine(string trimmed)
        {
            if (trimmed.Length == 0 || !trimmed.EndsWith(":"))
            {
                return false;
            }

            return TextUtils.StartsWithKeyword(trimmed, "case") ||
                TextUtils.StartsWithKeyword(trimmed, "default");
        }
    }
}
