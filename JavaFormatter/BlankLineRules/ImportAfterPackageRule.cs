namespace JavaFormatter
{
    /// <summary>
    /// Import-after-package rule: returns a blank line above the first
    /// import directive that follows a <c>package</c> declaration.
    /// </summary>
    internal sealed partial class BlankLineProcessor
    {
        /// <summary>
        /// Returns <see cref="BlankLineRuleResult.Decided"/> when the
        /// current line is an import directive and the previous non-blank
        /// line is a package declaration.
        /// </summary>
        internal BlankLineRuleResult ApplyImportAfterPackageRule(
            bool currentIsImport,
            bool prevIsPackage)
        {
            if (currentIsImport && prevIsPackage)
            {
                return BlankLineRuleResult.Decided;
            }

            return BlankLineRuleResult.None;
        }
    }
}
