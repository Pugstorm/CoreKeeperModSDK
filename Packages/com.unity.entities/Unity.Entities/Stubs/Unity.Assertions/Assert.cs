using System;
using System.Diagnostics;

namespace Unity.Assertions
{
    /// <summary>
    /// The intent of this class is to provide debug assert utilities that don't rely on UnityEngine, for compatibility
    /// with code that needs to run in the DOTS Runtime environment. The current implement just wraps the equivalent
    /// <see cref="UnityEngine.Assertions.Assert"/> calls.
    /// </summary>
    [DebuggerStepThrough]
    public static class Assert
    {
        /// <summary>
        /// Assert that a condition must be true. Throws an exception if the assertion fails.
        /// </summary>
        /// <param name="condition">The expression that the caller expects to be true.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void IsTrue(bool condition)
        {
            if (condition)
                return;

            UnityEngine.Assertions.Assert.IsTrue(condition);
        }

        /// <summary>
        /// Assert that a condition must be true. Throws an exception if the assertion fails.
        /// </summary>
        /// <param name="condition">The expression that the caller expects to be true.</param>
        /// <param name="message">If the assertion fails, this message will be logged.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void IsTrue(bool condition, string message)
        {
            if (condition)
                return;

            UnityEngine.Assertions.Assert.IsTrue(condition, message);
        }

        /// <summary>
        /// Assert that a condition must be false. Throws an exception if the assertion fails.
        /// </summary>
        /// <param name="condition">The expression that the caller expects to be false.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void IsFalse(bool condition)
        {
            if (!condition)
                return;

            UnityEngine.Assertions.Assert.IsFalse(condition);
        }

        /// <summary>
        /// Assert that a condition must be false. Throws an exception if the assertion fails.
        /// </summary>
        /// <param name="condition">The expression that the caller expects to be false.</param>
        /// <param name="message">If the assertion fails, this message will be logged.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void IsFalse(bool condition, string message)
        {
            if (!condition)
                return;

            UnityEngine.Assertions.Assert.IsFalse(condition, message);
        }

        /// <summary>
        /// Assert that a value is a null reference. Throws an exception if the assertion fails.
        /// </summary>
        /// <typeparam name="T">The type of the object being tested.</typeparam>
        /// <param name="value">The value that the caller expects to be a null reference.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void IsNull<T>(T value) where T : class
        {
            if(value != null)
            {
                UnityEngine.Assertions.Assert.IsNull(value);
            }
        }

        /// <summary>
        /// Assert that a value is a null reference. Throws an exception if the assertion fails.
        /// </summary>
        /// <typeparam name="T">The type of the object being tested.</typeparam>
        /// <param name="value">The value that the caller expects to be a null reference.</param>
        /// <param name="message">If the assertion fails, this message will be logged.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void IsNull<T>(T value, string message) where T : class
        {
            if (value != null)
            {
                UnityEngine.Assertions.Assert.IsNull(value, message);
            }
        }

        /// <summary>
        /// Assert that a value is not a null reference. Throws an exception if the assertion fails.
        /// </summary>
        /// <typeparam name="T">The type of the object being tested.</typeparam>
        /// <param name="value">The value that the caller expects to be a non-null reference to <typeparamref name="T"/>.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void IsNotNull<T>(T value) where T : class
        {
            if (value == null)
            {
                UnityEngine.Assertions.Assert.IsNotNull(value);
            }
        }

        /// <summary>
        /// Assert that a value is not a null reference. Throws an exception if the assertion fails.
        /// </summary>
        /// <typeparam name="T">The type of the object being tested.</typeparam>
        /// <param name="value">The value that the caller expects to be a non-null reference to <typeparamref name="T"/>.</param>
        /// <param name="message">If the assertion fails, this message will be logged.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void IsNotNull<T>(T value, string message) where T : class
        {
            if (value == null)
            {
                UnityEngine.Assertions.Assert.IsNotNull(value, message);
            }
        }

        /// <summary>
        /// Assert that two float expressions are approximately equal to each other, within a default tolerance. Throws an exception if the assertion fails.
        /// </summary>
        /// <param name="expected">The expected value of the float expression.</param>
        /// <param name="actual">The actual value of the float expression.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void AreApproximatelyEqual(float expected, float actual)
        {
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(expected, actual);
        }

