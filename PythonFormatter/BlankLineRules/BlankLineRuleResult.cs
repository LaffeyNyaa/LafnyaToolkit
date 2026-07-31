namespace PythonFormatter
{
    /// <summary>
    /// Outcome of a per-rule blank-line decision used by
    /// <see cref="BlankLineMainRules"/>.
    /// </summary>
    internal enum BlankLineRuleResult
    {
        /// <summary>The rule abstained; the dispatcher should try the next rule.</summary>
        None,

        /// <summary>The rule decided; consult the per-rule semantics for what to do.</summary>
        Decided
    }
}
