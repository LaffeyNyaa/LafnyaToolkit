using System;

using CSharpFormatter;

using LafnyaToolkit.Tests;

namespace LafnyaToolkit.Tests.Idempotency
{
    /// <summary>
    /// Idempotency property tests for the C# formatter. For each
    /// representative input, the test asserts that
    /// <c>Format(Format(x)) == Format(x)</c>.
    /// </summary>
    public sealed class CSharpIdempotencyTests
    {
        /// <summary>
        /// Runs all idempotency assertions for the C# formatter.
        /// </summary>
        public void TestIdempotency(bool unused)
        {
            RunCase("empty", string.Empty);
            RunCase("comment only", "// just a comment\n");

            RunCase("class with method",
                "public class Foo{public void Bar(){x=1;}}");

            RunCase("interface",
                "public interface IFoo{void Bar();int Baz{get;}}");
            RunCase("enum", "public enum Color{Red,Green,Blue}");

            RunCase("using directives",
                "using System;\nusing System.Collections.Generic;\nusing MyApp.Models;");
            RunCase("if else", "if(x){A();}else{B();}");
            RunCase("for loop", "for(int i=0;i<n;i++){sum+=i;}");

            RunCase("foreach",
                "foreach(var x in items){Console.WriteLine(x);}");
            RunCase("verbatim string", "var s=@\"line1\nline2\";");
            RunCase("interpolated", "var s=$\"Hello,{name},age={age}\";");

            RunCase("property with accessors",
                "public int X{get{return x;}set{x=value;}}");

            RunCase("switch",
                "switch(x){case 1:A();break;case 2:B();break;default:C();break;}");

            RunCase("long case label split",
                "class C{void M(){switch(x){case SomeEnum.VeryLongValue:" +
                " DoSomething(); break;}}}");

            RunCase("try catch",
                "try{A();}catch(Exception e){B();}finally{C();}");
            RunCase("namespace", "namespace MyApp{public class Foo{}}");

            RunCase("long line",
                "var result=SomeMethod(arg1,arg2,arg3,arg4,arg5,arg6,arg7,arg8);");

            RunCase("nested class",
                "public class A{public class B{public class C{}}}");
            RunCase("attributes", "[Obsolete]public void Old(){}");

            RunCase("async method",
                "public async Task<int> GetAsync(){await Task.Delay(100);return 42;}");
            RunCase("trailing whitespace", "int x=1;   \nint y=2;\t\n");

            RunCase("multi-param with comma string",
                "\"String atomicity test\"\nclass C { void M() { " +
                "Run(\"C#\", \"some, comma, filled, string, argument\", " +
                "true, 100, 200, 300, 400); } }");

            RunCase("nested param indent",
                "\"Nested parameter indent test\"\nclass C { void M() { " +
                "var result = Outer(Inner(arg1, arg2, arg3, arg4), " +
                "Another(arg5, arg6, arg7, arg8), Tail()); } }");

            RunCase("lambda continuation pattern",
                "\"Lambda continuation test\"\nclass C { void M() {" +
                "Action a = () => { DoSomething(); }, token); } }");

            RunCase("using with resource acquisition",
                "\"Using resource test\"\nclass C { void M() {" +
                "using (var bmp = new System.Drawing.Bitmap(w, h, s, " +
                "System.Drawing.Imaging.PixelFormat.Format24bppRgb, ptr))" +
                "{ Use(bmp); } } }");

            RunCase("nested method calls multi-param",
                "\"Nested multi-param test\"\nclass C { void M() {" +
                "var result = Call(Inner(arg1, arg2, arg3, arg4), " +
                "Outer(arg5, arg6, arg7, arg8, arg9, arg10), " +
                "Tail()); } }");
        }

        /// <summary>
        /// Regression: the line-length splitter must treat a string
        /// literal as atomic. A long call whose arguments include a
        /// string containing commas must not be split inside the
        /// string, and the string parameter must stay whole on its
        /// own line.
        /// </summary>
        public void TestStringLiteralPreserved(bool unused)
        {
            string input =
                "\"String atomicity test\"\nclass C { void M() { " +
                "Run(\"C#\", \"some, comma, filled, string, argument\", " +
                "true, 100, 200, 300, 400); } }";
            string output = Formatter.Instance.Format(input, string.Empty);
            // The comma-filled string literal must survive byte-for-byte.
            TestHarness.AssertTrue(output.Contains(
                "\"some, comma, filled, string, argument\""),
                "comma-filled string literal was split or altered");
            // The string argument must not be merged with its
            // neighbours; the parameter separators must be preserved.
            TestHarness.AssertTrue(output.Contains("\"C#\","),
                "string argument lost its following comma");
        }

        /// <summary>
        /// Regression: the multi-parameter layout must never merge a
        /// value argument (a literal or an identifier expression) with
        /// the preceding parameter, which would delete the comma and
        /// produce invalid C# such as "JsonType.Object null". Once the
        /// parameter list closes on the current line, an unrelated
        /// following statement must not be consumed as a continuation.
        /// </summary>
        public void TestValueArgumentsPreserved(bool unused)
        {
            string input =
                "\"Value argument regression\"\nclass C { void M() { " +
                "JsonValue v = new JsonValue(JsonType.Object, null, " +
                "new List<KeyValuePair<string, JsonValue>>(), null); } }";
            string output = Formatter.Instance.Format(input, string.Empty);
            // Literal arguments keep their separating commas.
            TestHarness.AssertTrue(output.Contains("JsonType.Object,"),
                "value argument lost its following comma");

            TestHarness.AssertTrue(output.Contains(
                "new List<KeyValuePair<string, JsonValue>>(),"),
                "value argument lost its following comma");

            string continuationInput =
                "\"Continuation regression\"\nclass C { void M() { " +
                "tokens.Add(new Token(TokenKind.Code, code.ToString(), " +
                "start));\ncode.Clear(); } }";

            string continuationOutput = Formatter.Instance.Format(
                continuationInput, string.Empty);

            // Identifier arguments keep their separating commas.
            TestHarness.AssertTrue(
                continuationOutput.Contains("code.ToString(),"),
                "identifier argument lost its following comma");
            // The statement following the closed parameter list must
            // stay on its own line instead of being appended to the
            // "));" line. Its line must contain only indentation.
            int index = continuationOutput.IndexOf("code.Clear();");

            int lineStart = continuationOutput.LastIndexOf('\n',
                index - 1) + 1;

            string linePrefix = continuationOutput.Substring(
                lineStart, index - lineStart);

            TestHarness.AssertTrue(linePrefix.Trim().Length == 0,
                "following statement was merged onto the parameter " +
                "layout line");
            // Both scenarios must be idempotent.
            TestHarness.AssertEqual(output,
                Formatter.Instance.Format(output, string.Empty));

            TestHarness.AssertEqual(continuationOutput,
                Formatter.Instance.Format(continuationOutput,
                    string.Empty));
        }

        private static void RunCase(string name, string input)
        {
            string first = Formatter.Instance.Format(input, string.Empty);
            string second = Formatter.Instance.Format(first, string.Empty);
            TestHarness.AssertEqual(first, second);
        }
    }
}
