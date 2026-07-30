using System;

namespace LafnyaToolkit.Tests
{
    /// <summary>
    /// Thrown by a test method to indicate a failure. Caught by the
    /// <see cref="TestRunner"/> and reported.
    /// </summary>
    public sealed class TestFailureException : Exception
    {
        /// <summary>
        /// Creates a new test failure exception with the given message.
        /// </summary>
        /// <param name="message">A description of the failure.</param>
        public TestFailureException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Assertion helpers used by test classes. Each method throws
    /// <see cref="TestFailureException"/> on assertion failure.
    /// </summary>
    public static class TestHarness
    {
        /// <summary>
        /// Asserts that <paramref name="actual"/> equals <paramref name="expected"/>.
        /// </summary>
        public static void AssertEqual<T>(T expected, T actual)
        {
            if (!object.Equals(expected, actual))
            {
                throw new TestFailureException(
                    "Expected: <" + expected + ">, Actual: <" + actual + ">");
            }
        }

        /// <summary>
        /// Asserts that <paramref name="actual"/> equals <paramref name="expected"/> using string comparison.
        /// </summary>
        public static void AssertEqual(string expected, string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new TestFailureException(
                    "Expected: <" + expected + ">, Actual: <" + actual + ">");
            }
        }

        /// <summary>
        /// Asserts that <paramref name="condition"/> is true.
        /// </summary>
        public static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new TestFailureException(message ?? "Expected true");
            }
        }
    }
}
