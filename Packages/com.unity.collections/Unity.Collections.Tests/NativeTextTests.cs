using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Text;
using Unity.Burst;
using Unity.Jobs;

namespace Unity.Collections.Tests
{
    internal class NativeTextTests
    {
        [Test]
        public void NativeTextFixedStringCtors()
        {
            using (NativeText aa = new NativeText(new FixedString32Bytes("test32"), Allocator.Temp))
            {
                Assert.True(aa != new FixedString32Bytes("test"));
                Assert.True(aa.Value == "test32");
                Assert.AreEqual("test32", aa);
            }

            using (NativeText aa = new NativeText(new FixedString64Bytes("test64"), Allocator.Temp))
            {
                Assert.True(aa != new FixedString64Bytes("test"));
                Assert.True(aa.Value == "test64");
                Assert.AreEqual("test64", aa);
            }

            using (NativeText aa = new NativeText(new FixedString128Bytes("test128"), Allocator.Temp))
            {
                Assert.True(aa != new FixedString128Bytes("test"));
                Assert.True(aa.Value == "test128");
                Assert.AreEqual("test128", aa);
            }

            using (NativeText aa = new NativeText(new FixedString512Bytes("test512"), Allocator.Temp))
            {
                Assert.True(aa != new FixedString512Bytes("test"));
                Assert.True(aa.Value == "test512");
                Assert.AreEqual("test512", aa);
            }

            using (NativeText aa = new NativeText(new FixedString4096Bytes("test4096"), Allocator.Temp))
            {
                Assert.True(aa != new FixedString4096Bytes("test"));
                Assert.True(aa.Value == "test4096");
                Assert.AreEqual("test4096", aa);
            }

            using (NativeText aa = new NativeText("testString", Allocator.Temp))
            {
                var s = "testString";
                Assert.AreEqual(aa, s);
                Assert.True(aa.Value == "testString");
                Assert.AreEqual("testString", aa);
            }
        }

        [Test]
        public void NativeTextCorrectLengthAfterClear()
        {
            NativeText aa = new NativeText(4, Allocator.Temp);
            Assert.True(aa.IsCreated);
            Assert.AreEqual(0, aa.Length, "Length after creation is not 0");
            aa.AssertNullTerminated();

            aa.Junk();

            aa.Clear();
            Assert.AreEqual(0, aa.Length, "Length after clear is not 0");
            aa.AssertNullTerminated();

            aa.Dispose();
        }

