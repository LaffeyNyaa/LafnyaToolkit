using System;

using JavaFormatter;

using LafnyaToolkit.Tests;

namespace LafnyaToolkit.Tests.Idempotency
{
    /// <summary>
    /// Idempotency property tests for the Java formatter. For each
    /// representative input, the test asserts that
    /// <c>Format(Format(x)) == Format(x)</c>.
    /// </summary>
    public sealed class JavaIdempotencyTests
    {
        /// <summary>
        /// Runs all idempotency assertions for the Java formatter.
        /// </summary>
        public void TestIdempotency(bool unused)
        {
            RunCase("empty", string.Empty);
            RunCase("comment only", "// just a comment\n");

            RunCase("class with method",
                "public class Foo{public void bar(){x=1;}}");
            RunCase("nested block comment", "/* outer /* inner */ outer */");
            RunCase("interface", "public interface IFoo{void bar();}");
            RunCase("enum", "public enum Color{RED,GREEN,BLUE}");

            RunCase("imports",
                "import java.util.List;\nimport java.util.ArrayList;\nimport com.example.Foo;");
            RunCase("if else", "if(x){a();}else{b();}");
            RunCase("for loop", "for(int i=0;i<n;i++){sum+=i;}");
            RunCase("while", "while(x>0){x--;}");
            RunCase("string", "String s=\"hello\\nworld\";");

            RunCase("text block",
                "String s=\"\"\"\n        line1\n        line2\n        \"\"\";");

            RunCase("switch",
                "switch(x){case 1:a();break;case 2:b();break;default:c();break;}");

            RunCase("try catch",
                "try{a();}catch(Exception e){b();}finally{c();}");

            RunCase("package and class",
                "package com.example;public class Foo{}");

            RunCase("long line",
                "int result=someMethod(arg1,arg2,arg3,arg4,arg5,arg6,arg7,arg8,arg9);");

            RunCase("nested class",
                "public class A{public class B{public class C{}}}");

            RunCase("generics",
                "public List<Map<String,Integer>> getMap(){return new ArrayList<>();}");

            RunCase("annotation",
                "@Override public String toString(){return \"Foo\";}");
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
