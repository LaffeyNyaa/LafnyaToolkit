namespace GDScriptFormatter
{
    /// <summary>
    /// Annotation suppression rule: when an annotation line (starting
    /// with @) immediately precedes a declaration line (func, class,
    /// var, signal, const, enum, etc.), returns non-zero to suppress
    /// blank lines between them. The annotation belongs to the
    /// declaration and should be directly adjacent.
    /// </summary>
    public sealed partial class BlankLineProcessor
    {
        private static int ApplyAnnotationSuppressRule(string prevTrimmed,
            string curTrimmed)
        {
            if (IsStandaloneAnnotation(prevTrimmed) &&
                DeclarationClassifier.Instance.IsDeclarationLine(curTrimmed))
            {
                return 1;
            }

            return 0;
        }
    }
}