        [Test]
        public void NativeTextFormatExtension1Params()
        {
            NativeText aa = new NativeText(4, Allocator.Temp);
            Assert.True(aa.IsCreated);
            aa.Junk();
            FixedString32Bytes format = "{0}";
            FixedString32Bytes arg0 = "a";
            aa.AppendFormat(format, arg0);
            aa.Add(0x61);
            Assert.AreEqual("aa", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void NativeTextFormatExtension2Params()
        {
            NativeText aa = new NativeText(4, Allocator.Temp);
            aa.Junk();
            FixedString32Bytes format = "{0} {1}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            aa.AppendFormat(format, arg0, arg1);
            Assert.AreEqual("a b", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void NativeTextFormatExtension3Params()
        {
            NativeText aa = new NativeText(4, Allocator.Temp);
            aa.Junk();
            FixedString32Bytes format = "{0} {1} {2}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            aa.AppendFormat(format, arg0, arg1, arg2);
            Assert.AreEqual("a b c", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void NativeTextFormatExtension4Params()
        {
            NativeText aa = new NativeText(Allocator.Temp);
            aa.Junk();
            FixedString32Bytes format = "{0} {1} {2} {3}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            aa.AppendFormat(format, arg0, arg1, arg2, arg3);
            Assert.AreEqual("a b c d", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void NativeTextFormatExtension5Params()
        {
            NativeText aa = new NativeText(4, Allocator.Temp);
            aa.Junk();
            FixedString32Bytes format = "{0} {1} {2} {3} {4}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4);
            Assert.AreEqual("a b c d e", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void NativeTextFormatExtension6Params()
        {
            NativeText aa = new NativeText(4, Allocator.Temp);
            aa.Junk();
            FixedString32Bytes format = "{0} {1} {2} {3} {4} {5}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5);
            Assert.AreEqual("a b c d e f", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void NativeTextFormatExtension7Params()
        {
            NativeText aa = new NativeText(4, Allocator.Temp);
            aa.Junk();
            FixedString32Bytes format = "{0} {1} {2} {3} {4} {5} {6}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            FixedString32Bytes arg6 = "g";
            aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5, arg6);
            Assert.AreEqual("a b c d e f g", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void NativeTextFormatExtension8Params()
        {
            NativeText aa = new NativeText(4, Allocator.Temp);
            aa.Junk();
            FixedString128Bytes format = "{0} {1} {2} {3} {4} {5} {6} {7}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            FixedString32Bytes arg6 = "g";
            FixedString32Bytes arg7 = "h";
            aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            Assert.AreEqual("a b c d e f g h", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void NativeTextFormatExtension9Params()
        {
            NativeText aa = new NativeText(4, Allocator.Temp);
            aa.Junk();
            FixedString128Bytes format = "{0} {1} {2} {3} {4} {5} {6} {7} {8}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            FixedString32Bytes arg6 = "g";
            FixedString32Bytes arg7 = "h";
            FixedString32Bytes arg8 = "i";
            aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            Assert.AreEqual("a b c d e f g h i", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void NativeTextFormatExtension10Params()
        {
            NativeText aa = new NativeText(4, Allocator.Temp);
            aa.Junk();
            FixedString128Bytes format = "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            FixedString32Bytes arg6 = "g";
            FixedString32Bytes arg7 = "h";
            FixedString32Bytes arg8 = "i";
            FixedString32Bytes arg9 = "j";
            aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
            Assert.AreEqual("a b c d e f g h i j", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }

        [Test]
        public void NativeTextAppendGrows()
        {
            NativeText aa = new NativeText(1, Allocator.Temp);
            var origCapacity = aa.Capacity;
            for (int i = 0; i < origCapacity; ++i)
                aa.Append('a');
            Assert.AreEqual(origCapacity, aa.Capacity);
            aa.Append('b');
            Assert.GreaterOrEqual(aa.Capacity, origCapacity);
            Assert.AreEqual(new String('a', origCapacity) + "b", aa.ToString());
            aa.Dispose();
        }

        [Test]
        public void NativeTextAppendString()
        {
            NativeText aa = new NativeText(4, Allocator.Temp);
            aa.Append("aa");
            Assert.AreEqual("aa", aa.ToString());
            aa.Append("bb");
            Assert.AreEqual("aabb", aa.ToString());
            aa.Dispose();
        }


        [TestCase("Antidisestablishmentarianism")]
        [TestCase("⁣🌹🌻🌷🌿🌵🌾⁣")]
        public void NativeTextCopyFromBytesWorks(String a)
        {
            NativeText aa = new NativeText(4, Allocator.Temp);
            aa.Junk();
            var utf8 = Encoding.UTF8.GetBytes(a);
            unsafe
            {
                fixed (byte* b = utf8)
                    aa.Append(b, (ushort) utf8.Length);
            }

            Assert.AreEqual(a, aa.ToString());
            aa.AssertNullTerminated();

            aa.Append("tail");
            Assert.AreEqual(a + "tail", aa.ToString());
            aa.AssertNullTerminated();

            aa.Dispose();
        }

        [TestCase("red")]
        [TestCase("紅色", TestName = "{m}(Chinese-Red)")]
        [TestCase("George Washington")]
        [TestCase("村上春樹", TestName = "{m}(HarukiMurakami)")]
        public void NativeTextToStringWorks(String a)
        {
            NativeText aa = new NativeText(4, Allocator.Temp);
            aa.Append(new FixedString128Bytes(a));
            Assert.AreEqual(a, aa.ToString());
            aa.AssertNullTerminated();
            aa.Dispose();
        }

        [TestCase("monkey", "monkey")]
        [TestCase("yellow", "green")]
        [TestCase("violet", "紅色", TestName = "{m}(Violet-Chinese-Red")]
        [TestCase("绿色", "蓝色", TestName = "{m}(Chinese-Green-Blue")]
        [TestCase("靛蓝色", "紫罗兰色", TestName = "{m}(Chinese-Indigo-Violet")]
        [TestCase("James Monroe", "John Quincy Adams")]
        [TestCase("Andrew Jackson", "村上春樹", TestName = "{m}(AndrewJackson-HarukiMurakami")]
        [TestCase("三島 由紀夫", "吉本ばなな", TestName = "{m}(MishimaYukio-YoshimotoBanana")]
        public void NativeTextEqualsWorks(String a, String b)
        {
            using (var aa = new NativeText(new FixedString128Bytes(a), Allocator.Temp))
            using (var bb = new NativeText(new FixedString128Bytes(b), Allocator.Temp))
            {
                Assert.AreEqual(aa.Equals(bb), a.Equals(b));
                aa.AssertNullTerminated();
                bb.AssertNullTerminated();
            }
        }

        [Test]
        public void NativeTextForEach()
        {
            NativeText actual = new NativeText("A🌕Z🌑D𒁃 𒁃𒁃𒁃𒁃𒁃", Allocator.Temp);
            FixedList128Bytes<uint> expected = default;
            expected.Add('A');
            expected.Add(0x1F315);
            expected.Add('Z');
            expected.Add(0x1F311);
            expected.Add('D');
            expected.Add(0x12043);
            expected.Add(' ');
            expected.Add(0x12043);
            expected.Add(0x12043);
            expected.Add(0x12043);
            expected.Add(0x12043);
            expected.Add(0x12043);
            int index = 0;
            foreach (var rune in actual)
            {
                Assert.AreEqual(expected[index], rune.value);
                ++index;
            }

            actual.Dispose();
        }

        [Test]
        public void NativeTextNSubstring()
        {
            NativeText a = new NativeText("This is substring.", Allocator.Temp);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<ArgumentOutOfRangeException>(() => a.Substring(-8, 9));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.Substring(200, 9));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.Substring(8, -9));
#endif

            {
                NativeText b = a.Substring(8, 9);
                Assert.IsTrue(b.Equals("substring"));
            }

            {
                NativeText b = a.Substring(8, 100);
                Assert.IsTrue(b.Equals("substring."));
            }
        }

        [Test]
        public void NativeTextIndexOf()
        {
            NativeText a = new NativeText("bookkeeper bookkeeper", Allocator.Temp);
            NativeText b = new NativeText("ookkee", Allocator.Temp);
            Assert.AreEqual(1, a.IndexOf(b));
            Assert.AreEqual(-1, b.IndexOf(a));
            a.Dispose();
            b.Dispose();
        }

        [Test]
        public void NativeTextLastIndexOf()
        {
            NativeText a = new NativeText("bookkeeper bookkeeper", Allocator.Temp);
            NativeText b = new NativeText("ookkee", Allocator.Temp);
            Assert.AreEqual(12, a.LastIndexOf(b));
            Assert.AreEqual(-1, b.LastIndexOf(a));
            a.Dispose();
            b.Dispose();
        }

        [Test]
        public void NativeTextContains()
        {
            NativeText a = new NativeText("bookkeeper", Allocator.Temp);
            NativeText b = new NativeText("ookkee", Allocator.Temp);
            Assert.AreEqual(true, a.Contains(b));
            a.Dispose();
            b.Dispose();
        }

        [Test]
        public void NativeTextComparisons()
        {
            using (var a = new NativeText("apple", Allocator.Temp))
            using (var b = new NativeText("banana", Allocator.Temp))
            {
                Assert.AreEqual(false, a.Equals(b));
                Assert.AreEqual(true, !b.Equals(a));
            }
        }

        [Test]
        public void NativeTextCustomAllocatorTest()
        {
            AllocatorManager.Initialize();
            var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
            ref var allocator = ref allocatorHelper.Allocator;
            allocator.Initialize();

            using (var container = new NativeText(allocator.Handle))
            {
            }

            Assert.IsTrue(allocator.WasUsed);
            allocator.Dispose();
            allocatorHelper.Dispose();
            AllocatorManager.Shutdown();
        }

        [BurstCompile]
        struct BurstedCustomAllocatorJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public unsafe CustomAllocatorTests.CountingAllocator* Allocator;

            public void Execute()
            {
                unsafe
                {
                    using (var container = new NativeText(Allocator->Handle))
                    {
                    }
                }
            }
        }

        [Test]
        public unsafe void NativeTextBurstedCustomAllocatorTest()
        {
            AllocatorManager.Initialize();
            var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
            ref var allocator = ref allocatorHelper.Allocator;
            allocator.Initialize();

            var allocatorPtr = (CustomAllocatorTests.CountingAllocator*)UnsafeUtility.AddressOf<CustomAllocatorTests.CountingAllocator>(ref allocator);
            unsafe
            {
                var handle = new BurstedCustomAllocatorJob {Allocator = allocatorPtr}.Schedule();
                handle.Complete();
            }

            Assert.IsTrue(allocator.WasUsed);
            allocator.Dispose();
            allocatorHelper.Dispose();
            AllocatorManager.Shutdown();
        }

        [Test]
        public void NativeTextIsEmpty()
        {
            var a = new NativeText("", Allocator.Temp);
            Assert.IsTrue(a.IsEmpty);
            a.CopyFrom("hello");
            Assert.IsFalse(a.IsEmpty);
            a.Dispose();
        }

        [Test]
        public void NativeTextIsEmptyReturnsTrueForNotConstructed()
        {
            NativeText a = default;
            Assert.IsTrue(a.IsEmpty);
        }

        [Test]
        public void NativeTextReadonlyCtor()
        {
            using (NativeText aa = new NativeText(new FixedString32Bytes("test32"), Allocator.Temp))
            {
                var ro = aa.AsReadOnly();
                Assert.True(ro != new FixedString32Bytes("test"));
                Assert.True(ro.Value == "test32");
                Assert.AreEqual("test32", ro);
            }

            using (NativeText aa = new NativeText(new FixedString64Bytes("test64"), Allocator.Temp))
            {
                var ro = aa.AsReadOnly();
                Assert.True(ro != new FixedString64Bytes("test"));
                Assert.True(ro.Value == "test64");
                Assert.AreEqual("test64", ro);
            }

            using (NativeText aa = new NativeText(new FixedString128Bytes("test128"), Allocator.Temp))
            {
                var ro = aa.AsReadOnly();
                Assert.True(ro != new FixedString128Bytes("test"));
                Assert.True(ro.Value == "test128");
                Assert.AreEqual("test128", ro);
            }

            using (NativeText aa = new NativeText(new FixedString512Bytes("test512"), Allocator.Temp))
            {
                var ro = aa.AsReadOnly();
                Assert.True(ro != new FixedString512Bytes("test"));
                Assert.True(ro.Value == "test512");
                Assert.AreEqual("test512", ro);
            }

            using (NativeText aa = new NativeText(new FixedString4096Bytes("test4096"), Allocator.Temp))
            {
                var ro = aa.AsReadOnly();
                Assert.True(ro != new FixedString4096Bytes("test"));
                Assert.True(ro.Value == "test4096");
                Assert.AreEqual("test4096", ro);
            }

            using (var aa = new NativeText("Hello", Allocator.Temp))
            {
                var ro = aa.AsReadOnly();

                Assert.AreEqual(aa.Equals(ro), ro.Equals(aa));
                aa.AssertNullTerminated();
                ro.AssertNullTerminated();
            }
        }

        [TestCase("monkey", "monkey")]
        [TestCase("yellow", "green")]
        [TestCase("violet", "紅色", TestName = "{m}(Violet-Chinese-Red")]
        [TestCase("绿色", "蓝色", TestName = "{m}(Chinese-Green-Blue")]
        [TestCase("靛蓝色", "紫罗兰色", TestName = "{m}(Chinese-Indigo-Violet")]
        [TestCase("James Monroe", "John Quincy Adams")]
        [TestCase("Andrew Jackson", "村上春樹", TestName = "{m}(AndrewJackson-HarukiMurakami")]
        [TestCase("三島 由紀夫", "吉本ばなな", TestName = "{m}(MishimaYukio-YoshimotoBanana")]
        public void NativeTextReadOnlyEqualsWorks(String a, String b)
        {
            using (var aa = new NativeText(new FixedString128Bytes(a), Allocator.Temp))
            using (var bb = new NativeText(new FixedString128Bytes(b), Allocator.Temp))
            {
                var aaRO = aa.AsReadOnly();
                var bbRO = bb.AsReadOnly();
                Assert.AreEqual(aaRO.Equals(bbRO), a.Equals(b));
                aaRO.AssertNullTerminated();
                bbRO.AssertNullTerminated();
            }
        }

        [Test]
        public void NativeTextReadOnlyIndexOf()
        {
            using (var a = new NativeText("bookkeeper bookkeeper", Allocator.Temp))
            using (var b = new NativeText("ookkee", Allocator.Temp))
            {
                var aRO = a.AsReadOnly();
                var bRO = b.AsReadOnly();
                Assert.AreEqual(1, aRO.IndexOf(bRO));
                Assert.AreEqual(-1, bRO.IndexOf(aRO));
            }
        }

        [Test]
        public void NativeTextReadOnlyLastIndexOf()
        {
            using (var a = new NativeText("bookkeeper bookkeeper", Allocator.Temp))
            using (var b = new NativeText("ookkee", Allocator.Temp))
            {
                var aRO = a.AsReadOnly();
                var bRO = b.AsReadOnly();
                Assert.AreEqual(12, aRO.LastIndexOf(bRO));
                Assert.AreEqual(-1, bRO.LastIndexOf(aRO));
            }
        }

        [Test]
        public void NativeTextReadOnlyContains()
        {
            using (var a = new NativeText("bookkeeper", Allocator.Temp))
            using (var b = new NativeText("ookkee", Allocator.Temp))
            {
                var aRO = a.AsReadOnly();
                var bRO = b.AsReadOnly();
                Assert.AreEqual(true, aRO.Contains(bRO));
            }
        }

        [Test]
        public void NativeTextReadOnlyComparisons()
        {
            using (var a = new NativeText("apple", Allocator.Temp))
            using (var b = new NativeText("banana", Allocator.Temp))
            {
                var aRO = a.AsReadOnly();
                var bRO = b.AsReadOnly();
                Assert.AreEqual(false, aRO.Equals(bRO));
                Assert.AreEqual(true, !bRO.Equals(aRO));
            }
        }

        [Test]
        public void NativeTextReadOnlyMakeMoreThanOne()
        {
            using (var a = new NativeText("apple", Allocator.Temp))
            {
                var aRO1 = a.AsReadOnly();
                var aRO2 = a.AsReadOnly();
                Assert.IsTrue(a.Equals(aRO1));
                Assert.IsTrue(aRO1.Equals(aRO2));
                Assert.IsTrue(aRO2.Equals(aRO1));
            }
        }

        [Test]
        public void NativeTextReadOnlyIsNotACopy()
        {
            using (var a = new NativeText("apple", Allocator.Temp))
            {
                var aRO = a.AsReadOnly();
                Assert.AreEqual(true, aRO.Equals(a));

                unsafe
                {
                    Assert.IsTrue(aRO.GetUnsafePtr() == a.GetUnsafePtr());
                }
            }
        }


        [Test]
        public void NativeTextReadOnlyIsEmpty()
        {
            var a = new NativeText("", Allocator.Temp);
            var aRO = a.AsReadOnly();
            Assert.IsTrue(aRO.IsEmpty);
            a.Dispose();

            a = new NativeText("not empty", Allocator.Temp);
            aRO = a.AsReadOnly();
            Assert.IsFalse(aRO.IsEmpty);
            a.Dispose();
        }

        [Test]
        public void NativeTextReadOnlyIsEmptyReturnsTrueOrThrowsForNotConstructed()
        {
            NativeText.ReadOnly aRO = default;
            Assert.IsTrue(aRO.IsEmpty);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeText aa = default;
            NativeText.ReadOnly aaRO = aa.AsReadOnly();
            Assert.IsTrue(aaRO.IsEmpty);
#endif
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks]
        public void NativeTextReadOnlyIsNotWritable()
        {
            using (var a = new NativeText("apple", Allocator.Temp))
            {
                var aRO = a.AsReadOnly();
                Assert.AreEqual(true, aRO.Equals(a));

                Assert.Throws<NotSupportedException>(() => { aRO.IsEmpty = true; });
                Assert.Throws<NotSupportedException>(() => { aRO.Length = 0; });
                Assert.Throws<NotSupportedException>(() => { aRO.Capacity = 0; });
                Assert.Throws<NotSupportedException>(() => { aRO.ElementAt(0); });
                Assert.Throws<NotSupportedException>(() => { aRO.Clear(); });
                Assert.Throws<NotSupportedException>(() => { aRO.Append("won't work"); });
                FixedString32Bytes format = "{0}";
                FixedString32Bytes arg0 = "a";
                Assert.Throws<NotSupportedException>(() => { aRO.AppendFormat(format, arg0); });
                Assert.Throws<NotSupportedException>(() => { aRO.AppendRawByte(1); });
                Assert.Throws<NotSupportedException>(() => { aRO.TryResize(0); });
            }
        }

        [Test]
        public void NativeTextReadOnlyCannotBeUsedAfterSourceIsDisposed()
        {
            AllocatorManager.Initialize();
            using (var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent))
            {
                ref var allocator = ref allocatorHelper.Allocator;
                allocator.Initialize();

                var a = new NativeText("Keep it secret, ", allocator.Handle);
                var ro = a.AsReadOnly();
                a.Dispose(); // Invalidate the string we are referring to
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.Throws<ObjectDisposedException>(() => { UnityEngine.Debug.Log(ro.ToString()); });
#endif

                Assert.IsTrue(allocator.WasUsed);
                allocator.Dispose();
            }

            AllocatorManager.Shutdown();
        }

        [Test]
        public void NativeTextReadOnlyCannotBeUsedAfterSourceIsChanged()
        {
            AllocatorManager.Initialize();
            using (var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent))
            {
                ref var allocator = ref allocatorHelper.Allocator;
                allocator.Initialize();

                var a = new NativeText("Keep it secret, ", allocator.Handle);
                var ro = a.AsReadOnly();

                a.Clear(); // Change the string we are referring to

                Assert.DoesNotThrow(() => { UnityEngine.Debug.Log(ro.ToString()); });

                a.Dispose();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.Throws<ObjectDisposedException>(() => { UnityEngine.Debug.Log(ro.ToString()); });
#endif

                Assert.IsTrue(allocator.WasUsed);
                allocator.Dispose();
            }

            AllocatorManager.Shutdown();
        }


        [Test]
        [TestRequiresCollectionChecks]
        public void NativeTextReadOnlyModificationDuringEnumerationThrows()
        {
            AllocatorManager.Initialize();
            using (var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent))
            {
                ref var allocator = ref allocatorHelper.Allocator;
                allocator.Initialize();

                var a = new NativeText("Keep it secret, ", allocator.Handle);
                var ro = a.AsReadOnly();

                int iterations = 0;
                Assert.Throws<ObjectDisposedException>(() =>
                {
                    // Mutate the source string while iterating. Can append once but when we use the iterator
                    // again it will now be invalid and should throw
                    foreach (var c in ro)
                    {
                        iterations++;
                        a.Append("keep it safe, ");
                    }
                });
                Assert.AreEqual(1, iterations);

                a.Dispose();

                Assert.IsTrue(allocator.WasUsed);
                allocator.Dispose();
            }

            AllocatorManager.Shutdown();
        }

        struct TestWriteOfTextMappedToReadOnly : IJob
        {
            public NativeText Text;
            public void Execute() { }
        }

        struct TestClearTextMappedToReadOnly : IJob
        {
            public NativeText Text;
            public void Execute()
            {
                Text.Clear();
            }
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void NativeTextReadOnlyCannotScheduledSourceTextForWrite()
        {
            AllocatorManager.Initialize();
            using (var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent))
            {
                ref var allocator = ref allocatorHelper.Allocator;
                allocator.Initialize();

                var a = new NativeText("Keep it secret, ", allocator.Handle);
                var ro = a.AsReadOnly();
                var job = new TestWriteOfTextMappedToReadOnly
                {
                    Text = a
                };

                var handle = job.Schedule();

                Assert.Throws<InvalidOperationException>(() =>
                {
                    UnityEngine.Debug.Log(ro.ToString());
                });

                Assert.Throws<InvalidOperationException>(() =>
                {
                    a.Dispose();
                });
                handle.Complete();

                a.Dispose();

                Assert.IsTrue(allocator.WasUsed);
                allocator.Dispose();
            }

            AllocatorManager.Shutdown();
        }

        [Test]
        public void NativeTextReadOnlyCanReadFromSourceTextModifiedInJob()
        {
            AllocatorManager.Initialize();
            using (var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent))
            {
                ref var allocator = ref allocatorHelper.Allocator;
                allocator.Initialize();

                var a = new NativeText("Keep it secret, ", allocator.Handle);
                var ro = a.AsReadOnly();
                var job = new TestClearTextMappedToReadOnly
                {
                    Text = a
                };

                var handle = job.Schedule();
                handle.Complete();

                Assert.DoesNotThrow(() =>
                {
                    UnityEngine.Debug.Log(ro.ToString());
                });

                a.Dispose();

                Assert.IsTrue(allocator.WasUsed);
                allocator.Dispose();
            }

            AllocatorManager.Shutdown();
        }

        struct TestReadOfTextMappedToReadOnly : IJob
        {
            [ReadOnly]
            public NativeText Text;
            public void Execute() { }
        }

        [Test]
        public void NativeTextReadOnlyCanScheduledSourceTextForRead()
        {
            AllocatorManager.Initialize();
            using (var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent))
            {
                ref var allocator = ref allocatorHelper.Allocator;
                allocator.Initialize();

                var a = new NativeText("Keep it secret, ", allocator.Handle);
                var ro = a.AsReadOnly();
                var job = new TestReadOfTextMappedToReadOnly
                {
                    Text = a
                };

                Assert.DoesNotThrow(() =>
                {
                    job.Schedule().Complete();
                });

                a.Dispose();

                Assert.IsTrue(allocator.WasUsed);
                allocator.Dispose();
            }

            AllocatorManager.Shutdown();
        }

        struct TestReadFromReadOnly : IJob
        {
            [ReadOnly] public NativeText.ReadOnly RO;
            public void Execute()
            {
                UnityEngine.Debug.Log($"{RO.Length}");
            }
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void NativeTextReadOnlyThrowWhenUsingReadOnlyInJobAfterSourceHasBeenDisposed()
        {
            AllocatorManager.Initialize();
            using (var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent))
            {
                ref var allocator = ref allocatorHelper.Allocator;
                allocator.Initialize();

                var a = new NativeText("Keep it secret, ", allocator.Handle);
                var job = new TestReadFromReadOnly
                {
                    RO = a.AsReadOnly()
                };

                var handle = job.Schedule();

                Assert.Throws<InvalidOperationException>(() =>
                {
                    a.Dispose();
                });

                handle.Complete();
                a.Dispose();

                Assert.IsTrue(allocator.WasUsed);
                allocator.Dispose();
            }

            AllocatorManager.Shutdown();
        }
    }
}
