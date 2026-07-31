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
        }

        private static void RunCase(string name, string input)
        {
            string first = Formatter.Instance.Format(input, string.Empty);
            string second = Formatter.Instance.Format(first, string.Empty);
            TestHarness.AssertEqual(first, second);
        }
    }
}
