using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.Tests;

[TestFixture]
internal class UTF8ArrayUnsafeUtilityTests : CollectionsTestCommonBase
{
    [TestCase("The Quick Brown Fox Jumps Over The Lazy Dog")]
    [TestCase("Albert osti fagotin ja töräytti puhkuvan melodian.", TestName = "{m}(Finnish)")]
    [TestCase("Franz jagt im komplett verwahrlosten Taxi quer durch Bayern.", TestName = "{m}(German)")]
    [TestCase("איך בלש תפס גמד רוצח עז קטנה?", TestName = "{m}(Hebrew)")]
    [TestCase("PORTEZ CE VIEUX WHISKY AU JUGE BLOND QUI FUME.", TestName = "{m}(French)")]
    [TestCase("いろはにほへとちりぬるをわかよたれそつねならむうゐのおくやまけふこえてあさきゆめみしゑひもせす", TestName = "{m}(Japanese)")]
    [TestCase("키스의 고유조건은 입술끼리 만나야 하고 특별한 기술은 필요치 않다.", TestName = "{m}(Korean)")]
    public void CopyTest(string text)
    {
        unsafe
        {
            int bytes = text.Length*4;
            int chars = text.Length*2;
            var destByte = stackalloc byte[bytes];
            var destChar = stackalloc char[chars];
            var srcByte = stackalloc byte[bytes];
            var srcChar = stackalloc char[chars];
            int byteLength = 0;
            int charLength = text.Length;
            fixed(char *t = text)
            {
                Unicode.Utf16ToUtf8(t, text.Length, srcByte, out byteLength, bytes);
                UnsafeUtility.MemCpy(srcChar, t, charLength*sizeof(char));
            }
            CopyError shouldPass, shouldFail;
            int destLengthInt = default;
            ushort destLengthUshort = default;

            shouldPass = UTF8ArrayUnsafeUtility.Copy(destByte, out destLengthInt, bytes, srcChar, charLength);
            shouldFail = UTF8ArrayUnsafeUtility.Copy(destByte, out destLengthInt, destLengthInt-1, srcChar, charLength);
            Assert.AreEqual(CopyError.None, shouldPass);
            Assert.AreEqual(CopyError.Truncation, shouldFail);

            shouldPass = UTF8ArrayUnsafeUtility.Copy(destByte, out destLengthUshort, (ushort)bytes, srcChar, charLength);
            shouldFail = UTF8ArrayUnsafeUtility.Copy(destByte, out destLengthUshort, (ushort)(destLengthUshort-1), srcChar, charLength);
            Assert.AreEqual(CopyError.None, shouldPass);
            Assert.AreEqual(CopyError.Truncation, shouldFail);

            shouldPass = UTF8ArrayUnsafeUtility.Copy(destByte, out destLengthInt, bytes, srcByte, byteLength);
            shouldFail = UTF8ArrayUnsafeUtility.Copy(destByte, out destLengthInt, destLengthInt-1, srcByte, byteLength);
            Assert.AreEqual(CopyError.None, shouldPass);
            Assert.AreEqual(CopyError.Truncation, shouldFail);

            shouldPass = UTF8ArrayUnsafeUtility.Copy(destByte, out destLengthUshort, (ushort)bytes, srcByte, (ushort)byteLength);
            shouldFail = UTF8ArrayUnsafeUtility.Copy(destByte, out destLengthUshort, (ushort)(destLengthUshort-1), srcByte, (ushort)byteLength);
            Assert.AreEqual(CopyError.None, shouldPass);
            Assert.AreEqual(CopyError.Truncation, shouldFail);

            shouldPass = UTF8ArrayUnsafeUtility.Copy(destChar, out destLengthInt, chars, srcByte, byteLength);
            shouldFail = UTF8ArrayUnsafeUtility.Copy(destChar, out destLengthInt, destLengthInt-1, srcByte, byteLength);
            Assert.AreEqual(CopyError.None, shouldPass);
            Assert.AreEqual(CopyError.Truncation, shouldFail);

            shouldPass = UTF8ArrayUnsafeUtility.Copy(destChar, out destLengthUshort, (ushort)chars, srcByte, (ushort)byteLength);
            shouldFail = UTF8ArrayUnsafeUtility.Copy(destChar, out destLengthUshort, (ushort)(destLengthUshort-1), srcByte, (ushort)byteLength);
            Assert.AreEqual(CopyError.None, shouldPass);
            Assert.AreEqual(CopyError.Truncation, shouldFail);
        }
    }

