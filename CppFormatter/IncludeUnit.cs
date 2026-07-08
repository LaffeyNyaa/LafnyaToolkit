using System.Collections.Generic;

namespace CppFormatter
{
    /// <summary>
    /// Represents a single #include directive together with any preceding
    /// lines (preprocessor directives, blank lines, comments) that appeared
    /// between this include and the previous include, or a complete
    /// #if/#ifdef/#ifndef ... #endif block that contains at least one
    /// #include directive.
    /// </summary>
    internal class IncludeUnit
    {
        /// <summary>Gets the preceding lines (preprocessor, blanks, etc.).</summary>
        public List<string> PrecedingLines
        {
            get;
        }

        /// <summary>Gets the raw #include directive line used for sorting.</summary>
        public string IncludeLine
        {
            get;
        }

        /// <summary>
        /// Gets the full block lines of an #if/#ifdef/#ifndef ... #endif
        /// block that contains #include directives. Null when this unit
        /// is a simple include without a wrapping conditional block.
        /// </summary>
        public List<string> BlockLines
        {
            get;
        }

        /// <summary>Gets whether this unit is a preprocessor conditional block.</summary>
        public bool IsBlock
        {
            get { return BlockLines != null && BlockLines.Count > 0; }
        }

        /// <summary>
        /// Initializes a new instance of the IncludeUnit class for a
        /// simple #include directive without a wrapping conditional block.
        /// </summary>
        /// <param name="precedingLines">The lines preceding this include.</param>
        /// <param name="includeLine">The include directive line.</param>
        public IncludeUnit(List<string> precedingLines,
            string includeLine)
        {
            PrecedingLines = precedingLines;
            IncludeLine = includeLine;
            BlockLines = null;
        }

        /// <summary>
        /// Initializes a new instance of the IncludeUnit class for an
        /// #if/#ifdef/#ifndef ... #endif block that contains at least one
        /// #include directive.
        /// </summary>
        /// <param name="precedingLines">The lines preceding the block.</param>
        /// <param name="includeLine">The first #include directive line inside the block, used for sorting.</param>
        /// <param name="blockLines">The full content of the #if block (from #if to #endif, inclusive).</param>
        public IncludeUnit(List<string> precedingLines,
            string includeLine,
            List<string> blockLines)
        {
            PrecedingLines = precedingLines;
            IncludeLine = includeLine;
            BlockLines = blockLines;
        }
    }
}
