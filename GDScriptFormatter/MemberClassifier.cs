using LafnyaToolkit.Core.Text;

namespace GDScriptFormatter
{
    /// <summary>
    /// The classification of a top-level GDScript class member. The
    /// numeric values are part of the spec and must remain stable
    /// (signal, enum, const, static var, @export, var, @onready,
    /// private _, method).
    /// </summary>
    public enum MemberGroup
    {
        Signal,
        Enum,
        Const,
        StaticVar,
        Export,
        RegularVar,
        Onready,
        Private,
        Method
    }

    /// <summary>
    /// Classifies GDScript top-level class members into groups and
    /// extracts member names. The group ordering matches the spec
    /// declaration order: signal(0), enum(1), const(2), static var(3),
    /// @export(4), regular var(5), @onready(6), private(7), methods(8).
    /// </summary>
    public sealed class MemberClassifier
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly MemberClassifier Instance = new MemberClassifier();

        private MemberClassifier()
        {
        }

        /// <summary>
        /// Determines whether a line is a top-level class member
        /// (signal/enum/const/var/func/static/@export/@onready).
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>True if the line declares a top-level class member.</returns>
        public bool IsTopLevelMember(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "signal"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "enum"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "const"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "static") &&
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

            if (TextUtils.StartsWithKeyword(trimmed, "var"))
            {
                return true;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "func"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether two top-level members belong to the same
        /// variable group.
        /// </summary>
        /// <param name="a">The first trimmed line.</param>
        /// <param name="b">The second trimmed line.</param>
        /// <returns>True if both lines classify to the same <see cref="MemberGroup"/>.</returns>
        public bool IsSameGroup(string a, string b)
        {
            MemberGroup groupA = ClassifyMember(a);
            MemberGroup groupB = ClassifyMember(b);
            return groupA == groupB;
        }

        /// <summary>
        /// Classifies a top-level member into a group (first-match-wins).
        /// Groups are ordered to match the spec: signal(0), enum(1),
        /// const(2), static var(3), @export(4), regular var(5),
        /// @onready(6), private(7), methods(8).
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>The classification group.</returns>
        public MemberGroup ClassifyMember(string trimmed)
        {
            if (TextUtils.StartsWithKeyword(trimmed, "signal"))
            {
                return MemberGroup.Signal;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "enum"))
            {
                return MemberGroup.Enum;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "const"))
            {
                return MemberGroup.Const;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "static var"))
            {
                return MemberGroup.StaticVar;
            }

            if (trimmed.StartsWith("@export"))
            {
                return MemberGroup.Export;
            }

            if (trimmed.StartsWith("@onready"))
            {
                return MemberGroup.Onready;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "func") ||
                (trimmed.StartsWith("class ") &&
                !trimmed.StartsWith("class_name")))
            {
                return MemberGroup.Method;
            }

            if (TextUtils.StartsWithKeyword(trimmed, "static") &&
                trimmed.Contains("func"))
            {
                return MemberGroup.Method;
            }

            string name = ExtractMemberName(trimmed);

            if (name.StartsWith("_"))
            {
                return MemberGroup.Private;
            }

            if (name.Length > 0)
            {
                return MemberGroup.RegularVar;
            }

            return MemberGroup.Method;
        }

        /// <summary>
        /// Extracts the member name from a member declaration. Handles
        /// static-prefixed declarations (static var, static func) by
        /// stripping the leading "static " before applying the keyword
        /// rules.
        /// </summary>
        /// <param name="trimmed">The trimmed line text.</param>
        /// <returns>The extracted member name, or empty string if none.</returns>
        public string ExtractMemberName(string trimmed)
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
        /// <param name="s">The source string.</param>
        /// <param name="prefix">The keyword prefix including trailing space.</param>
        /// <returns>The identifier after the prefix; empty if none.</returns>
        public string ExtractNameAfter(string s, string prefix)
        {
            int start = prefix.Length;

            while (start < s.Length && s[start] == ' ')
            {
                start++;
            }

            int end = start;

            while (end < s.Length && TextUtils.IsWordChar(s[end]))
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
