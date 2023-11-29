#if !UNITY_DOTSRUNTIME
using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Text;

// change this to change the core type under test
using FixedStringN = Unity.Collections.FixedString128Bytes;

namespace Unity.Collections.Tests
{

[TestFixture("en-US")]
[TestFixture("da-DK")]
internal class FixedStringCultureTests
{
    CultureInfo testCulture;
    CultureInfo backupCulture;

    public FixedStringCultureTests(string culture)
    {
        testCulture = CultureInfo.CreateSpecificCulture(culture);
    }

    [SetUp]
    public virtual void Setup()
    {
        backupCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = testCulture;
    }

    [TearDown]
    public virtual void TearDown()
    {
        Thread.CurrentThread.CurrentCulture = backupCulture;
    }

    [TestCase("red", 0, 0, ParseError.Syntax)]
    [TestCase("-red", 0, 0, ParseError.Syntax)]
    [TestCase("+red", 0, 0, ParseError.Syntax)]
    [TestCase("0", 1, 0, ParseError.None)]
    [TestCase("+0", 2, 0, ParseError.None)]
    [TestCase("-0", 2, 0, ParseError.None)]
    [TestCase("-1", 2, -1, ParseError.None)]
    [TestCase("100", 3, 100, ParseError.None)]
    [TestCase("+100", 4, 100, ParseError.None)]
    [TestCase("-100", 4, -100, ParseError.None)]
    [TestCase("100.50", 3, 100, ParseError.None)]
    [TestCase("-100ab", 4, -100, ParseError.None)]
    [TestCase("2147483647", 10, 2147483647, ParseError.None)]
    [TestCase("+2147483647", 11, 2147483647, ParseError.None)]
    [TestCase("-2147483648", 11, -2147483648, ParseError.None)]
    [TestCase("2147483648", 10, 0, ParseError.Overflow)]
    [TestCase("-2147483649", 11, 0, ParseError.Overflow)]
    [TestCase("2147483648000", 13, 0, ParseError.Overflow)]
    [TestCase("-2147483649000", 14, 0, ParseError.Overflow)]
    public void FixedStringNParseIntWorks(String a, int expectedOffset, int expectedOutput, ParseError expectedResult)
    {
        FixedStringN aa = new FixedStringN(a);
        int offset = 0;
        int output = 0;
        var result = aa.Parse(ref offset, ref output);
        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(expectedOffset, offset);
        if (result == ParseError.None)
        {
            Assert.AreEqual(expectedOutput, output);
        }
    }

    [TestCase("red", 0, 0u, ParseError.Syntax)]
    [TestCase("-red", 0, 0u, ParseError.Syntax)]
    [TestCase("+red", 0, 0u, ParseError.Syntax)]
    [TestCase("0", 1, 0u, ParseError.None)]
    [TestCase("+0", 2, 0u, ParseError.None)]
    [TestCase("-0", 2, 0u, ParseError.None)]
    [TestCase("-1", 2, 0u, ParseError.Overflow)]
    [TestCase("100", 3, 100u, ParseError.None)]
    [TestCase("+100", 4, 100u, ParseError.None)]
    [TestCase("-100", 4, 0u, ParseError.Overflow)]
    [TestCase("100.50", 3, 100u, ParseError.None)]
    [TestCase("-100.50", 4, 0u, ParseError.Overflow)]
    [TestCase("100ab", 3, 100u, ParseError.None)]
    [TestCase("-100ab", 4, 0u, ParseError.Overflow)]
    [TestCase("2147483647", 10, 2147483647u, ParseError.None)]
    [TestCase("-2147483648", 11, 0u, ParseError.Overflow)]
    [TestCase("2147483648", 10, 2147483648u, ParseError.None)]
    [TestCase("-2147483649", 11, 0u, ParseError.Overflow)]
    [TestCase("4294967295", 10, 4294967295u, ParseError.None)]
    [TestCase("+4294967295", 11, 4294967295u, ParseError.None)]
    [TestCase("4294967296", 10, 0u, ParseError.Overflow)]
    [TestCase("2147483648000", 13, 0u, ParseError.Overflow)]
    [TestCase("-2147483649000", 14, 0u, ParseError.Overflow)]
    public void FixedStringNParseUIntWorks(String a, int expectedOffset, uint expectedOutput, ParseError expectedResult)
    {
        FixedStringN aa = new FixedStringN(a);
        int offset = 0;
        uint output = 0;
        var result = aa.Parse(ref offset, ref output);
        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(expectedOffset, offset);
        if (result == ParseError.None)
        {
            Assert.AreEqual(expectedOutput, output);
        }
    }

