using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Represents a single #include directive together with any preceding
    /// lines (preprocessor directives, blank lines, comments) that appeared
    /// between this include and the previous include.
    /// </summary>
    internal class IncludeUnit
    {
        /// <summary>Gets the preceding lines (preprocessor, blanks, etc.).</summary>
        public List<string> PrecedingLines
        {
            get;
        }

        /// <summary>Gets the raw #include directive line.</summary>
        public string IncludeLine
        {
            get;
        }

        /// <summary>
        /// Initializes a new instance of the IncludeUnit class.
        /// </summary>
        /// <param name="precedingLines">The lines preceding this include.</param>
        /// <param name="includeLine">The include directive line.</param>
        public IncludeUnit(List<string> precedingLines,
            string includeLine)
        {
            PrecedingLines = precedingLines;
            IncludeLine = includeLine;
        }
    }
}
