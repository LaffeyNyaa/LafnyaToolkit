using System;
using System.IO;

using CSharpFormatter;
using LafnyaToolkit.Core.IO;

namespace FinalVerify
{
    public class Program
    {
        public static int Main(string[] args)
        {
            string path =
                @"c:\Users\LaffeyNyaa\Desktop\Repositories\LafnyaToolkit\Samples\ImportSorter.cs";

            try
            {
                string current = FileIO.ReadAllTextAutoDetect(path);
                bool allStable = true;

                for (int i = 1; i <= 5; i++)
                {
                    string next = Formatter.Instance.Format(current, "");

                    bool changed = !string.Equals(current, next,
                        StringComparison.Ordinal);

                    Console.WriteLine($"Pass {i}: length={next.Length} changed={changed}");

                    if (changed)
                    {
                        allStable = false;
                    }

                    current = next;
                }

                Console.WriteLine($"FINAL VERDICT: {(allStable ? "IDEMPOTENT" : "NOT IDEMPOTENT")}");
                return allStable ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.GetType().FullName +
                    ": " + (ex.Message ?? "<null>"));
                return 1;
            }
        }
    }
}