        /// <summary>
        /// Assert that two float expressions are approximately equal to each other, within a default tolerance. Throws an exception if the assertion fails.
        /// </summary>
        /// <param name="expected">The expected value of the float expression.</param>
        /// <param name="actual">The actual value of the float expression.</param>
        /// <param name="message">If the assertion fails, this message will be logged.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void AreApproximatelyEqual(float expected, float actual, string message)
        {
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(expected, actual, message);
        }

        /// <summary>
        /// Assert that two float expressions are approximately equal to each other, within a caller-provided tolerance. Throws an exception if the assertion fails.
        /// </summary>
        /// <param name="expected">The expected value of the float expression.</param>
        /// <param name="actual">The actual value of the float expression.</param>
        /// <param name="tolerance">The maximum absolute difference between <paramref name="expected"/> and
        /// <paramref name="actual"/> for the two values to be considered equal.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void AreApproximatelyEqual(float expected, float actual, float tolerance)
        {
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(expected, actual, tolerance);
        }

        /// <summary>
        /// Assert that two values are exactly equal to each other, according to their type's definition of equality. Throws an exception if the assertion fails.
        /// </summary>
        /// <typeparam name="T">The type of the values to test.</typeparam>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void AreEqual<T>(T expected, T actual) where T: IEquatable<T>
        {
            if (!expected.Equals(actual))
                UnityEngine.Assertions.Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Assert that two values are exactly equal to each other, according to their type's definition of equality. Throws an exception if the assertion fails.
        /// </summary>
        /// <typeparam name="T">The type of the values to test.</typeparam>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="message">If the assertion fails, this message will be logged.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void AreEqual<T>(T expected, T actual, string message) where T: IEquatable<T>
        {
            if (!expected.Equals(actual))
            {
                UnityEngine.Assertions.Assert.AreEqual(expected, actual, message);
            }
        }

        /// <summary>
        /// Assert that two values are not equal to each other, according to their type's definition of equality. Throws an exception if the assertion fails.
        /// </summary>
        /// <typeparam name="T">The type of the values to test.</typeparam>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void AreNotEqual<T>(T expected, T actual) where T: IEquatable<T>
        {
            if (expected.Equals(actual))
                UnityEngine.Assertions.Assert.AreNotEqual(expected, actual);
        }

        /// <summary>
        /// Assert that two values are not equal to each other, according to their type's definition of equality. Throws an exception if the assertion fails.
        /// </summary>
        /// <typeparam name="T">The type of the values to test.</typeparam>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="message">If the assertion fails, this message will be logged.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void AreNotEqual<T>(T expected, T actual, string message) where T: IEquatable<T>
        {
            if (expected.Equals(actual))
            {
                UnityEngine.Assertions.Assert.AreNotEqual(expected, actual, message);
            }
        }

        /// <summary>
        /// Assert that two 32-bit integer values are equal to each other. Throws an exception if the assertion fails.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void AreEqual(int expected, int actual)
        {
            if (expected == actual)
                return;

            UnityEngine.Assertions.Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Assert that two 32-bit integer values are not equal to each other. Throws an exception if the assertion fails.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void AreNotEqual(int expected, int actual)
        {
            if (expected != actual)
                return;

            UnityEngine.Assertions.Assert.AreNotEqual(expected, actual);
        }

        /// <summary>
        /// Assert that two boolean values are equal to each other. Throws an exception if the assertion fails.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void AreEqual(bool expected, bool actual)
        {
            if (expected == actual)
                return;

            UnityEngine.Assertions.Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Assert that two boolean values are not equal to each other. Throws an exception if the assertion fails.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void AreEqual(Type expected, Type actual)
        {
            if (expected == actual)
                return;

            UnityEngine.Assertions.Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Assert that two <see cref="IntPtr"/> values are equal to each other. Throws an exception if the assertion fails.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void AreEqual(IntPtr expected, IntPtr actual)
        {
            if (expected == actual)
                return;

            UnityEngine.Assertions.Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Assert that two <see cref="IntPtr"/> values are not equal to each other. Throws an exception if the assertion fails.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        [Conditional("UNITY_ASSERTIONS")]
        public static void AreNotEqual(bool expected, bool actual)
        {
            if (expected != actual)
                return;

            UnityEngine.Assertions.Assert.AreNotEqual(expected, actual);
        }
    }
}