    [TestCase("red", 0, ParseError.Syntax)]
    [TestCase("-red", 0, ParseError.Syntax)]
    [TestCase("+red", 0, ParseError.Syntax)]
    [TestCase("0", 1,  ParseError.None)]
    [TestCase("-1", 2, ParseError.None)]
    [TestCase("-0", 2, ParseError.None)]
    [TestCase("100", 3, ParseError.None)]
    [TestCase("+100", 4, ParseError.None)]
    [TestCase("-100", 4, ParseError.None)]
    [TestCase("100.50", 6, ParseError.None)]
    [TestCase("2147483648", 10, ParseError.None)]
    [TestCase("-2147483649", 11, ParseError.None)]
    [TestCase("-10E10", 6, ParseError.None)]
    [TestCase("-10E-10", 7, ParseError.None)]
    [TestCase("-10E+10", 7, ParseError.None)]
    [TestCase("+10E10", 6, ParseError.None)]
    [TestCase("+10E-10", 7, ParseError.None)]
    [TestCase("+10E+10", 7, ParseError.None)]
    [TestCase("10E-40", 6, ParseError.Underflow)]
    [TestCase("10E+40", 6, ParseError.Overflow)]
    // These tests are highly inconsistent among .NET versions. All 6 cases below parse correctly in
    // .NET 5 however, so once we have updated they should be tried again.
    //[TestCase("-nan", 4, ParseError.None)]
    //[TestCase("+nan", 4, ParseError.None)]
    //[TestCase("nan", 3, ParseError.None)]
    //[TestCase("-infinity", 9, ParseError.None)]
    //[TestCase("+infinity", 9, ParseError.None)]
    //[TestCase("infinity", 8, ParseError.None)]
    [TestCase("1000001",       7, ParseError.None)]
    [TestCase("10000001",      8, ParseError.None)]
    [TestCase("100000001",     9, ParseError.None)]
    [TestCase("1000000001",   10, ParseError.None)]
    [TestCase("10000000001",  11, ParseError.None)]
    [TestCase("100000000001", 12, ParseError.None)]
    public void FixedStringNParseFloat(String unlocalizedString, int expectedOffset, ParseError expectedResult)
    {
        var localizedDecimalSeparator = Convert.ToChar(Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        var localizedString = unlocalizedString.Replace('.', localizedDecimalSeparator);
        float expectedOutput = 0;
        try { expectedOutput = Single.Parse(localizedString); } catch {}
        FixedStringN nativeLocalizedString = new FixedStringN(localizedString);
        int offset = 0;
        float output = 0;
        var result = nativeLocalizedString.Parse(ref offset, ref output, localizedDecimalSeparator);
        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(expectedOffset, offset);
        if (result == ParseError.None)
        {
            Assert.AreEqual(expectedOutput, output);
        }
    }

    // While .NET 5 supports all combinations and capitalizations of "nan", "-nan", and "+nan",
    // the implementation in Unity currently only supports "NaN" specifically
    [Test]
    public void FixedStringNParseFloatNan()
    {
        FixedStringN aa = new FixedStringN("NaN");
        int offset = 0;
        float output = 0;
        var result = aa.Parse(ref offset, ref output);
        Assert.AreEqual(ParseError.None, result);
        Assert.IsTrue(Single.IsNaN(output));
    }

    // While .NET 5 supports all combinations and capitalizations of "infinity", "-infinity", and "+infinity",
    // the mono implementation in Unity currently only supports "Infinity" and "-Infinity" specifically
    // (not even "+Infinity")
    [Test]
    public void FixedStringNParseFloatInfinity()
    {
        FixedStringN aa = new FixedStringN("Infinity");
        int offset = 0;
        float output = 0;
        var result = aa.Parse(ref offset, ref output);
        Assert.AreEqual(ParseError.None, result);
        Assert.IsTrue(Single.IsPositiveInfinity(output));
    }

    [Test]
    public void FixedStringNParseFloatNegativeInfinity()
    {
        FixedStringN aa = new FixedStringN("-Infinity");
        int offset = 0;
        float output = 0;
        var result = aa.Parse(ref offset, ref output);
        Assert.AreEqual(ParseError.None, result);
        Assert.IsTrue(Single.IsNegativeInfinity(output));
    }

    [TestCase(-2147483648)]
    [TestCase(-100)]
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(100)]
    [TestCase(2147483647)]
    public void FixedStringNFormatInt(int input)
    {
        var expectedOutput = input.ToString();
        FixedStringN aa = new FixedStringN();
        var result = aa.Append(input);
        Assert.AreEqual(FormatError.None, result);
        var actualOutput = aa.ToString();
        Assert.AreEqual(expectedOutput, actualOutput);
    }