    // public static FormatError AppendUTF8Bytes(byte* dest, ref int destLength, int destCapacity, byte* src, int srcLength)
    [TestCase("The Quick Brown Fox Jumps Over The Lazy Dog")]
    [TestCase("Albert osti fagotin ja töräytti puhkuvan melodian.", TestName = "{m}(Finnish)")]
    [TestCase("Franz jagt im komplett verwahrlosten Taxi quer durch Bayern.", TestName = "{m}(German)")]
    [TestCase("איך בלש תפס גמד רוצח עז קטנה?", TestName = "{m}(Hebrew)")]
    [TestCase("PORTEZ CE VIEUX WHISKY AU JUGE BLOND QUI FUME.", TestName = "{m}(French)")]
    [TestCase("いろはにほへとちりぬるをわかよたれそつねならむうゐのおくやまけふこえてあさきゆめみしゑひもせす", TestName = "{m}(Japanese)")]
    [TestCase("키스의 고유조건은 입술끼리 만나야 하고 특별한 기술은 필요치 않다.", TestName = "{m}(Korean)")]
    public void AppendUTF8BytesTest(string text)
    {
        unsafe
        {
            ushort bytes = (ushort)(text.Length*4);
            var destByte = stackalloc byte[bytes];
            var srcByte = stackalloc byte[bytes];
            int byteLength = 0;
            fixed(char *t = text)
            {
                Unicode.Utf16ToUtf8(t, text.Length, srcByte, out byteLength, bytes);
            }
            FormatError shouldPass, shouldFail;
            int destLength;
            destLength = 0;
            shouldPass = UTF8ArrayUnsafeUtility.AppendUTF8Bytes(destByte, ref destLength, bytes, srcByte, byteLength);
            destLength = 0;
            shouldFail = UTF8ArrayUnsafeUtility.AppendUTF8Bytes(destByte, ref destLength, byteLength-1, srcByte, byteLength);
            Assert.AreEqual(FormatError.None, shouldPass);
            Assert.AreEqual(FormatError.Overflow, shouldFail);
        }
    }

    // public static CopyError Append(byte *dest, ref ushort destLength, ushort destUTF8MaxLengthInBytes, byte *src, ushort srcLength)
    // public static CopyError Append(byte *dest, ref ushort destLength, ushort destUTF8MaxLengthInBytes, char *src, int srcLength)
    // public static CopyError Append(char *dest, ref ushort destLength, ushort destUCS2MaxLengthInChars, byte *src, ushort srcLength)
    [TestCase("The Quick Brown Fox Jumps Over The Lazy Dog")]
    [TestCase("Albert osti fagotin ja töräytti puhkuvan melodian.", TestName = "{m}(Finnish)")]
    [TestCase("Franz jagt im komplett verwahrlosten Taxi quer durch Bayern.", TestName = "{m}(German)")]
    [TestCase("איך בלש תפס גמד רוצח עז קטנה?", TestName = "{m}(Hebrew)")]
    [TestCase("PORTEZ CE VIEUX WHISKY AU JUGE BLOND QUI FUME.", TestName = "{m}(French)")]
    [TestCase("いろはにほへとちりぬるをわかよたれそつねならむうゐのおくやまけふこえてあさきゆめみしゑひもせす", TestName = "{m}(Japanese)")]
    [TestCase("키스의 고유조건은 입술끼리 만나야 하고 특별한 기술은 필요치 않다.", TestName = "{m}(Korean)")]
    public void AppendTest(string text)
    {
        unsafe
        {
            ushort bytes = (ushort)(text.Length*4);
            ushort chars = (ushort)(text.Length*2);
            var destByte = stackalloc byte[bytes];
            var destChar = stackalloc char[chars];
            var srcByte = stackalloc byte[bytes];
            var srcChar = stackalloc char[chars];
            int byteLength = 0;
            int charLength = text.Length;
            fixed(char *t = text)
            {
                Unicode.Utf16ToUtf8(t, text.Length, srcByte, out byteLength, bytes);
                UnsafeUtility.MemCpy(srcChar, t, charLength*sizeof(char));
            }
            CopyError shouldPass, shouldFail;
            ushort destLength = default;
            ushort tooFewBytes = 0;
            ushort tooFewChars = 0;

            destLength = 0;
            shouldPass = UTF8ArrayUnsafeUtility.Append(destByte, ref destLength, bytes, srcByte, (ushort)charLength);
            tooFewBytes = (ushort)(destLength - 1);
            destLength = 0;
            shouldFail = UTF8ArrayUnsafeUtility.Append(destByte, ref destLength, tooFewBytes, srcByte, (ushort)charLength);
            Assert.AreEqual(CopyError.None, shouldPass);
            Assert.AreEqual(CopyError.Truncation, shouldFail);

            destLength = 0;
            shouldPass = UTF8ArrayUnsafeUtility.Append(destByte, ref destLength, bytes, srcChar, charLength);
            tooFewBytes = (ushort)(destLength - 1);
            destLength = 0;
            shouldFail = UTF8ArrayUnsafeUtility.Append(destByte, ref destLength, tooFewBytes, srcChar, charLength);
            Assert.AreEqual(CopyError.None, shouldPass);
            Assert.AreEqual(CopyError.Truncation, shouldFail);

            destLength = 0;
            shouldPass = UTF8ArrayUnsafeUtility.Append(destChar, ref destLength, chars, srcByte, (ushort)charLength);
            tooFewChars = (ushort)(destLength - 1);
            destLength = 0;
            shouldFail = UTF8ArrayUnsafeUtility.Append(destChar, ref destLength, tooFewChars, srcByte, (ushort)charLength);
            Assert.AreEqual(CopyError.None, shouldPass);
            Assert.AreEqual(CopyError.Truncation, shouldFail);
        }
    }

