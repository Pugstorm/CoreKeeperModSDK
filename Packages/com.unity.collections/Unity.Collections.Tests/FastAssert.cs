using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

/// <summary>
/// This class mirrors parts of the Assert API from NUnit in a sane way.
/// The problem with NUnit is that stuff like Assert.AreEqual(15, 16) creates allocations behind the scenes, so tests
/// that checks large collections will spend the vast majority of their time just checking their results.
/// You can use this by writing
///    using Assert = FastAssert;
/// at the top of your file. There are some parts of the API that you may need to fix up manually, mainly because this
/// class does not expose overloads like Assert.AreEqual(object a, object b) because that's just asking for pain.
/// </summary>
internal static class FastAssert
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IsTrue(bool b)
    {
        if (!b)
        {
            Assert.IsTrue(b);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IsTrue(bool b, string msg)
    {
        if (!b)
        {
            Assert.IsTrue(b, msg);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void True(bool b)
    {
        if (!b)
        {
            Assert.IsTrue(b);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void True(bool b, string msg)
    {
        if (!b)
        {
            Assert.IsTrue(b, msg);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IsFalse(bool b)
    {
        if (b)
        {
            Assert.IsFalse(b);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IsFalse(bool b, string msg)
    {
        if (b)
        {
            Assert.IsFalse(b, msg);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void False(bool b)
    {
        if (b)
        {
            Assert.IsFalse(b);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void False(bool b, string msg)
    {
        if (b)
        {
            Assert.IsFalse(b, msg);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AreEqual<T>(in T a, in T b) where T : IEquatable<T>
    {
        if (!a.Equals(b))
        {
            Assert.Fail($"{a} != {b}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AreEqual<T>(in T a, in T b, string msg) where T : IEquatable<T>
    {
        if (!a.Equals(b))
        {
            Assert.Fail($"{a} != {b}: {msg}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AreNotEqual<T>(in T a, in T b) where T : IEquatable<T>
    {
        if (a.Equals(b))
        {
            Assert.Fail($"{a} == {b}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AreNotEqual<T>(in T a, in T b, string msg) where T : IEquatable<T>
    {
        if (a.Equals(b))
        {
            Assert.Fail($"{a} == {b}: {msg}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LessOrEqual<T>(in T a, in T b) where T : IComparable<T>
    {
        if (a.CompareTo(b) > 0)
        {
            Assert.Fail($"{a} > {b}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LessOrEqual<T>(in T a, in T b, string msg) where T : IComparable<T>
    {
        if (a.CompareTo(b) > 0)
        {
            Assert.Fail($"{a} > {b}: {msg}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Less<T>(in T a, in T b) where T : IComparable<T>
    {
        if (a.CompareTo(b) >= 0)
        {
            Assert.Fail($"{a} >= {b}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Less<T>(in T a, in T b, string msg) where T : IComparable<T>
    {
        if (a.CompareTo(b) >= 0)
        {
            Assert.Fail($"{a} >= {b}: {msg}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GreaterOrEqual<T>(in T a, in T b) where T : IComparable<T>
    {
        if (a.CompareTo(b) < 0)
        {
            Assert.Fail($"{a} < {b}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GreaterOrEqual<T>(in T a, in T b, string msg) where T : IComparable<T>
    {
        if (a.CompareTo(b) < 0)
        {
            Assert.Fail($"{a} < {b}: {msg}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Greater<T>(in T a, in T b) where T : IComparable<T>
    {
        if (a.CompareTo(b) <= 0)
        {
            Assert.Fail($"{a} <= {b}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Greater<T>(in T a, in T b, string msg) where T : IComparable<T>
    {
        if (a.CompareTo(b) <= 0)
        {
            Assert.Fail($"{a} <= {b}: {msg}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DoesNotThrow(TestDelegate del)
    {
        Assert.DoesNotThrow(del);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Throws<T>(TestDelegate del) where T : Exception
    {
        Assert.Throws<T>(del);
    }
}