    [TestCase(-9223372036854775808L)]
    [TestCase(-100L)]
    [TestCase(-1L)]
    [TestCase(0L)]
    [TestCase(1L)]
    [TestCase(100L)]
    [TestCase(9223372036854775807L)]
    public void FixedStringNFormatLong(long input)
    {
        var expectedOutput = input.ToString();
        FixedStringN aa = new FixedStringN();
        var result = aa.Append(input);
        Assert.AreEqual(FormatError.None, result);
        var actualOutput = aa.ToString();
        Assert.AreEqual(expectedOutput, actualOutput);
    }

    [TestCase(0U)]
    [TestCase(1U)]
    [TestCase(100U)]
    [TestCase(4294967295U)]
    public void FixedStringNFormatUInt(uint input)
    {
        var expectedOutput = input.ToString();
        FixedStringN aa = new FixedStringN();
        var result = aa.Append(input);
        Assert.AreEqual(FormatError.None, result);
        var actualOutput = aa.ToString();
        Assert.AreEqual(expectedOutput, actualOutput);
    }

    [TestCase(0UL)]
    [TestCase(1UL)]
    [TestCase(100UL)]
    [TestCase(18446744073709551615UL)]
    public void FixedStringNFormatULong(ulong input)
    {
        var expectedOutput = input.ToString();
        FixedStringN aa = new FixedStringN();
        var result = aa.Append(input);
        Assert.AreEqual(FormatError.None, result);
        var actualOutput = aa.ToString();
        Assert.AreEqual(expectedOutput, actualOutput);
    }

    [TestCase(Single.NaN, FormatError.None)]
    [TestCase(Single.PositiveInfinity, FormatError.None)]
    [TestCase(Single.NegativeInfinity, FormatError.None)]
    [TestCase(0.0f, FormatError.None)]
    [TestCase(-1.0f, FormatError.None)]
    [TestCase(100.0f, FormatError.None)]
    [TestCase(-100.0f, FormatError.None)]
    [TestCase(100.5f, FormatError.None)]
    [TestCase(0.001005f, FormatError.None)]
    [TestCase(0.0001f, FormatError.None)]
    [TestCase(0.00001f, FormatError.None)]
    [TestCase(0.000001f, FormatError.None)]
    [TestCase(-1E10f, FormatError.None)]
    [TestCase(-1E-10f, FormatError.None)]
    [TestCase(3.402823E+38f, FormatError.None)]
    public void FixedStringNFormatFloat(float input, FormatError expectedResult)
    {
        var localizedDecimalSeparator = Convert.ToChar(Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        var expectedOutput = input.ToString();
        FixedStringN aa = new FixedStringN();
        var result = aa.Append(input, localizedDecimalSeparator);
        Assert.AreEqual(expectedResult, result);
        if (result == FormatError.None)
        {
            var actualOutput = aa.ToString();
            Assert.AreEqual(expectedOutput, actualOutput);
        }
    }

    [TestCase(-2147483648)]
    [TestCase(-100)]
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(100)]
    [TestCase(2147483647)]
    public void FixedStringNAppendInt(int input)
    {
        var expectedOutput = "foo" + input.ToString();
        FixedStringN aa = "foo";
        var result = aa.Append(input);
        Assert.AreEqual(FormatError.None, result);
        var actualOutput = aa.ToString();
        Assert.AreEqual(expectedOutput, actualOutput);
    }

