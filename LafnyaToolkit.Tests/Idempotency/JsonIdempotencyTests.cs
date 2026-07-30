using System;
using LafnyaToolkit.Tests;
using JsonFormatter;

namespace LafnyaToolkit.Tests.Idempotency
{
    /// <summary>
    /// Idempotency property tests for the JSON formatter. For each
    /// representative input, the test asserts that
    /// <c>Format(Format(x)) == Format(x)</c>.
    /// </summary>
    public sealed class JsonIdempotencyTests
    {
        /// <summary>
        /// Runs all idempotency assertions for the JSON formatter.
        /// </summary>
        public void TestIdempotency(bool unused)
        {
            RunCase("whitespace", "{\"a\":1}   \n\n  \n");
            RunCase("simple object", "{\"a\":1,\"b\":2}");
            RunCase("nested object", "{\"a\":{\"b\":{\"c\":1}},\"d\":[1,2,3]}");
            RunCase("array of mixed", "[1,\"two\",true,null,1.5]");
            RunCase("unicode string", "{\"name\":\"\\u4e2d\\u6587\",\"emoji\":\"\\ud83d\\ude00\"}");
            RunCase("escaped chars", "{\"q\":\"a\\\"b\\\\c\\/d\\bf\\ne\\rf\\tg\\u0001h\"}");
            RunCase("empty object", "{}");
            RunCase("empty array", "[]");
            RunCase("number formats", "{\"ints\":[0,-1,1,42,-999],\"floats\":[0.0,-0.0,1.5,1e10,2.5E-3],\"exponents\":[1e0,1E+10,1.0e-10]}");
            RunCase("long string", "{\"s\":\"" + new string('a', 100) + "\"}");
            RunCase("deeply nested", "{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":1}}}}}}");
            RunCase("leading whitespace", "   \n\n{\"a\":1}");
            RunCase("trailing whitespace", "{\"a\":1}   \n\n  ");
            RunCase("duplicate keys", "{\"a\":1,\"a\":2,\"a\":3}");
            RunCase("nested arrays", "[[[[[[1]]]]]]");
            RunCase("mixed types", "[null,true,false,0,\"\",{},[],{\"k\":\"v\"}]");
            RunCase("large object", BuildLargeObject(20));
            RunCase("null value variants", "{\"a\":null,\"b\":{\"c\":null},\"d\":[null,null]}");
            RunCase("strings with control chars", "{\"a\":\"line1\\nline2\\ttab\"}");
            RunCase("minimal array", "[1]");
        }

        private static void RunCase(string name, string input)
        {
            var formatter = JsonFormatter.JsonFormatter.Instance;
            string first = formatter.Format(input);
            string second = formatter.Format(first);
            TestHarness.AssertEqual(first, second);
        }

        private static string BuildLargeObject(int n)
        {
            var parts = new string[n];
            for (int i = 0; i < n; i++)
            {
                parts[i] = "\"k" + i + "\":" + i;
            }
            return "{" + string.Join(",", parts) + "}";
        }
    }
}