    [TestCase("The Quick Brown Fox Jumps Over The Lazy Dog")]
    [TestCase("Albert osti fagotin ja töräytti puhkuvan melodian.", TestName = "{m}(Finnish)")]
    [TestCase("Franz jagt im komplett verwahrlosten Taxi quer durch Bayern.", TestName = "{m}(German)")]
    [TestCase("איך בלש תפס גמד רוצח עז קטנה?", TestName = "{m}(Hebrew)")]
    [TestCase("PORTEZ CE VIEUX WHISKY AU JUGE BLOND QUI FUME.", TestName = "{m}(French)")]
    [TestCase("いろはにほへとちりぬるをわかよたれそつねならむうゐのおくやまけふこえてあさきゆめみしゑひもせす", TestName = "{m}(Japanese)")]
    [TestCase("키스의 고유조건은 입술끼리 만나야 하고 특별한 기술은 필요치 않다.", TestName = "{m}(Korean)")]
    public void EqualsUTF8BytesTest(string text)
    {
        unsafe
        {
            ushort bytes = (ushort)(text.Length*4);
            var srcByte0 = stackalloc byte[bytes];
            var srcByte1 = stackalloc byte[bytes];
            int byteLength = 0;
            fixed(char *t = text)
            {
                Unicode.Utf16ToUtf8(t, text.Length, srcByte0, out byteLength, bytes);
                Unicode.Utf16ToUtf8(t, text.Length, srcByte1, out byteLength, bytes);
            }
            bool shouldPass, shouldFail;
            shouldPass = UTF8ArrayUnsafeUtility.EqualsUTF8Bytes(srcByte0, byteLength, srcByte1, byteLength);
            shouldFail = UTF8ArrayUnsafeUtility.EqualsUTF8Bytes(srcByte0, byteLength, srcByte1, byteLength-1);
            Assert.AreEqual(true, shouldPass);
            Assert.AreEqual(false, shouldFail);
        }
    }

    [TestCase("PORTEZ", "VIEUX", TestName = "{m}(French)")]
    [TestCase("いろは", "にほへ", TestName = "{m}(Japanese)")]
    [TestCase("고유조건은", "키스의", TestName = "{m}(Korean)")]
    public unsafe void StrCmpTest(string utf16A, string utf16B)
    {
        FixedString32Bytes utf8A = utf16A;
        FixedString32Bytes utf8B = utf16B;
        byte *utf8BufferA = utf8A.GetUnsafePtr();
        byte *utf8BufferB = utf8B.GetUnsafePtr();
        fixed(char *utf16BufferA = utf16A)
        fixed(char *utf16BufferB = utf16B)
        {
            Assert.IsTrue(0 > UTF8ArrayUnsafeUtility.StrCmp(utf8BufferA, utf8A.Length, utf8BufferB, utf8B.Length));
            Assert.IsTrue(0 > UTF8ArrayUnsafeUtility.StrCmp(utf16BufferA, utf16A.Length, utf8BufferB, utf8B.Length));
            Assert.IsTrue(0 > UTF8ArrayUnsafeUtility.StrCmp(utf8BufferA, utf8A.Length, utf16BufferB, utf16B.Length));
            Assert.IsTrue(0 > UTF8ArrayUnsafeUtility.StrCmp(utf16BufferA, utf16A.Length, utf16BufferB, utf16B.Length));

            Assert.AreEqual(0, UTF8ArrayUnsafeUtility.StrCmp(utf8BufferA, utf8A.Length, utf8BufferA, utf8A.Length));
            Assert.AreEqual(0, UTF8ArrayUnsafeUtility.StrCmp(utf16BufferA, utf16A.Length, utf8BufferA, utf8A.Length));
            Assert.AreEqual(0, UTF8ArrayUnsafeUtility.StrCmp(utf8BufferA, utf8A.Length, utf16BufferA, utf16A.Length));
            Assert.AreEqual(0, UTF8ArrayUnsafeUtility.StrCmp(utf16BufferA, utf16A.Length, utf16BufferA, utf16A.Length));

            Assert.IsTrue(0 < UTF8ArrayUnsafeUtility.StrCmp(utf8BufferB, utf8B.Length, utf8BufferA, utf8A.Length));
            Assert.IsTrue(0 < UTF8ArrayUnsafeUtility.StrCmp(utf16BufferB, utf16B.Length, utf8BufferA, utf8A.Length));
            Assert.IsTrue(0 < UTF8ArrayUnsafeUtility.StrCmp(utf8BufferB, utf8B.Length, utf16BufferA, utf16A.Length));
            Assert.IsTrue(0 < UTF8ArrayUnsafeUtility.StrCmp(utf16BufferB, utf16B.Length, utf16BufferA, utf16A.Length));
        }
    }
}