    [TestCase(-9223372036854775808L)]
    [TestCase(-100L)]
    [TestCase(-1L)]
    [TestCase(0L)]
    [TestCase(1L)]
    [TestCase(100L)]
    [TestCase(9223372036854775807L)]
    public void FixedStringNAppendLong(long input)
    {
        var expectedOutput = "foo" + input.ToString();
        FixedStringN aa = "foo";
        var result = aa.Append(input);
        Assert.AreEqual(FormatError.None, result);
        var actualOutput = aa.ToString();
        Assert.AreEqual(expectedOutput, actualOutput);
    }

    [TestCase(0U)]
    [TestCase(1U)]
    [TestCase(100U)]
    [TestCase(4294967295U)]
    public void FixedStringNAppendUInt(uint input)
    {
        var expectedOutput = "foo" + input.ToString();
        FixedStringN aa = "foo";
        var result = aa.Append(input);
        Assert.AreEqual(FormatError.None, result);
        var actualOutput = aa.ToString();
        Assert.AreEqual(expectedOutput, actualOutput);
    }

    [TestCase(0UL)]
    [TestCase(1UL)]
    [TestCase(100UL)]
    [TestCase(18446744073709551615UL)]
    public void FixedStringNAppendULong(ulong input)
    {
        var expectedOutput = "foo" + input.ToString();
        FixedStringN aa = "foo";
        var result = aa.Append(input);
        Assert.AreEqual(FormatError.None, result);
        var actualOutput = aa.ToString();
        Assert.AreEqual(expectedOutput, actualOutput);
    }

    [TestCase(Single.NaN, FormatError.None)]
    [TestCase(Single.PositiveInfinity, FormatError.None)]
    [TestCase(Single.NegativeInfinity, FormatError.None)]
    [TestCase(0.0f, FormatError.None)]
    [TestCase(-1.0f, FormatError.None)]
    [TestCase(100.0f, FormatError.None)]
    [TestCase(-100.0f, FormatError.None)]
    [TestCase(100.5f, FormatError.None)]
    [TestCase(0.001005f, FormatError.None)]
    [TestCase(0.0001f, FormatError.None)]
    [TestCase(0.00001f, FormatError.None)]
    [TestCase(0.000001f, FormatError.None)]
    [TestCase(-1E10f, FormatError.None)]
    [TestCase(-1E-10f, FormatError.None)]
    public void FixedStringNAppendFloat(float input, FormatError expectedResult)
    {
        var localizedDecimalSeparator = Convert.ToChar(Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        var expectedOutput = "foo" + input.ToString();
        FixedStringN aa = "foo";
        var result = aa.Append(input, localizedDecimalSeparator);
        Assert.AreEqual(expectedResult, result);
        if (result == FormatError.None)
        {
            var actualOutput = aa.ToString();
            Assert.AreEqual(expectedOutput, actualOutput);
        }
    }

    [Test]
    public void FixedStringNFormatNegativeZero()
    {
        float input = -0.0f;
        var expectedOutput = input.ToString(CultureInfo.InvariantCulture);
        FixedStringN aa = new FixedStringN();
        var result = aa.Append(input);
        Assert.AreEqual(FormatError.None, result);
        var actualOutput = aa.ToString();
        Assert.AreEqual(expectedOutput, actualOutput);
    }

    [TestCase("en-US")]
    [TestCase("da-DK")]
    public void FixedStringNParseFloatLocale(String locale)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(locale);
            var localizedDecimalSeparator = Convert.ToChar(Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            float value = 1.5f;
            FixedStringN native = new FixedStringN();
            native.Append(value, localizedDecimalSeparator);
            var nativeResult = native.ToString();
            var managedResult = value.ToString();
            Assert.AreEqual(managedResult, nativeResult);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}

}
#endif
