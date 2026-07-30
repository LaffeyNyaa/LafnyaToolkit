using System;
using System.Collections.Generic;
using System.Reflection;

namespace LafnyaToolkit.Tests
{
    /// <summary>
    /// Lightweight test harness. Discovers all public methods of all
    /// non-abstract test classes in this assembly whose name starts with
    /// "Test" or matches the pattern "XxxTests", and invokes each method
    /// on a fresh instance. Throws are reported as failures; clean returns
    /// are reported as passes. The runner exits with code 0 if all tests
    /// pass and 1 if any test fails.
    /// </summary>
    public static class TestRunner
    {
        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">Command-line arguments. Optional first arg "snapshot-update" regenerates golden files when set.</param>
        public static int Main(string[] args)
        {
            bool updateSnapshots = args != null && args.Length > 0 && args[0] == "snapshot-update";
            int passed = 0;
            int failed = 0;
            int skipped = 0;
            var failures = new List<string>();

            var testClasses = DiscoverTestClasses();

            foreach (var cls in testClasses)
            {
                object instance;

                try
                {
                    instance = Activator.CreateInstance(cls);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("FAIL: " + cls.FullName + " (ctor): " + ex.Message);
                    failed++;
                    failures.Add(cls.FullName + " (ctor)");
                    continue;
                }

                var methods = cls.GetMethods(BindingFlags.Instance | BindingFlags.Public);

                foreach (var method in methods)
                {
                    if (!IsTestMethod(method))
                    {
                        continue;
                    }

                    string testName = cls.Name + "." + method.Name;

                    try
                    {
                        method.Invoke(instance, new object[] { updateSnapshots });
                        Console.WriteLine("PASS: " + testName);
                        passed++;
                    }
                    catch (TargetInvocationException tie)
                    {
                        var inner = tie.InnerException ?? tie;
                        Console.WriteLine("FAIL: " + testName + ": " + inner.Message);
                        failed++;
                        failures.Add(testName + ": " + inner.Message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("FAIL: " + testName + ": " + ex.Message);
                        failed++;
                        failures.Add(testName + ": " + ex.Message);
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Total: " + (passed + failed + skipped) + ", Passed: " + passed + ", Failed: " + failed + ", Skipped: " + skipped);

            if (failed > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Failures:");
                foreach (var f in failures)
                {
                    Console.WriteLine("  " + f);
                }

                return 1;
            }

            return 0;
        }

        private static List<Type> DiscoverTestClasses()
        {
            var result = new List<Type>();
            var asm = Assembly.GetExecutingAssembly();

            foreach (var t in asm.GetTypes())
            {
                if (t.IsAbstract || t.IsInterface)
                {
                    continue;
                }

                if (!t.Name.EndsWith("Tests", StringComparison.Ordinal))
                {
                    continue;
                }

                if (t.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                result.Add(t);
            }

            return result;
        }

        private static bool IsTestMethod(MethodInfo method)
        {
            if (method.ReturnType != typeof(void))
            {
                return false;
            }

            var parameters = method.GetParameters();

            if (parameters.Length != 1)
            {
                return false;
            }

            if (parameters[0].ParameterType != typeof(bool))
            {
                return false;
            }

            return true;
        }
    }
}
