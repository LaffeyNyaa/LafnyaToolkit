using PythonFormatter;

using LafnyaToolkit.Tests;

namespace LafnyaToolkit.Tests.Idempotency
{
    /// <summary>
    /// Idempotency property tests for the Python formatter. For each
    /// representative input, the test asserts that
    /// <c>Format(Format(x)) == Format(x)</c>.
    /// </summary>
    public sealed class PythonIdempotencyTests
    {
        /// <summary>
        /// Runs all idempotency assertions for the Python formatter.
        /// </summary>
        public void TestIdempotency(bool unused)
        {
            RunCase("empty", string.Empty);
            RunCase("comment only", "# just a comment\n");

            RunCase("single def",
                "def foo():\n    return 1\n");

            RunCase("class with methods",
                "class Foo:\n    def a(self):\n        return 1\n    def b(self):\n        return 2\n");

            RunCase("nested def",
                "def outer():\n    def inner():\n        return 1\n    return inner()\n");

            RunCase("multi-line string",
                "def f():\n    \"\"\"docstring\n    spanning\n    multiple lines\"\"\"\n    return 0\n");

            RunCase("string with code-like content",
                "def f():\n    s = 'if x: pass'\n    return s\n");

            RunCase("imports",
                "import os\nimport sys\nimport requests\nfrom pathlib import Path\n");

            RunCase("combined import",
                "import os, sys\n");

            RunCase("decorator",
                "@staticmethod\ndef f():\n    return 0\n");

            RunCase("if else",
                "def f(x):\n    if x:\n        return 1\n    else:\n        return 0\n");

            RunCase("for loop",
                "def f(xs):\n    for x in xs:\n        print(x)\n");

            RunCase("while",
                "def f(n):\n    while n > 0:\n        n -= 1\n    return n\n");

            RunCase("try except",
                "def f(t):\n    try:\n        return int(t)\n    except ValueError:\n        return 0\n");

            RunCase("with",
                "def f(path):\n    with open(path) as fp:\n        return fp.read()\n");

            RunCase("long line",
                "def f():\n    return some_long_function_name(arg1, arg2, arg3, arg4, arg5, arg6, arg7)\n");

            RunCase("trailing whitespace",
                "def f():\n    return 1   \n");

            RunCase("crlf endings",
                "def f():\r\n    return 1\r\n");

            RunCase("tab indentation",
                "def f():\n\treturn 1\n");

            RunCase("triple-quoted string with quotes inside",
                "def f():\n    return \"\"\"contains \"quotes\" inside\"\"\"\n");

            RunCase("async def",
                "async def f():\n    return 1\n");
        }

        private static void RunCase(string name, string input)
        {
            string first = Formatter.Instance.Format(input);
            string second = Formatter.Instance.Format(first);
            TestHarness.AssertEqual(first, second);
        }
    }
}
