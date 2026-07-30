using System;
using LafnyaToolkit.Tests;
using CppFormatter;

namespace LafnyaToolkit.Tests.Idempotency
{
    /// <summary>
    /// Idempotency property tests for the C++ formatter. For each
    /// representative input, the test asserts that
    /// <c>Format(Format(x)) == Format(x)</c>.
    /// </summary>
    public sealed class CppIdempotencyTests
    {
        /// <summary>
        /// Runs all idempotency assertions for the C++ formatter.
        /// </summary>
        public void TestIdempotency(bool unused)
        {
            RunCase("empty", string.Empty);
            RunCase("comment only", "// just a comment\n");
            RunCase("block comment", "/* block comment */\n");
            RunCase("class with method", "class Foo{public:void bar(){x=1;}};");
            RunCase("namespace", "namespace n{class C{public:void m(){}};}");
            RunCase("if with else", "if(x){a();}else{b();}");
            RunCase("while loop", "while(x>0){--x;}");
            RunCase("for loop", "for(int i=0;i<n;i++){sum+=i;}");
            RunCase("switch case", "switch(x){case 1:a();break;case 2:b();break;default:c();break;}");
            RunCase("string literal", "const char* s=\"hello\\nworld\";");
            RunCase("raw string", "auto s=R\"(multi\nline\nraw)\";");
            RunCase("char literal", "char c='a';");
            RunCase("preprocessor", "#include <iostream>\n#define MAX 100\n");
            RunCase("enum", "enum Color{Red,Green,Blue};");
            RunCase("template", "template<typename T>T max(T a,T b){return a>b?a:b;}");
            RunCase("include sort", "#include \"local.h\"\n#include <vector>\n#include <map>\n");
            RunCase("constructor init", "Foo::Foo():x(0),y(0),name(\"a\"){}");
            RunCase("long line", "int veryLongVariableName=someFunction(arg1,arg2,arg3,arg4,arg5,arg6);");
            RunCase("nested namespaces", "namespace a{namespace b{namespace c{class D{};}}}");
            RunCase("trailing whitespace", "int x=1;   \nint y=2;\t\n");
        }

        private static void RunCase(string name, string input)
        {
            string first = Formatter.Instance.Format(input);
            string second = Formatter.Instance.Format(first);
            TestHarness.AssertEqual(first, second);
        }
    }
}
