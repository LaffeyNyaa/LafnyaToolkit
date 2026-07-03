using static GDScriptFormatter.DeclarationClassifier;

namespace GDScriptFormatter
{
    internal static partial class BlankLineProcessor
    {
        /// <summary>
        /// When an annotation line (starting with @) immediately precedes a
        /// declaration line (func, class, var, signal, const, enum, etc.),
        /// returns non-zero to suppress blank lines between them. The
        /// annotation belongs to the declaration and should be directly
        /// adjacent.
        /// </summary>
        private static int ApplyAnnotationSuppressRule(string prevTrimmed,
            string curTrimmed)
        {
            if (IsStandaloneAnnotation(prevTrimmed) &&
                IsDeclarationLine(curTrimmed))
            {
                return 1;
            }

            return 0;
        }
    }
}
