using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Text;
using Unity.Burst;

// change this to change the core type under test
using FixedStringN = Unity.Collections.FixedString128Bytes;

namespace FixedStringTests
{
    [BurstCompile]
    internal class FixedStringBurstTests
    {
        [BurstCompile]
        static int BurstAppendFn(ref FixedStringN fs, in FixedString32Bytes other)
        {
            fs.Append(in other);
            return fs.Length;
        }

        delegate int BurstAppendDelegate(ref FixedStringN a, in FixedString32Bytes b);

        [Test]
        public void TestBurstAppend()
        {
            var fp = BurstCompiler.CompileFunctionPointer<BurstAppendDelegate>(BurstAppendFn);
            var invoke = fp.Invoke;

            FixedStringN a = new FixedStringN("Hello ");
            FixedString32Bytes b = new FixedString32Bytes("World");

            var len = invoke(ref a, b);
            Assert.AreEqual(11, len);
            Assert.AreEqual("Hello World", a.ToString());
        }
    }
}
