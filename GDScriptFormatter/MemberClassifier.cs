using static GDScriptFormatter.TextUtils;

namespace GDScriptFormatter
{
    /// <summary>
    /// Classifies GDScript top-level class members into groups and extracts member names.
    /// </summary>
    internal static class MemberClassifier
    {
        /// <summary>
        /// Determines whether a line is a top-level class member (signal/enum/const/var/func/static/@export/@onready).
        /// </summary>
        internal static bool IsTopLevelMember(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (StartsWithKeyword(trimmed, "signal"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "enum"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "const"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "static") &&
                (trimmed.Contains("var") || trimmed.Contains("func")))
            {
                return true;
            }

            if (trimmed.StartsWith("@export"))
            {
                return true;
            }

            if (trimmed.StartsWith("@onready"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "var"))
            {
                return true;
            }

            if (StartsWithKeyword(trimmed, "func"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether two top-level members belong to the same variable group.
        /// </summary>
        internal static bool IsSameGroup(string a, string b)
        {
            return ClassifyMember(a) == ClassifyMember(b);
        }

        /// <summary>
        /// Classifies a top-level member into a group (first-match-wins).
        /// Groups are ordered to match the spec: signal(0), enum(1), const(2),
        /// static var(3), @export(4), regular var(5), @onready(6), private(7),
        /// methods(8).
        /// </summary>
        internal static int ClassifyMember(string trimmed)
        {
            if (StartsWithKeyword(trimmed, "signal"))
            {
                return 0;
            }

            if (StartsWithKeyword(trimmed, "enum"))
            {
                return 1;
            }

            if (StartsWithKeyword(trimmed, "const"))
            {
                return 2;
            }

            if (StartsWithKeyword(trimmed, "static var"))
            {
                return 3;
            }

            if (trimmed.StartsWith("@export"))
            {
                return 4;
            }

            if (trimmed.StartsWith("@onready"))
            {
                return 6;
            }

            // func/class declarations (including static func) → methods group (8)

            if (StartsWithKeyword(trimmed, "func") ||
                (trimmed.StartsWith("class ") &&
                !trimmed.StartsWith("class_name")))
            {
                return 8;
            }

            if (StartsWithKeyword(trimmed, "static") &&
                trimmed.Contains("func"))
            {
                return 8;
            }

            string name = ExtractMemberName(trimmed);

            if (name.StartsWith("_"))
            {
                return 7;
            }

            if (name.Length > 0)
            {
                return 5;
            }

            return 8;
        }

        /// <summary>
        /// Extracts the member name from a member declaration. Handles static-prefixed declarations
        /// (static var, static func) by stripping the leading "static " before applying the keyword rules.
        /// </summary>
        internal static string ExtractMemberName(string trimmed)
        {
            if (trimmed.StartsWith("static "))
            {
                string rest = trimmed.Substring("static ".Length).TrimStart();

                if (rest.StartsWith("var "))
                {
                    return ExtractNameAfter(rest, "var ");
                }

                if (rest.StartsWith("func "))
                {
                    return ExtractNameAfter(rest, "func ");
                }
            }

            if (trimmed.StartsWith("var "))
            {
                return ExtractNameAfter(trimmed, "var ");
            }

            if (trimmed.StartsWith("func "))
            {
                return ExtractNameAfter(trimmed, "func ");
            }

            if (trimmed.StartsWith("signal "))
            {
                return ExtractNameAfter(trimmed, "signal ");
            }

            if (trimmed.StartsWith("const "))
            {
                return ExtractNameAfter(trimmed, "const ");
            }

            if (trimmed.StartsWith("@"))
            {
                int spaceIdx = trimmed.IndexOf(' ');

                if (spaceIdx >= 0 && spaceIdx + 1 < trimmed.Length)
                {
                    string rest = trimmed.Substring(spaceIdx + 1);

                    if (rest.StartsWith("var "))
                    {
                        return ExtractNameAfter(rest, "var ");
                    }

                    if (rest.StartsWith("func "))
                    {
                        return ExtractNameAfter(rest, "func ");
                    }
                }
            }

            return "";
        }

        /// <summary>
        /// Extracts NAME from a string of the form "keyword NAME".
        /// </summary>
        internal static string ExtractNameAfter(string s, string prefix)
        {
            int start = prefix.Length;

            while (start < s.Length && s[start] == ' ')
            {
                start++;
            }

            int end = start;

            while (end < s.Length && IsWordChar(s[end]))
            {
                end++;
            }

            if (end > start)
            {
                return s.Substring(start, end - start);
            }

            return "";
        }
    }
}
