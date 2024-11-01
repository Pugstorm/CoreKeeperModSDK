using NUnit.Framework;
using Unity.Collections;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Serialization;
using Unity.Jobs;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst.Intrinsics;
using Unity.Mathematics;
using Assert = FastAssert;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Profiling;
using BinaryReader = Unity.Entities.Serialization.BinaryReader;
using BinaryWriter = Unity.Entities.Serialization.BinaryWriter;
using Random = Unity.Mathematics.Random;
using System.IO;

namespace Unity.Entities.Tests
{
    public unsafe struct TestBinaryReader : BinaryReader
    {
        NativeList<byte> content;
        int position;
        public TestBinaryReader(TestBinaryWriter writer)
        {
            position = 0;
            content = writer.content;
            writer.content = new NativeList<byte>();
        }

        public void Dispose()
        {
            content.Dispose();
        }

        public void ReadBytes(void* data, int bytes)
        {
            UnsafeUtility.MemCpy(data, (byte*)content.GetUnsafePtr() + position, bytes);
            position += bytes;
        }

        public long Position
        {
            get => position;
            set => position = (int)value;
        }
    }

    public unsafe class TestBinaryWriter : BinaryWriter
    {
        public NativeList<byte> content;

        public TestBinaryWriter(Allocator allocator)
        {
            content = new NativeList<byte>(allocator);
        }

        public void Dispose()
        {
            content.Dispose();
        }

        public void WriteBytes(void* data, int bytes)
        {
            int length = (int)Position;
            content.Resize(length + bytes, NativeArrayOptions.UninitializedMemory);
            UnsafeUtility.MemCpy((byte*)content.GetUnsafePtr() + length, data, bytes);
            Position += bytes;
        }

        public long Position { get; set; }
    }

    struct DeserializeJob : IJob
    {
        public ExclusiveEntityTransaction Transaction;
        public TestBinaryReader Reader { get; set; }

        public void Execute()
        {
            SerializeUtility.DeserializeWorld(Transaction, Reader);
        }
    }

    internal class YAMLSerializationHelpers
    {
        /// <summary>
        /// Compare two YAML file for equality, ignore CRLF mismatch
        /// </summary>
        /// <param name="fileA">Stream of the first file to compare</param>
        /// <param name="fileB">Stream of the second file to compare</param>
        /// <returns>true if both file are equal, false otherwise</returns>
        /// <remarks>
        /// This method start reading from both streams from their CURRENT position
        /// </remarks>
        public static bool EqualYAMLFiles(Stream fileA, Stream fileB)
        {
            using (var readerA = new StreamReader(fileA))
            using (var readerB = new StreamReader(fileB))
            {
                string lineA;
                while ((lineA = readerA.ReadLine()) != null)
                {
                    var lineB = readerB.ReadLine();
                    if (string.Compare(lineA, lineB, StringComparison.Ordinal) != 0)
                    {
                        return false;
                    }
                }
                return readerB.ReadLine() == null;
            }
        }
    }

#if UNITY_EDITOR
    public static class Hash128Helpers
    {
        private static uint _counter = 1;
        public static Hash128 New()
        {
            return new Hash128(123, 456, 789, _counter++);
        }
    }

    class DotsSerializationTest
    {
        public struct BlobTestA
        {
            public int A;
            public int B;
            public BlobArray<int> Values;

            public static BlobAssetReference<BlobTestA> Build(int a, int b, int v1, int v2, int v3)
            {
                var builder = new BlobBuilder(Allocator.Temp);
                ref var r = ref builder.ConstructRoot<BlobTestA>();
                r.A = a;
                r.B = b;
                var array = builder.Allocate(ref r.Values, 3);
                array[0] = v1;
                array[1] = v2;
                array[2] = v3;

                return builder.CreateBlobAssetReference<BlobTestA>(Allocator.Persistent);
            }

            public static BlobAssetReference<BlobTestA> Build(int a, int b, int v1, int v2, int v3, int v4, int v5)
            {
                var builder = new BlobBuilder(Allocator.Temp);
                ref var r = ref builder.ConstructRoot<BlobTestA>();
                r.A = a;
                r.B = b;
                var array = builder.Allocate(ref r.Values, 5);
                array[0] = v1;
                array[1] = v2;
                array[2] = v3;
                array[3] = v4;
                array[4] = v5;

                return builder.CreateBlobAssetReference<BlobTestA>(Allocator.Persistent);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NodeHeaderA : IComponentData
        {
            public DotsSerialization.NodeHeader Header;
            public int A;
            public int B;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NodeHeaderB : IComponentData
        {
            public DotsSerialization.NodeHeader Header;
            public float A;
            public float B;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NodeHeaderC : IComponentData
        {
            public DotsSerialization.NodeHeader Header;
            public uint4 A;
            public uint2 B;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NodeHeaderD : IComponentData
        {
            public DotsSerialization.NodeHeader Header;
            public uint2 A;
            public uint2 B;
        }

        ulong TypeHash<T>() => TypeManager.GetTypeInfo<T>().StableTypeHash;

        [Test]
        public void CreateWriteReadNodeStructureWithEnumeration()
        {
            var pfn = FileUtil.GetUniqueTempPathInProject();

            try
            {
                CreateSimpleFile(pfn);

                using (var reader = new StreamBinaryReader(pfn))
                using (var dsf = DotsSerialization.CreateReader(reader))
                {
                    Assert.AreEqual(dsf.RootNode.ChildrenCount, 2);

                    var curChild = default(DotsSerializationReader.NodeHandle);
                    while (dsf.RootNode.MoveToNextChild(ref curChild))
                    {
                        if (curChild.NodeTypeHash == TypeHash<NodeHeaderA>())
                        {
                            Assert.AreEqual(curChild.AsNodeHeader.Id, NodeId_A);
                            Assert.AreEqual(curChild.NodeDotNetType, typeof(NodeHeaderA));

                            ref var nodeA = ref curChild.As<NodeHeaderA>();
                            Assert.AreEqual(nodeA.A, 1234);
                            Assert.AreEqual(nodeA.B, 5678);
                            Assert.AreEqual(nodeA.Header.NodeTypeHash, TypeHash<NodeHeaderA>());
                            Assert.AreEqual(nodeA.Header.Size, UnsafeUtility.SizeOf<NodeHeaderA>());
                            Assert.AreEqual(nodeA.Header.ChildrenCount, 3);
                        } else if (curChild.NodeTypeHash == TypeHash<NodeHeaderB>()) {
                            Assert.AreEqual(curChild.AsNodeHeader.Id, NodeId_B);
                            Assert.AreEqual(curChild.NodeDotNetType, typeof(NodeHeaderB));

                            ref var nodeB = ref curChild.As<NodeHeaderB>();
                            Assert.AreEqual(nodeB.A, 1.1234f);
                            Assert.AreEqual(nodeB.B, 1.5678f);
                            Assert.AreEqual(nodeB.Header.NodeTypeHash, TypeHash<NodeHeaderB>());
                            Assert.AreEqual(nodeB.Header.Size, UnsafeUtility.SizeOf<NodeHeaderB>());
                            Assert.AreEqual(nodeB.Header.ChildrenCount, 2);

                            var counter = 0;
                            var nestedChild = default(DotsSerializationReader.NodeHandle);
                            while (curChild.MoveToNextChild( ref nestedChild))
                            {
                                Assert.AreEqual(nestedChild.NodeTypeHash, TypeHash<NodeHeaderA>());
                                Assert.AreEqual(nestedChild.NodeDotNetType, typeof(NodeHeaderA));

                                ref var childNode = ref nestedChild.As<NodeHeaderA>();

                                Assert.AreEqual(childNode.Header.NodeTypeHash,TypeHash<NodeHeaderA>());
                                Assert.AreEqual(childNode.Header.Size, UnsafeUtility.SizeOf<NodeHeaderA>());
                                if (counter == 0)
                                {
                                    Assert.AreEqual(childNode.Header.Id, NodeId_BA1);
                                    Assert.AreEqual(childNode.A, 1);
                                    Assert.AreEqual(childNode.B, 2);
                                    Assert.AreEqual(childNode.Header.ChildrenCount, 0);
                                }
                                else
                                {
                                    Assert.AreEqual(childNode.Header.Id, NodeId_BA2);
                                    Assert.AreEqual(childNode.A, 3);
                                    Assert.AreEqual(childNode.B, 4);
                                    Assert.AreEqual(childNode.Header.ChildrenCount, 1);
                                }

                                ++counter;
                            }
                        }
                    }
                }
            }
            finally
            {
                File.Delete(pfn);
            }
        }

        [Test]
        public void CreateWriteReadFromFind()
        {
            var pfn = FileUtil.GetUniqueTempPathInProject();

            try
            {
                CreateSimpleFile(pfn);

                using (var reader = new StreamBinaryReader(pfn))
                using (var dsf = DotsSerialization.CreateReader(reader))
                {
                    // Find Node A from type
                    var nha = dsf.RootNode.FindNode<NodeHeaderA>();
                    Assert.IsTrue(nha.IsValid);
                    Assert.AreEqual(nha.NodeDotNetType, typeof(NodeHeaderA));

                    ref var nodeA = ref nha.As<NodeHeaderA>();
                    Assert.AreEqual(nodeA.A, 1234);
                    Assert.AreEqual(nodeA.B, 5678);

                    // Find Node AC1
                    var nhc = nha.FindNode(NodeId_AC1);
                    Assert.IsTrue(nhc.IsValid);
                    ref var nodeC1 = ref nhc.As<NodeHeaderC>();
                    Assert.AreEqual(nodeC1.Header.Id, NodeId_AC1);

                    // Find Node AC2
                    nhc = nha.FindNode(NodeId_AC2);
                    Assert.IsTrue(nhc.IsValid);
                    ref var nodeC2 = ref nhc.As<NodeHeaderC>();
                    Assert.AreEqual(nodeC2.Header.Id, NodeId_AC2);

                    // Find Node AC3
                    nhc = nha.FindNode(NodeId_AC3);
                    Assert.IsTrue(nhc.IsValid);
                    ref var nodeC3 = ref nhc.As<NodeHeaderC>();
                    Assert.AreEqual(nodeC3.Header.Id, NodeId_AC3);

                    // Attempt to find BA2CD but can't because there's not instance inside of A
                    var nhd = nha.FindNode(NodeId_BA2CD);
                    Assert.IsFalse(nhd.IsValid);

                    // Attempt to find BA2CD but can't because of nested level too short
                    nhd = dsf.RootNode.FindNode(NodeId_BA2CD, 3);
                    Assert.IsFalse(nhd.IsValid);

                    nhd = dsf.RootNode.FindNode(NodeId_BA2CD, 4);
                    Assert.IsTrue(nhd.IsValid);
                }
            }
            finally
            {
                File.Delete(pfn);
            }
        }

        [Test]
        public void CreateWriteReadNodeStructureWithMetadata()
        {
            var pfn = FileUtil.GetUniqueTempPathInProject();

            var blobsToDispose = new NativeList<BlobAssetReference<BlobTestA>>(Allocator.Temp);
            try
            {
                using (var writer = new StreamBinaryWriter(pfn))
                using (var dsf1 = DotsSerialization.CreateWriter(writer, Hash128Helpers.New(), "pipo"))
                {
                    using (var a = dsf1.CreateNode<NodeHeaderA>(NodeId_A))
                    {
                        a.NodeHeader.A = 1234;
                        a.NodeHeader.B = 5678;

                        var blobRef1 = BlobTestA.Build(1, 2, 1, 2, 3);
                        blobsToDispose.Add(blobRef1);
                        a.SetMetadata(blobRef1);

                        var id = NodeId_AC1;
                        for (int i = 0; i < 3; i++)
                        {
                            id.Value.z = (uint)(i + 1);
                            using (var ba2c = dsf1.CreateNode<NodeHeaderC>(id))
                            {
                                ba2c.NodeHeader.A = new uint4((uint)(1+i*10), 2, 3, 4);
                                ba2c.NodeHeader.B = new uint2((uint)(2+i*10), 2);
                                var blobRef = BlobTestA.Build(i, 2, i, i, i);
                                blobsToDispose.Add(blobRef);
                                ba2c.SetMetadata(blobRef);
                            }
                        }
                    }
                }

                using (var reader = new StreamBinaryReader(pfn))
                using (var dsf = DotsSerialization.CreateReader(reader))
                {
                    // Find Node A from type
                    var nha = dsf.RootNode.FindNode<NodeHeaderA>();
                    {
                        Assert.IsTrue(nha.IsValid);
                        Assert.AreEqual(nha.NodeDotNetType, typeof(NodeHeaderA));

                        Assert.IsTrue(nha.HasMetadata);
                        ref var metadata = ref nha.GetMetadata<BlobTestA>().Value;
                        Assert.AreEqual(metadata.A, 1);
                        Assert.AreEqual(metadata.B, 2);
                        Assert.AreEqual(metadata.Values[0], 1);
                        Assert.AreEqual(metadata.Values[1], 2);
                        Assert.AreEqual(metadata.Values[2], 3);

                        ref var nodeA = ref nha.As<NodeHeaderA>();
                        Assert.AreEqual(nodeA.A, 1234);
                        Assert.AreEqual(nodeA.B, 5678);
                    }

                    // Find Node AC1
                    var id = NodeId_AC1;

                    for (int i = 0; i < 3; i++)
                    {
                        id.Value.z = (uint)(i + 1);

                        var nhc = nha.FindNode(id);
                        Assert.IsTrue(nhc.IsValid);
                        ref var nodeC1 = ref nhc.As<NodeHeaderC>();
                        Assert.AreEqual(nodeC1.Header.Id, id);

                        Assert.IsTrue(nha.HasMetadata);
                        ref var metadata = ref nhc.GetMetadata<BlobTestA>().Value;
                        Assert.AreEqual(metadata.A, i);
                        Assert.AreEqual(metadata.B, 2);
                        Assert.AreEqual(metadata.Values[0], i);
                        Assert.AreEqual(metadata.Values[1], i);
                        Assert.AreEqual(metadata.Values[2], i);
                    }
                }
            }
            finally
            {
                File.Delete(pfn);
                foreach (var blobRef in blobsToDispose)
                    blobRef.Dispose();
            }
        }

        [Test]
        public unsafe void CreateWriteReadNodeStructureWithData()
        {
            var pfn = FileUtil.GetUniqueTempPathInProject();

            const int nodeADataSize = 1024*1024;
            try
            {
                using (var writer = new StreamBinaryWriter(pfn))
                using (var dsf1 = DotsSerialization.CreateWriter(writer, Hash128Helpers.New(), "pipo"))
                {
                    using (var a = dsf1.CreateNode<NodeHeaderA>(NodeId_A))
                    {
                        a.NodeHeader.A = 1234;
                        a.NodeHeader.B = 5678;

                        var data = new NativeArray<int>(nodeADataSize, Allocator.Temp);
                        for (int i = 0; i < nodeADataSize; i++)
                        {
                            data[i] = i;
                        }
                        a.WriteData(data.GetUnsafePtr(), nodeADataSize * sizeof(int));

                        var id = NodeId_AC1;
                        for (int i = 0; i < 3; i++)
                        {
                            id.Value.z = (uint)(i + 1);
                            using (var ba2c = dsf1.CreateNode<NodeHeaderC>(id))
                            {
                                ba2c.NodeHeader.A = new uint4((uint)(1+i*10), 2, 3, 4);
                                ba2c.NodeHeader.B = new uint2((uint)(2+i*10), 2);

                                // Issue two writes of the same data
                                ba2c.WriteData(data.GetUnsafePtr(), 4*4);
                                ba2c.WriteData(data.GetUnsafePtr(), 4*4);
                            }
                        }

                        data.Dispose();
                    }
                }

                using (var reader = new StreamBinaryReader(pfn))
                using (var dsf = DotsSerialization.CreateReader(reader))
                {
                    // Find Node A from type
                    var nha = dsf.RootNode.FindNode<NodeHeaderA>();
                    {
                        Assert.IsTrue(nha.IsValid);
                        Assert.AreEqual(nha.NodeDotNetType, typeof(NodeHeaderA));

                        // Check data
                        var dataSize = nha.DataSize;
                        Assert.AreEqual(dataSize, nodeADataSize * sizeof(int));

                        var data = new NativeArray<int>((int)dataSize/4, Allocator.Temp);
                        nha.ReadData((byte*)data.GetUnsafePtr());

                        for (int i = 0; i < data.Length; i++)
                        {
                            Assert.AreEqual(data[i], i);
                        }

                        data.Dispose();

                        // Check node
                        ref var nodeA = ref nha.As<NodeHeaderA>();
                        Assert.AreEqual(nodeA.A, 1234);
                        Assert.AreEqual(nodeA.B, 5678);
                    }

                    // Find Node AC1
                    var id = NodeId_AC1;

                    for (int i = 0; i < 3; i++)
                    {
                        var nhc = nha.FindNode(id);
                        Assert.IsTrue(nhc.IsValid);
                        ref var nodeC1 = ref nhc.As<NodeHeaderC>();
                        Assert.AreEqual(nodeC1.Header.Id, id);

                        // Check data
                        var dataSize = nhc.DataSize;
                        Assert.AreEqual(dataSize, 8*4);

                        var data = new NativeArray<int>(8, Allocator.Temp);
                        nhc.ReadData((byte*)data.GetUnsafePtr());

                        for (int j = 0; j < 4; j++)
                        {
                            Assert.AreEqual(data[j], j);
                            Assert.AreEqual(data[j+4], j);
                        }

                        data.Dispose();

                        id.Value.z = (uint)(i + 1);
                    }
                }
            }
            finally
            {
                File.Delete(pfn);
            }
        }


        [Test]
        public unsafe void CreateWriteReadNodeStructureWithDataInterleaved()
        {
            var pfn = FileUtil.GetUniqueTempPathInProject();

            const int nodeADataSize = 1024*1024;
            try
            {
                using (var writer = new StreamBinaryWriter(pfn))
                using (var dsf1 = DotsSerialization.CreateWriter(writer, Hash128Helpers.New(), "pipo"))
                {
                    using (var a = dsf1.CreateNode<NodeHeaderA>(NodeId_A))
                    {
                        a.NodeHeader.A = 1234;
                        a.NodeHeader.B = 5678;

                        var data = new NativeArray<int>(nodeADataSize, Allocator.Temp);
                        for (int i = 0; i < nodeADataSize; i++)
                        {
                            data[i] = i;
                        }
                        a.WriteData(data.GetUnsafePtr(), nodeADataSize*4);

                        var id = NodeId_AC1;
                        for (int i = 0; i < 3; i++)
                        {
                            id.Value.z = (uint)(i + 1);
                            using (var ba2c = dsf1.CreateNode<NodeHeaderC>(id))
                            {
                                ba2c.NodeHeader.A = new uint4((uint)(1+i*10), 2, 3, 4);
                                ba2c.NodeHeader.B = new uint2((uint)(2+i*10), 2);

                                // Issue two writes of the same data
                                ba2c.WriteData(data.GetUnsafePtr(), 4*4);
                                ba2c.WriteData(data.GetUnsafePtr(), 4*4);
                            }
                        }

                        // Writing before and after should cause an exception
                        Assert.Throws<InvalidOperationException>(() => a.WriteData(data.GetUnsafePtr(), nodeADataSize * 4));

                        data.Dispose();
                    }
                }
            }
            finally
            {
                File.Delete(pfn);
            }
        }

        [Test]
        public void CreateFileWithDeferredNode()
        {
            var pfn = FileUtil.GetUniqueTempPathInProject();

            try
            {
                // |- NodeA
                //    |- NodeAC1 (Deferred)
                //    |- NodeAC2
                using (var writer = new StreamBinaryWriter(pfn))
                using (var dsf = DotsSerialization.CreateWriter(writer, Hash128Helpers.New(), "pipo"))
                {
                    using (var a = dsf.CreateNode<NodeHeaderA>(NodeId_A))
                    {
                        // Here the dispose is NOT used to pop-up from the hierarchy tree, but to actually write the String Table to RawData
                        // The StringTable is designed to be a leaf node.
                        using (var st = dsf.CreateStringTableNode(NodeId_AC1))
                        {
                            st.WriteString("Pipo");
                            st.WriteString("Bimbo");

                            a.NodeHeader.A = 1234;
                            a.NodeHeader.B = 5678;

                            // Will be children of A
                            using (var ba2c = dsf.CreateNode<NodeHeaderC>(NodeId_AC2))
                            {
                                ba2c.NodeHeader.A = new uint4(1, 2, 3, 4);
                                ba2c.NodeHeader.B = new uint2((uint)st.WriteString("ba2cst1"), (uint)st.WriteString("ba2cst2"));
                            }
                        }
                    }
                }

                using (var reader = new StreamBinaryReader(pfn))
                using (var dsf = DotsSerialization.CreateReader(reader))
                {
                    // Find Node AC1
                    var nhac1 = dsf.RootNode.FindNode(NodeId_AC1);
                    Assert.IsTrue(nhac1.IsValid);

                    // GetString can only be called during the lifetime of st, once the object is disposed the StringTable buffer will be freed
                    using (var st = dsf.OpenStringTableNode(nhac1))
                    {
                        var nhac2 = dsf.RootNode.FindNode(NodeId_AC2);
                        ref var nodeAC2 = ref nhac2.As<NodeHeaderC>();

                        var st1 = st.GetString((int)nodeAC2.B.x);
                        Assert.AreEqual(st1, "ba2cst1");

                        var st2 = st.GetString((int)nodeAC2.B.y);
                        Assert.AreEqual(st2, "ba2cst2");
                    }
                }
            }
            finally
            {
                File.Delete(pfn);
            }
        }

        private static readonly Hash128 NodeId_A     = new Hash128(0, 0, 0, 1);
        private static readonly Hash128 NodeId_AC1   = new Hash128(0, 0, 1, 1);
        private static readonly Hash128 NodeId_AC2   = new Hash128(0, 0, 2, 1);
        private static readonly Hash128 NodeId_AC3   = new Hash128(0, 0, 3, 1);
        private static readonly Hash128 NodeId_B     = new Hash128(0, 0, 0, 2);
        private static readonly Hash128 NodeId_BA1   = new Hash128(0, 0, 1, 2);
        private static readonly Hash128 NodeId_BA2   = new Hash128(0, 0, 2, 2);
        private static readonly Hash128 NodeId_BA2C  = new Hash128(0, 1, 2, 1);
        private static readonly Hash128 NodeId_BA2CD = new Hash128(1, 1, 2, 1);

        private static void CreateSimpleFile(string pfn)
        {
            // |- NodeA
            //    |- NodeAC1
            //    |- NodeAC2
            //    |- NodeAC3
            // |- NodeB
            //    |- NodeBA1
            //    |- NodeBA2
            //       |- NodeBA2C
            //         |- NodeBA2CD
            using (var writer = new StreamBinaryWriter(pfn))
            using (var dsf = DotsSerialization.CreateWriter(writer, Hash128Helpers.New(), "pipo"))
            {
                using (var a = dsf.CreateNode<NodeHeaderA>(NodeId_A))
                {
                    a.NodeHeader.A = 1234;
                    a.NodeHeader.B = 5678;

                    var id = NodeId_AC1;
                    for (int i = 0; i < 3; i++)
                    {
                        id.Value.z = (uint)(i + 1);
                        using (var ba2c = dsf.CreateNode<NodeHeaderC>(id))
                        {
                            ba2c.NodeHeader.A = new uint4((uint)(1+i*10), 2, 3, 4);
                            ba2c.NodeHeader.B = new uint2((uint)(2+i*10), 2);
                        }
                    }
                }

                using (var b = dsf.CreateNode<NodeHeaderB>(NodeId_B))
                {
                    b.NodeHeader.A = 1.1234f;
                    b.NodeHeader.B = 1.5678f;

                    using (var ba1 = dsf.CreateNode<NodeHeaderA>(NodeId_BA1))
                    {
                        ba1.NodeHeader.A = 1;
                        ba1.NodeHeader.B = 2;
                    }

                    using (var ba2 = dsf.CreateNode<NodeHeaderA>(NodeId_BA2))
                    {
                        ba2.NodeHeader.A = 3;
                        ba2.NodeHeader.B = 4;

                        using (var ba2c = dsf.CreateNode<NodeHeaderC>(NodeId_BA2C))
                        {
                            ba2c.NodeHeader.A = new uint4(1, 2, 3, 4);
                            ba2c.NodeHeader.B = new uint2(10, 20);

                            using (var ba2cd = dsf.CreateNode<NodeHeaderD>(NodeId_BA2CD))
                            {
                                ba2cd.NodeHeader.A = new uint2(1000, 2000);
                                ba2cd.NodeHeader.B = new uint2(2000, 4000);
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public unsafe void PagedAllocation_AllocationTest()
        {
            var pa = new PagedAllocation(Allocator.Persistent, 1024);
            try
            {
                // Check initial state
                Assert.IsFalse(pa.IsDisposed);
                if (pa.Allocator != Allocator.Persistent)
                    Assert.AreEqual((object)pa.Allocator, Allocator.Persistent);
                Assert.AreEqual(pa.CurrentGlobalOffset, 0);
                Assert.AreEqual(pa.PageSize, 1024);
                Assert.AreEqual(pa.Pages.Length, 1);

                // Reserve in first page
                var b = pa.Reserve(10);
                Assert.AreEqual((IntPtr)b, (IntPtr)pa.Pages[0].Buffer);
                Assert.AreEqual(10, pa.Pages[0].FreeOffset);
                Assert.AreEqual(10, pa.CurrentGlobalOffset);

                // Can't fit in first page, has to be in a second one
                b = pa.Reserve(1020);
                Assert.AreEqual(2, pa.Pages.Length);
                Assert.AreEqual((IntPtr)b, (IntPtr)pa.Pages[1].Buffer);
                Assert.AreEqual(10, pa.Pages[0].FreeOffset);
                Assert.AreEqual(1020, pa.Pages[1].FreeOffset);
                Assert.AreEqual(1030, pa.CurrentGlobalOffset);

                // Fit in second page
                b = pa.Reserve(4);
                Assert.AreEqual(2, pa.Pages.Length);
                Assert.AreEqual((IntPtr)b, (IntPtr)(pa.Pages[1].Buffer + 1020));
                Assert.AreEqual(10, pa.Pages[0].FreeOffset);
                Assert.AreEqual(1024, pa.Pages[1].FreeOffset);
                Assert.AreEqual(1034, pa.CurrentGlobalOffset);

                // Fit in third page
                b = pa.Reserve(20);
                Assert.AreEqual(3, pa.Pages.Length);
                Assert.AreEqual((IntPtr)b, (IntPtr)pa.Pages[2].Buffer);
                Assert.AreEqual(10, pa.Pages[0].FreeOffset);
                Assert.AreEqual(1024, pa.Pages[1].FreeOffset);
                Assert.AreEqual(20, pa.Pages[2].FreeOffset);
                Assert.AreEqual(1054, pa.CurrentGlobalOffset);

                // Dedicated fourth because reserved size is bigger than page's one
                b = pa.Reserve(2000);
                Assert.AreEqual(4, pa.Pages.Length);
                Assert.AreEqual((IntPtr)b, (IntPtr)pa.Pages[3].Buffer);
                Assert.AreEqual(10, pa.Pages[0].FreeOffset);
                Assert.AreEqual(1024, pa.Pages[1].FreeOffset);
                Assert.AreEqual(20, pa.Pages[2].FreeOffset);
                Assert.AreEqual(2000, pa.Pages[3].FreeOffset);
                Assert.AreEqual(3054, pa.CurrentGlobalOffset);

                // Fifth, normal alloc
                b = pa.Reserve(100);
                Assert.AreEqual(5, pa.Pages.Length);
                Assert.AreEqual((IntPtr)b, (IntPtr)pa.Pages[4].Buffer);
                Assert.AreEqual(10, pa.Pages[0].FreeOffset);
                Assert.AreEqual(1024, pa.Pages[1].FreeOffset);
                Assert.AreEqual(20, pa.Pages[2].FreeOffset);
                Assert.AreEqual(2000, pa.Pages[3].FreeOffset);
                Assert.AreEqual(100, pa.Pages[4].FreeOffset);
                Assert.AreEqual(3154, pa.CurrentGlobalOffset);

                // Fifth, normal alloc
                b = pa.Reserve(200);
                Assert.AreEqual(5, pa.Pages.Length);
                Assert.AreEqual((IntPtr)b, (IntPtr)(pa.Pages[4].Buffer + 100));
                Assert.AreEqual(10, pa.Pages[0].FreeOffset);
                Assert.AreEqual(1024, pa.Pages[1].FreeOffset);
                Assert.AreEqual(20, pa.Pages[2].FreeOffset);
                Assert.AreEqual(2000, pa.Pages[3].FreeOffset);
                Assert.AreEqual(300, pa.Pages[4].FreeOffset);
                Assert.AreEqual(3354, pa.CurrentGlobalOffset);
            }
            finally
            {
                // Dispose and check cleaned state
                pa.Dispose();
                Assert.AreEqual(pa.Pages.IsCreated, false);
                Assert.AreEqual(pa.Pages.Length, 0);
                Assert.AreEqual(pa.IsDisposed, true);
            }
        }
    }
#endif

    class SerializeTests : ECSTestsFixture
    {
        public struct TestComponentData1 : IComponentData
        {
            public int value;
            public Entity referencedEntity;
        }

        public struct TestComponentData2 : IComponentData
        {
            public int value;
            public Entity referencedEntity;
        }

        [InternalBufferCapacity(16)]
        public struct TestBufferElement : IBufferElementData
        {
            public Entity entity;
            public int value;
        }

        public struct EcsTestSharedCompBlobAssetRef : ISharedComponentData
        {
            public BlobAssetReference<int> value;
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        public class EcsTestManagedDataBlobAssetRef : IComponentData
        {
            public BlobAssetReference<int> value;
        }
#endif

        [Test]
        public void SerializeIntoExistingWorldThrows()
        {
            m_Manager.CreateEntity(typeof(EcsTestData));

            // disposed via reader
            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer);

            var reader = new TestBinaryReader(writer);

            Assert.Throws<ArgumentException>(() =>
                SerializeUtility.DeserializeWorld(m_Manager.BeginExclusiveEntityTransaction(), reader)
            );
            m_Manager.EndExclusiveEntityTransaction();
        }

#if UNITY_EDITOR
        struct SetupDeserializationTestData : IJobChunk
        {
            public ComponentTypeHandle<EcsTestData> EcsTestData;
            public ComponentTypeHandle<EcsTestData2> EcsTestData2;
            public ComponentTypeHandle<EcsTestData3> EcsTestData3;
            public ComponentTypeHandle<EcsTestData4> EcsTestData4;
            public ComponentTypeHandle<EcsTestData5> EcsTestData5;
            public void Execute(in ArchetypeChunk chunk, int chunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                int i = chunkIndex * Chunk.kMaximumEntitiesPerChunk;
                var testData = chunk.GetNativeArray(ref EcsTestData);
                var testData2 = chunk.GetNativeArray(ref EcsTestData2);
                var testData3 = chunk.GetNativeArray(ref EcsTestData3);
                var testData4 = chunk.GetNativeArray(ref EcsTestData4);
                var testData5 = chunk.GetNativeArray(ref EcsTestData5);
                int n = chunk.Count;
                for (int e = 0; e < n; e++, i++)
                {
                    testData[e] = new EcsTestData(i);
                    testData2[e] = new EcsTestData2(i);
                    testData3[e] = new EcsTestData3(i);
                    testData4[e] = new EcsTestData4(i);
                    testData5[e] = new EcsTestData5(i);
                }
            }
        }

        [Test]
        public void TestBigEntityCountDeserialization()
        {
            var _previousWorld = World.DefaultGameObjectInjectionWorld;

            var _bigEntitiesFilePFN = FileUtil.GetUniqueTempPathInProject();

            var entityCount = 4_000_000;
            var world = World.DefaultGameObjectInjectionWorld = new World("Big Entity Count World");
            var manager1 = world.EntityManager;

            var targetArchetype = manager1.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData4), typeof(EcsTestData5));
            manager1.CreateEntity(targetArchetype, entityCount, World.UpdateAllocator.ToAllocator);

            using (var query = manager1.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2),
                       typeof(EcsTestData3),
                       typeof(EcsTestData4), typeof(EcsTestData5)))
            {
                new SetupDeserializationTestData
                {
                    EcsTestData = manager1.GetComponentTypeHandle<EcsTestData>(false),
                    EcsTestData2 = manager1.GetComponentTypeHandle<EcsTestData2>(false),
                    EcsTestData3 = manager1.GetComponentTypeHandle<EcsTestData3>(false),
                    EcsTestData4 = manager1.GetComponentTypeHandle<EcsTestData4>(false),
                    EcsTestData5 = manager1.GetComponentTypeHandle<EcsTestData5>(false),
                }.ScheduleParallel(query, default).Complete();
            }

            long _bigEntitiesFileSize;
            using (var writer = new StreamBinaryWriter(_bigEntitiesFilePFN))
            {
                SerializeUtility.SerializeWorld(manager1, writer);
                _bigEntitiesFileSize = new FileInfo(_bigEntitiesFilePFN).Length;
            }

            world.Dispose();

            // NOTE: uncomment to enable profiling
            // Profiler.logFile = @"D:\LoadScene";
            // Profiler.enableAllocationCallstacks = false;
            // Profiler.enableBinaryLog = true;
            // Profiler.maxUsedMemory = 256 * 1024 * 1024;
            // Profiler.enabled = true;

            var sw = new Stopwatch();
            TimeSpan _bigEntitiesFileReadTime = default;
            for (int i = 0; i < 3; i++)
            {
                sw.Start();

                Profiler.BeginSample("Read From Disk");

                File.ReadAllBytes(_bigEntitiesFilePFN);

                Profiler.EndSample();

                sw.Stop();
                _bigEntitiesFileReadTime = sw.Elapsed;


                using (var reader = new StreamBinaryReader(_bigEntitiesFilePFN))
                using (var deserializedWorld = new World("Big Entity Count World Load"))
                {
                    var manager = deserializedWorld.EntityManager;

                    sw = new Stopwatch();
                    sw.Start();

                    Profiler.BeginSample("DOTS File Deserialization");

                    SerializeUtility.DeserializeWorld(manager.BeginExclusiveEntityTransaction(), reader);

                    Profiler.EndSample();

                    sw.Stop();
                    manager.EndExclusiveEntityTransaction();
                }
            }

            // Profiler.enableBinaryLog = false;
            // Profiler.enabled = false;
            // Profiler.logFile = "";

            var t = sw.Elapsed;

            var r = _bigEntitiesFileReadTime.TotalMilliseconds / t.TotalMilliseconds;
            var sizemb = (double)_bigEntitiesFileSize / (1024 * 1024);
            Debug.Log($"File Read Time {_bigEntitiesFileReadTime.TotalMilliseconds:N2}ms, EntityScene Load Time {t.TotalMilliseconds:N2}ms\r\nFile Size {sizemb:N2}MB, Ratio {r:N2}");

            File.Delete(_bigEntitiesFilePFN);

            World.DefaultGameObjectInjectionWorld = _previousWorld;
        }
#endif

        [Test]
        public unsafe void SerializeEntities()
        {
            var dummyEntity = CreateEntityWithDefaultData(0); //To ensure entity indices are offset
            var e1 = CreateEntityWithDefaultData(1);
            var e2 = CreateEntityWithDefaultData(2);
            var e3 = CreateEntityWithDefaultData(3);
            m_Manager.AddComponentData(e1, new TestComponentData1 { value = 10, referencedEntity = e2 });
            m_Manager.AddComponentData(e2, new TestComponentData2 { value = 20, referencedEntity = e1 });
            m_Manager.AddComponentData(e3, new TestComponentData1 { value = 30, referencedEntity = Entity.Null });
            m_Manager.AddComponentData(e3, new TestComponentData2 { value = 40, referencedEntity = Entity.Null });
            m_Manager.AddBuffer<EcsIntElement>(e1);
            m_Manager.RemoveComponent<EcsTestData2>(e3);
            m_Manager.AddBuffer<EcsIntElement>(e3);

            m_Manager.GetBuffer<EcsIntElement>(e1).CopyFrom(new EcsIntElement[] { 1, 2, 3 }); // no overflow
            m_Manager.GetBuffer<EcsIntElement>(e3).CopyFrom(new EcsIntElement[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }); // overflow into heap

            var e4 = m_Manager.CreateEntity();
            m_Manager.AddBuffer<EcsComplexEntityRefElement>(e4);
            var ebuf = m_Manager.GetBuffer<EcsComplexEntityRefElement>(e4);
            ebuf.Add(new EcsComplexEntityRefElement { Entity = e1, Dummy = 1 });
            ebuf.Add(new EcsComplexEntityRefElement { Entity = e2, Dummy = 2 });
            ebuf.Add(new EcsComplexEntityRefElement { Entity = e3, Dummy = 3 });

            m_Manager.DestroyEntity(dummyEntity);
            // disposed via reader
            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);

            SerializeUtility.SerializeWorld(m_Manager, writer);
            var reader = new TestBinaryReader(writer);

            var deserializedWorld = new World("SerializeEntities Test World 3");
            var entityManager = deserializedWorld.EntityManager;

            SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
            entityManager.EndExclusiveEntityTransaction();

            try
            {
                var allEntities = entityManager.GetAllEntities(Allocator.Temp);
                var count = allEntities.Length;
                allEntities.Dispose();

                Assert.AreEqual(4, count);

                var group1 = entityManager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2),
                    typeof(TestComponentData1));
                var group2 = entityManager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2),
                    typeof(TestComponentData2));
                var group3 = entityManager.CreateEntityQuery(typeof(EcsTestData),
                    typeof(TestComponentData1), typeof(TestComponentData2));
                var group4 = entityManager.CreateEntityQuery(typeof(EcsComplexEntityRefElement));

                Assert.AreEqual(1, group1.CalculateEntityCount());
                Assert.AreEqual(1, group2.CalculateEntityCount());
                Assert.AreEqual(1, group3.CalculateEntityCount());
                Assert.AreEqual(1, group4.CalculateEntityCount());

                var everythingGroup = entityManager.CreateEntityQuery(Array.Empty<ComponentType>());
                var chunks = everythingGroup.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(4, chunks.Length);
                everythingGroup.Dispose();

                var entityType = entityManager.GetEntityTypeHandle();
                Assert.AreEqual(1, chunks[0].GetNativeArray(entityType).Length);
                Assert.AreEqual(1, chunks[1].GetNativeArray(entityType).Length);
                Assert.AreEqual(1, chunks[2].GetNativeArray(entityType).Length);
                Assert.AreEqual(1, chunks[3].GetNativeArray(entityType).Length);

                var entities1 = group1.ToEntityArray(World.UpdateAllocator.ToAllocator);
                var entities2 = group2.ToEntityArray(World.UpdateAllocator.ToAllocator);
                var entities3 = group3.ToEntityArray(World.UpdateAllocator.ToAllocator);
                var entities4 = group4.ToEntityArray(World.UpdateAllocator.ToAllocator);

                var new_e1 = entities1[0];
                var new_e2 = entities2[0];
                var new_e3 = entities3[0];
                var new_e4 = entities4[0];

                Assert.AreEqual(1, entityManager.GetComponentData<EcsTestData>(new_e1).value);
                Assert.AreEqual(-1, entityManager.GetComponentData<EcsTestData2>(new_e1).value0);
                Assert.AreEqual(-1, entityManager.GetComponentData<EcsTestData2>(new_e1).value1);
                Assert.AreEqual(10, entityManager.GetComponentData<TestComponentData1>(new_e1).value);

                Assert.AreEqual(2, entityManager.GetComponentData<EcsTestData>(new_e2).value);
                Assert.AreEqual(-2, entityManager.GetComponentData<EcsTestData2>(new_e2).value0);
                Assert.AreEqual(-2, entityManager.GetComponentData<EcsTestData2>(new_e2).value1);
                Assert.AreEqual(20, entityManager.GetComponentData<TestComponentData2>(new_e2).value);

                Assert.AreEqual(3, entityManager.GetComponentData<EcsTestData>(new_e3).value);
                Assert.AreEqual(30, entityManager.GetComponentData<TestComponentData1>(new_e3).value);
                Assert.AreEqual(40, entityManager.GetComponentData<TestComponentData2>(new_e3).value);

                Assert.IsTrue(entityManager.Exists(entityManager.GetComponentData<TestComponentData1>(new_e1).referencedEntity));
                Assert.IsTrue(entityManager.Exists(entityManager.GetComponentData<TestComponentData2>(new_e2).referencedEntity));
                Assert.AreEqual(new_e2 , entityManager.GetComponentData<TestComponentData1>(new_e1).referencedEntity);
                Assert.AreEqual(new_e1 , entityManager.GetComponentData<TestComponentData2>(new_e2).referencedEntity);

                var buf1 = entityManager.GetBuffer<EcsIntElement>(new_e1);
                Assert.AreEqual(3, buf1.Length);
                Assert.AreNotEqual((ulong)m_Manager.GetBuffer<EcsIntElement>(e1).GetUnsafeReadOnlyPtr(), (ulong)buf1.GetUnsafeReadOnlyPtr());

                for (int i = 0; i < 3; ++i)
                {
                    Assert.AreEqual(i + 1, buf1[i].Value);
                }

                var buf3 = entityManager.GetBuffer<EcsIntElement>(new_e3);
                Assert.AreEqual(10, buf3.Length);
                Assert.AreNotEqual((ulong)m_Manager.GetBuffer<EcsIntElement>(e3).GetUnsafeReadOnlyPtr(), (ulong)buf3.GetUnsafeReadOnlyPtr());

                for (int i = 0; i < 10; ++i)
                {
                    Assert.AreEqual(i + 1, buf3[i].Value);
                }

                var buf4 = entityManager.GetBuffer<EcsComplexEntityRefElement>(new_e4);
                Assert.AreEqual(3, buf4.Length);

                Assert.AreEqual(1, buf4[0].Dummy);
                Assert.AreEqual(new_e1, buf4[0].Entity);

                Assert.AreEqual(2, buf4[1].Dummy);
                Assert.AreEqual(new_e2, buf4[1].Entity);

                Assert.AreEqual(3, buf4[2].Dummy);
                Assert.AreEqual(new_e3, buf4[2].Entity);

                entityManager.Debug.CheckInternalConsistency();
                m_Manager.Debug.CheckInternalConsistency();
            }
            finally
            {
                deserializedWorld.Dispose();
            }
        }

/*

// There is no simple way to deserialize with an unknown type, so this test needs to be manually executed, twice:
//  - Once to save a file, comment everything between the LOAD sections and uncomment the SAVE section.
//  - Once to load the file, reverse the commented load and save sections.
// It's not pretty, but

// [LOAD
        public struct TestUnknownComponent : IComponentData
        {
            public int Value;
        }
// LOAD]

        [Test]
        public void DeserializeWithUnknownComponentType()
        {
            const string tempFileName = "DesWithUnknownFile.tmp";
            var pfn = $"{Path.GetTempPath()}{tempFileName}";

// [LOAD
            try
            {
                using (var reader = new StreamBinaryReader(pfn))
                {
                    var deserializedWorld = new World("Test World");
                    var entityManager = deserializedWorld.EntityManager;

                    Assert.Throws<ArgumentException>(() =>
                    {
                        SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
                        entityManager.EndExclusiveEntityTransaction();
                    });
                }
            }
            finally
            {
                File.Delete(pfn);
            }
// LOAD]
// [SAVE
            var e1 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e1, new TestUnknownComponent { Value = 7 });

            using (var writer = new StreamBinaryWriter(pfn))
            {
                SerializeUtility.SerializeWorld(m_Manager, writer);
            }
// SAVE]
        }
*/

        //测试

        public struct 测试 : IComponentData
        {
            public int value;
        }

        [Test]
        public void SerializeEntitiesSupportsNonASCIIComponentTypeNames()
        {
            var e1 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e1, new 测试 { value = 7 });

            // disposed via reader
            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer);
            var reader = new TestBinaryReader(writer);

            var deserializedWorld = new World("SerializeEntitiesSupportsNonASCIIComponentTypeNames Test World");
            var entityManager = deserializedWorld.EntityManager;

            SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
            entityManager.EndExclusiveEntityTransaction();

            try
            {
                var allEntities = entityManager.GetAllEntities(Allocator.Temp);
                var count = allEntities.Length;
                allEntities.Dispose();

                Assert.AreEqual(1, count);

                var group1 = entityManager.CreateEntityQuery(typeof(测试));

                Assert.AreEqual(1, group1.CalculateEntityCount());

                var entities = group1.ToEntityArray(World.UpdateAllocator.ToAllocator);
                var new_e1 = entities[0];

                Assert.AreEqual(7, entityManager.GetComponentData<测试>(new_e1).value);
            }
            finally
            {
                deserializedWorld.Dispose();
            }
        }

        [Test]
        public unsafe void SerializeEntitiesRemapsEntitiesInBuffers()
        {
            var dummyEntity = CreateEntityWithDefaultData(0); //To ensure entity indices are offset

            var e1 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e1, new EcsTestData(1));
            var e2 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e2, new EcsTestData2(2));

            m_Manager.AddBuffer<TestBufferElement>(e1);
            var buffer1 = m_Manager.GetBuffer<TestBufferElement>(e1);
            for (int i = 0; i < 1024; ++i)
                buffer1.Add(new TestBufferElement {entity = e2, value = 2});

            m_Manager.AddBuffer<TestBufferElement>(e2);
            var buffer2 = m_Manager.GetBuffer<TestBufferElement>(e2);
            for (int i = 0; i < 8; ++i)
                buffer2.Add(new TestBufferElement {entity = e1, value = 1});

            m_Manager.DestroyEntity(dummyEntity);
            // disposed via reader
            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);

            SerializeUtility.SerializeWorld(m_Manager, writer);
            var reader = new TestBinaryReader(writer);

            var deserializedWorld = new World("SerializeEntities Test World 3");
            var entityManager = deserializedWorld.EntityManager;

            SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
            entityManager.EndExclusiveEntityTransaction();

            try
            {
                var group1 = entityManager.CreateEntityQuery(typeof(EcsTestData), typeof(TestBufferElement));
                var group2 = entityManager.CreateEntityQuery(typeof(EcsTestData2), typeof(TestBufferElement));

                Assert.AreEqual(1, group1.CalculateEntityCount());
                Assert.AreEqual(1, group2.CalculateEntityCount());

                var entities1 = group1.ToEntityArray(World.UpdateAllocator.ToAllocator);
                var entities2 = group2.ToEntityArray(World.UpdateAllocator.ToAllocator);

                var new_e1 = entities1[0];
                var new_e2 = entities2[0];

                var newBuffer1 = entityManager.GetBuffer<TestBufferElement>(new_e1);
                Assert.AreEqual(1024, newBuffer1.Length);
                for (int i = 0; i < 1024; ++i)
                {
                    Assert.AreEqual(new_e2, newBuffer1[i].entity);
                    Assert.AreEqual(2, newBuffer1[i].value);
                }

                var newBuffer2 = entityManager.GetBuffer<TestBufferElement>(new_e2);
                Assert.AreEqual(8, newBuffer2.Length);
                for (int i = 0; i < 8; ++i)
                {
                    Assert.AreEqual(new_e1, newBuffer2[i].entity);
                    Assert.AreEqual(1, newBuffer2[i].value);
                }
            }
            finally
            {
                deserializedWorld.Dispose();
            }
        }

        [Test]
        public unsafe void SerializeEntitiesWorksWithChunkComponents()
        {
            var dummyEntity = CreateEntityWithDefaultData(0); //To ensure entity indices are offset

            var e1 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e1, new EcsTestData(1));
            m_Manager.AddChunkComponentData<EcsTestData3>(e1);
            m_Manager.SetChunkComponentData(m_Manager.GetChunk(e1), new EcsTestData3(42));
            var e2 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e2, new EcsTestData2(2));
            m_Manager.AddChunkComponentData<EcsTestData3>(e2);
            m_Manager.SetChunkComponentData(m_Manager.GetChunk(e2), new EcsTestData3(57));

            m_Manager.DestroyEntity(dummyEntity);
            // disposed via reader
            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);

            SerializeUtility.SerializeWorld(m_Manager, writer);
            var reader = new TestBinaryReader(writer);

            var deserializedWorld = new World("SerializeEntities Test World 3");
            var entityManager = deserializedWorld.EntityManager;

            SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
            entityManager.EndExclusiveEntityTransaction();

            try
            {
                var group1 = entityManager.CreateEntityQuery(typeof(EcsTestData));
                var group2 = entityManager.CreateEntityQuery(typeof(EcsTestData2));

                Assert.AreEqual(1, group1.CalculateEntityCount());
                Assert.AreEqual(1, group2.CalculateEntityCount());

                var entities1 = group1.ToEntityArray(World.UpdateAllocator.ToAllocator);
                var entities2 = group2.ToEntityArray(World.UpdateAllocator.ToAllocator);

                var new_e1 = entities1[0];
                var new_e2 = entities2[0];

                Assert.AreEqual(1, entityManager.GetComponentData<EcsTestData>(new_e1).value);
                Assert.AreEqual(42, entityManager.GetChunkComponentData<EcsTestData3>(new_e1).value0);

                Assert.AreEqual(2, entityManager.GetComponentData<EcsTestData2>(new_e2).value0);
                Assert.AreEqual(57, entityManager.GetChunkComponentData<EcsTestData3>(new_e2).value0);
            }
            finally
            {
                deserializedWorld.Dispose();
            }
        }

        [Test]
        public void SerializeDoesntRemapOriginalHeapBuffers()
        {
            var dummyEntity = CreateEntityWithDefaultData(0); //To ensure entity indices are offset

            var e1 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e1, new EcsTestData(1));
            var e2 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e2, new EcsTestData2(2));

            m_Manager.AddBuffer<TestBufferElement>(e1);
            var buffer1 = m_Manager.GetBuffer<TestBufferElement>(e1);
            for (int i = 0; i < 1024; ++i)
                buffer1.Add(new TestBufferElement {entity = e2, value = 2});

            m_Manager.AddBuffer<TestBufferElement>(e2);
            var buffer2 = m_Manager.GetBuffer<TestBufferElement>(e2);
            for (int i = 0; i < 8; ++i)
                buffer2.Add(new TestBufferElement {entity = e1, value = 1});

            m_Manager.DestroyEntity(dummyEntity);
            // disposed via reader
            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);

            SerializeUtility.SerializeWorld(m_Manager, writer);

            buffer1 = m_Manager.GetBuffer<TestBufferElement>(e1);
            for (int i = 0; i < buffer1.Length; ++i)
            {
                Assert.AreEqual(e2, buffer1[i].entity);
                Assert.AreEqual(2, buffer1[i].value);
            }

            buffer2 = m_Manager.GetBuffer<TestBufferElement>(e2);
            for (int i = 0; i < buffer2.Length; ++i)
            {
                Assert.AreEqual(e1, buffer2[i].entity);
                Assert.AreEqual(1, buffer2[i].value);
            }
        }

        struct ExternalSharedComponentValue
        {
            public object obj;
            public int hashcode;
            public TypeIndex typeIndex;
        }

        unsafe ExternalSharedComponentValue[] ExtractSharedComponentValues(int[] indices, EntityManager manager)
        {
            var values = new ExternalSharedComponentValue[indices.Length];
            ManagedComponentStore mcs = manager.GetCheckedEntityDataAccess()->ManagedComponentStore;
            for (int i = 0; i < indices.Length; ++i)
            {
                object value = mcs.GetSharedComponentDataNonDefaultBoxed(indices[i]);
                var typeIndex = TypeManager.GetTypeIndex(value.GetType());
                int hash = TypeManager.GetHashCode(value, typeIndex);
                values[i] = new ExternalSharedComponentValue {obj = value, hashcode = hash, typeIndex = typeIndex};
            }
            return values;
        }

        unsafe void InsertSharedComponentValues(ExternalSharedComponentValue[] values, EntityManager manager)
        {
            ManagedComponentStore mcs = manager.GetCheckedEntityDataAccess()->ManagedComponentStore;
            for (int i = 0; i < values.Length; ++i)
            {
                ExternalSharedComponentValue value = values[i];
                int index = mcs.InsertSharedComponentAssumeNonDefault(value.typeIndex,
                    value.hashcode, value.obj);
                Assert.AreEqual(i + 1, index);
            }
        }

        [Ignore("This test is unstable and will leak occasionally on CI causing PRs to fail. Disabled until fixed - DOTS-4206")]
        [Test]
        public unsafe void SerializeEntitiesWorksWithBlobAssetReferences()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestSharedComp2), typeof(EcsTestData2));
            var dummyEntity = CreateEntityWithDefaultData(0); //To ensure entity indices are offset

            var builder = new BlobBuilder(Allocator.Temp);
            ref var blobArray = ref builder.ConstructRoot<BlobArray<float>>();
            var array = builder.Allocate(ref blobArray, 5);
            array[0] = 1.7f;
            array[1] = 2.6f;
            array[2] = 3.5f;
            array[3] = 4.4f;
            array[4] = 5.3f;

            var arrayComponent = new EcsTestDataBlobAssetArray { array = builder.CreateBlobAssetReference<BlobArray<float>>(Allocator.Temp) };
            builder.Dispose();

            const int entityCount = 1000;
            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);

            m_Manager.CreateEntity(archetype1, entities);
            for (int i = 0; i < entityCount; ++i)
            {
                m_Manager.AddComponentData(entities[i], arrayComponent);
                m_Manager.SetSharedComponentManaged(entities[i], new EcsTestSharedComp(i % 4));
            }

            var intComponents = new NativeArray<EcsTestDataBlobAssetRef>(entityCount / 5, Allocator.Temp);
            for (int i = 0; i < intComponents.Length; ++i)
                intComponents[i] = new EcsTestDataBlobAssetRef { value = BlobAssetReference<int>.Create(i) };


            m_Manager.CreateEntity(archetype2, entities);
            for (int i = 0; i < entityCount; ++i)
            {
                var intComponent = intComponents[i % intComponents.Length];
                m_Manager.AddComponentData(entities[i], intComponent);
                m_Manager.SetComponentData(entities[i], new EcsTestData2(intComponent.value.Value));
                m_Manager.SetSharedComponentManaged(entities[i], new EcsTestSharedComp2(i % 3));

                m_Manager.AddBuffer<EcsTestDataBlobAssetElement>(entities[i]);
                var buffer = m_Manager.GetBuffer<EcsTestDataBlobAssetElement>(entities[i]);
                int numBlobs = i % 100;
                buffer.EnsureCapacity(numBlobs);
                for (int j = 0; j < numBlobs; ++j)
                {
                    buffer.Add(new EcsTestDataBlobAssetElement() { blobElement = BlobAssetReference<int>.Create(j) });
                }

                m_Manager.AddBuffer<EcsTestDataBlobAssetElement2>(entities[i]);
                var buffer2 = m_Manager.GetBuffer<EcsTestDataBlobAssetElement2>(entities[i]);
                buffer2.EnsureCapacity(numBlobs);
                for (int j = 0; j < numBlobs; ++j)
                {
                    buffer2.Add(new EcsTestDataBlobAssetElement2()
                    {
                        blobElement = BlobAssetReference<int>.Create(j + 1),
                        blobElement2 = BlobAssetReference<int>.Create(j + 2)
                    });
                }
            }

            m_Manager.DestroyEntity(dummyEntity);

            // disposed via reader
            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);

            SerializeUtility.SerializeWorld(m_Manager, writer);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            arrayComponent.array.Dispose();
            for (int i = 0; i < intComponents.Length; ++i)
                intComponents[i].value.Dispose();

            var reader = new TestBinaryReader(writer);

            var deserializedWorld = new World("SerializeEntities Test World 3");
            var entityManager = deserializedWorld.EntityManager;

            var job = new DeserializeJob {Transaction = entityManager.BeginExclusiveEntityTransaction(), Reader = reader};
            job.Schedule().Complete();
            entityManager.EndExclusiveEntityTransaction();

            entityManager.Debug.CheckInternalConsistency();

            var access = entityManager.GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            var mcs = access->ManagedComponentStore;

            Assert.IsTrue(access->AllSharedComponentReferencesAreFromChunks(ecs));

            try
            {
                var group1 = entityManager.CreateEntityQuery(typeof(EcsTestDataBlobAssetArray));
                var group2 = entityManager.CreateEntityQuery(typeof(EcsTestDataBlobAssetRef));
                var group3 = entityManager.CreateEntityQuery(typeof(EcsTestDataBlobAssetElement));
                var group4 = entityManager.CreateEntityQuery(typeof(EcsTestDataBlobAssetElement2));

                var entities1 = group1.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(entityCount, entities1.Length);
                var new_e1 = entities1[0];
                arrayComponent = entityManager.GetComponentData<EcsTestDataBlobAssetArray>(new_e1);
                var a = arrayComponent.array;
                Assert.AreEqual(1.7f, a.Value[0]);
                Assert.AreEqual(2.6f, a.Value[1]);
                Assert.AreEqual(3.5f, a.Value[2]);
                Assert.AreEqual(4.4f, a.Value[3]);
                Assert.AreEqual(5.3f, a.Value[4]);

                var entities2 = group2.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(entityCount, entities2.Length);
                for (int i = 0; i < entityCount; ++i)
                {
                    var val = entityManager.GetComponentData<EcsTestData2>(entities2[i]).value0;
                    Assert.AreEqual(val, entityManager.GetComponentData<EcsTestDataBlobAssetRef>(entities2[i]).value.Value);
                }

                var entities3 = group3.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(entityCount, entities3.Length);

                // We can't rely on the entity order matching how we filled the buffers so we instead ensure
                // that we see buffers as many buffers as there are entities and we see buffers with 1 to 'entityCount'
                // elements in them with ascending values from 0 to bufferLength-1
                NativeParallelHashMap<int, int> bufferMap = new NativeParallelHashMap<int, int>(entityCount, Allocator.Temp);
                for (int i = 0; i < entityCount; ++i)
                {
                    var buffer = entityManager.GetBuffer<EcsTestDataBlobAssetElement>(entities3[i]);

                    for (int j = 0; j < buffer.Length; ++j)
                    {
                        Assert.AreEqual(j, buffer[j].blobElement.Value);
                    }
                    if (!bufferMap.TryGetValue(buffer.Length, out var count))
                    {
                        bufferMap.TryAdd(buffer.Length, 1);
                    }
                    else
                    {
                        bufferMap[buffer.Length] = count + 1;
                    }
                }
                for (int i = 0; i < entityCount; ++i)
                {
                    Assert.IsTrue(bufferMap[i % 100] == 10);
                }
                bufferMap.Dispose();


                var entities4 = group4.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(entityCount, entities4.Length);

                NativeParallelHashMap<int, int> bufferMap2 = new NativeParallelHashMap<int, int>(entityCount, Allocator.Temp);
                for (int i = 0; i < entityCount; ++i)
                {
                    var buffer = entityManager.GetBuffer<EcsTestDataBlobAssetElement2>(entities4[i]);

                    for (int j = 0; j < buffer.Length; ++j)
                    {
                        Assert.AreEqual(j + 1, buffer[j].blobElement.Value);
                        Assert.AreEqual(j + 2, buffer[j].blobElement2.Value);
                    }
                    if (!bufferMap2.TryGetValue(buffer.Length, out var count))
                    {
                        bufferMap2.TryAdd(buffer.Length, 1);
                    }
                    else
                    {
                        bufferMap2[buffer.Length] = count + 1;
                    }
                }
                for (int i = 0; i < entityCount; ++i)
                {
                    Assert.IsTrue(bufferMap2[i % 100] == 10);
                }
                bufferMap2.Dispose();
            }
            finally
            {
                deserializedWorld.Dispose();
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            float f = 1.0f;
            Assert.Throws<InvalidOperationException>(() => f = arrayComponent.array.Value[0]);
#endif
        }

        // A test for DOTS Runtime to validate blob assets.
        [Ignore("This test is unstable and will leak occasionally on CI causing PRs to fail. Disabled until fixed - DOTS-4206")]
        [Test]
        public unsafe void SerializeEntitiesWorksWithBlobAssetReferencesNoSharedComponents()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData2));
            var dummyEntity = CreateEntityWithDefaultData(0); //To ensure entity indices are offset

            var builder = new BlobBuilder(Allocator.Temp);
            ref var blobArray = ref builder.ConstructRoot<BlobArray<float>>();
            var array = builder.Allocate(ref blobArray, 5);
            array[0] = 1.7f;
            array[1] = 2.6f;
            array[2] = 3.5f;
            array[3] = 4.4f;
            array[4] = 5.3f;

            var arrayComponent = new EcsTestDataBlobAssetArray { array = builder.CreateBlobAssetReference<BlobArray<float>>(Allocator.Temp) };
            builder.Dispose();

            const int entityCount = 1000;
            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);

            m_Manager.CreateEntity(archetype1, entities);
            for (int i = 0; i < entityCount; ++i)
            {
                m_Manager.AddComponentData(entities[i], arrayComponent);
            }

            var intComponents = new NativeArray<EcsTestDataBlobAssetRef>(entityCount / 5, Allocator.Temp);
            for (int i = 0; i < intComponents.Length; ++i)
                intComponents[i] = new EcsTestDataBlobAssetRef { value = BlobAssetReference<int>.Create(i) };


            m_Manager.CreateEntity(archetype2, entities);
            for (int i = 0; i < entityCount; ++i)
            {
                var intComponent = intComponents[i % intComponents.Length];
                m_Manager.AddComponentData(entities[i], intComponent);
                m_Manager.SetComponentData(entities[i], new EcsTestData2(intComponent.value.Value));

                m_Manager.AddBuffer<EcsTestDataBlobAssetElement>(entities[i]);
                var buffer = m_Manager.GetBuffer<EcsTestDataBlobAssetElement>(entities[i]);
                int numBlobs = i % 100;
                buffer.EnsureCapacity(numBlobs);
                for (int j = 0; j < numBlobs; ++j)
                {
                    buffer.Add(new EcsTestDataBlobAssetElement() { blobElement = BlobAssetReference<int>.Create(j) });
                }

                m_Manager.AddBuffer<EcsTestDataBlobAssetElement2>(entities[i]);
                var buffer2 = m_Manager.GetBuffer<EcsTestDataBlobAssetElement2>(entities[i]);
                buffer2.EnsureCapacity(numBlobs);
                for (int j = 0; j < numBlobs; ++j)
                {
                    buffer2.Add(new EcsTestDataBlobAssetElement2()
                    {
                        blobElement = BlobAssetReference<int>.Create(j + 1),
                        blobElement2 = BlobAssetReference<int>.Create(j + 2)
                    });
                }
            }

            m_Manager.DestroyEntity(dummyEntity);

            // disposed via reader
            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);

            SerializeUtility.SerializeWorld(m_Manager, writer);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            arrayComponent.array.Dispose();
            for (int i = 0; i < intComponents.Length; ++i)
                intComponents[i].value.Dispose();

            var reader = new TestBinaryReader(writer);

            var deserializedWorld = new World("SerializeEntities Test World 3");
            var entityManager = deserializedWorld.EntityManager;

            var job = new DeserializeJob { Transaction = entityManager.BeginExclusiveEntityTransaction(), Reader = reader };
            job.Schedule().Complete();
            entityManager.EndExclusiveEntityTransaction();

            entityManager.Debug.CheckInternalConsistency();

            var access = entityManager.GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            var mcs = access->ManagedComponentStore;

            Assert.IsTrue(access->AllSharedComponentReferencesAreFromChunks(ecs));

            try
            {
                var group1 = entityManager.CreateEntityQuery(typeof(EcsTestDataBlobAssetArray));
                var group2 = entityManager.CreateEntityQuery(typeof(EcsTestDataBlobAssetRef));
                var group3 = entityManager.CreateEntityQuery(typeof(EcsTestDataBlobAssetElement));
                var group4 = entityManager.CreateEntityQuery(typeof(EcsTestDataBlobAssetElement2));

                var entities1 = group1.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(entityCount, entities1.Length);
                var new_e1 = entities1[0];
                arrayComponent = entityManager.GetComponentData<EcsTestDataBlobAssetArray>(new_e1);
                var a = arrayComponent.array;
                Assert.AreEqual(1.7f, a.Value[0]);
                Assert.AreEqual(2.6f, a.Value[1]);
                Assert.AreEqual(3.5f, a.Value[2]);
                Assert.AreEqual(4.4f, a.Value[3]);
                Assert.AreEqual(5.3f, a.Value[4]);

                var entities2 = group2.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(entityCount, entities2.Length);
                for (int i = 0; i < entityCount; ++i)
                {
                    var val = entityManager.GetComponentData<EcsTestData2>(entities2[i]).value0;
                    Assert.AreEqual(val, entityManager.GetComponentData<EcsTestDataBlobAssetRef>(entities2[i]).value.Value);
                }

                var entities3 = group3.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(entityCount, entities3.Length);

                // We can't rely on the entity order matching how we filled the buffers so we instead ensure
                // that we see buffers as many buffers as there are entities and we see buffers with 1 to 'entityCount'
                // elements in them with ascending values from 0 to bufferLength-1
                NativeParallelHashMap<int, int> bufferMap = new NativeParallelHashMap<int, int>(entityCount, Allocator.Temp);
                for (int i = 0; i < entityCount; ++i)
                {
                    var buffer = entityManager.GetBuffer<EcsTestDataBlobAssetElement>(entities3[i]);

                    for (int j = 0; j < buffer.Length; ++j)
                    {
                        Assert.AreEqual(j, buffer[j].blobElement.Value);
                    }
                    if (!bufferMap.TryGetValue(buffer.Length, out var count))
                    {
                        bufferMap.TryAdd(buffer.Length, 1);
                    }
                    else
                    {
                        bufferMap[buffer.Length] = count + 1;
                    }
                }
                for (int i = 0; i < entityCount; ++i)
                {
                    Assert.IsTrue(bufferMap[i % 100] == 10);
                }
                bufferMap.Dispose();


                var entities4 = group4.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(entityCount, entities4.Length);

                NativeParallelHashMap<int, int> bufferMap2 = new NativeParallelHashMap<int, int>(entityCount, Allocator.Temp);
                for (int i = 0; i < entityCount; ++i)
                {
                    var buffer = entityManager.GetBuffer<EcsTestDataBlobAssetElement2>(entities4[i]);

                    for (int j = 0; j < buffer.Length; ++j)
                    {
                        Assert.AreEqual(j + 1, buffer[j].blobElement.Value);
                        Assert.AreEqual(j + 2, buffer[j].blobElement2.Value);
                    }
                    if (!bufferMap2.TryGetValue(buffer.Length, out var count))
                    {
                        bufferMap2.TryAdd(buffer.Length, 1);
                    }
                    else
                    {
                        bufferMap2[buffer.Length] = count + 1;
                    }
                }
                for (int i = 0; i < entityCount; ++i)
                {
                    Assert.IsTrue(bufferMap2[i % 100] == 10);
                }
                bufferMap2.Dispose();
            }
            finally
            {
                deserializedWorld.Dispose();
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            float f = 1.0f;
            Assert.Throws<InvalidOperationException>(() => f = arrayComponent.array.Value[0]);
#endif
        }

#pragma warning disable 169
        public unsafe struct ComponentWithPointer : IComponentData
        {
            int m_Pad;
            byte* m_Data;
        }

        [Test]
        public void SerializeComponentWithPointerField()
        {
            var e1 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e1, new ComponentWithPointer());

            using (var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator))
            {
                Assert.Throws<ArgumentException>(() =>
                    SerializeUtility.SerializeWorld(m_Manager, writer)
                );
            }
        }

        public struct ComponentWithIntPtr : IComponentData
        {
            int m_Pad;
            IntPtr m_Data;
        }

        [Test]
        public void SerializeComponentWithIntPtrField()
        {
            var e1 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e1, new ComponentWithIntPtr());

            using (var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator))
            {
                Assert.Throws<ArgumentException>(() =>
                    SerializeUtility.SerializeWorld(m_Manager, writer)
                );
            }
        }

        public struct ValidTypeForSerialization
        {
            public int m_Int;
            public byte m_Byte;
        }

        public unsafe struct TypeWithPointer
        {
            int m_Pad;
            byte* m_Data;
        }

        public struct TypeWithNestedPointer
        {
            int m_Pad;
            ValidTypeForSerialization m_ValidStruct;
            TypeWithPointer m_Data;
            int m_Pad1;
        }

        public struct ComponentWithNestedPointer : IComponentData
        {
            int m_Pad;
            TypeWithNestedPointer m_PointerField;
            int m_Pad1;
        }

        [Test]
        public void SerializeComponentWithNestedPointerField()
        {
            var e1 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e1, new ComponentWithNestedPointer());

            using (var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator))
            {
                Assert.Throws<ArgumentException>(() =>
                    SerializeUtility.SerializeWorld(m_Manager, writer)
                );
            }
        }

        public struct TypeWithNestedWhiteListType
        {
            int m_Pad;
            ChunkHeader m_Header; // whitelisted pointer type
            int m_Pad1;
        }

        public struct ComponentWithNestedPointerAndNestedWhiteListType : IComponentData
        {
            TypeWithNestedPointer m_PointerField;
            TypeWithNestedWhiteListType m_NestedWhitelistField;
        }
#pragma warning restore 169

        [Test]
        public void EnsureSerializationWhitelistingDoesNotTrumpValidation()
        {
            var e1 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e1, new ComponentWithNestedPointerAndNestedWhiteListType());

            using (var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator))
            {
                Assert.Throws<ArgumentException>(() =>
                    SerializeUtility.SerializeWorld(m_Manager, writer)
                );
            }
        }

        [Test]
        public void DeserializedChunksAreConsideredChangedOnlyOnce()
        {
            TestBinaryReader CreateSerializedData()
            {
                using var world = new World("DeserializedChunksAreConsideredChangedOnlyOnce World");
                var manager = world.EntityManager;
                var entity = manager.CreateEntity();
                manager.AddComponentData(entity, new EcsTestData(42));

                // owned by caller via reader
                var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
                SerializeUtility.SerializeWorld(manager, writer, out var objRefs);
                return new TestBinaryReader(writer);
            }

            var reader = CreateSerializedData();

            using var deserializedWorld = new World("DeserializedChunksAreConsideredChangedOnlyOnce World 2");
            var deserializedManager = deserializedWorld.EntityManager;

            var system = deserializedWorld.GetOrCreateSystemManaged<TestEcsChangeSystem>();
            system.Update();
            Assert.AreEqual(0, system.NumChanged);
            deserializedWorld.DestroySystemManaged(system);

            SerializeUtility.DeserializeWorld(deserializedManager.BeginExclusiveEntityTransaction(), reader);
            deserializedManager.EndExclusiveEntityTransaction();
            reader.Dispose();

            system = deserializedWorld.GetOrCreateSystemManaged<TestEcsChangeSystem>();
            system.Update();
            Assert.AreEqual(1, system.NumChanged);

            system.Update();
            Assert.AreEqual(0, system.NumChanged);
        }

        [Test]
        public void DeserializedWorldCantContainSystems()
        {
            TestBinaryReader CreateSerializedData()
            {
                using var world = new World("DeserializedChunksAreConsideredChangedOnlyOnce World");
                var manager = world.EntityManager;
                var entity = manager.CreateEntity();
                manager.AddComponentData(entity, new EcsTestData(42));

                // owned by caller via reader
                var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
                SerializeUtility.SerializeWorld(manager, writer, out var objRefs);
                return new TestBinaryReader(writer);
            }

            var reader = CreateSerializedData();

            using var deserializedWorld = new World("DeserializedChunksAreConsideredChangedOnlyOnce World 2");
            var deserializedManager = deserializedWorld.EntityManager;

            var system = deserializedWorld.GetOrCreateSystemManaged<TestEcsChangeSystem>();
            Assert.Throws<ArgumentException>(() => SerializeUtility.DeserializeWorld(deserializedManager.BeginExclusiveEntityTransaction(), reader));
            deserializedManager.EndExclusiveEntityTransaction();
            reader.Dispose();
        }

        [Test]
        public void SerializedWorldDoesntSerializeSystemEntities()
        {
            TestBinaryReader CreateSerializedData()
            {
                using var world = new World("DeserializedChunksAreConsideredChangedOnlyOnce World");
                var manager = world.EntityManager;
                var entity = manager.CreateEntity();
                manager.AddComponentData(entity, new EcsTestData(42));

                var system = world.GetOrCreateSystemManaged<TestEcsChangeSystem>();

                // owned by caller via reader
                var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
                SerializeUtility.SerializeWorld(manager, writer, out var objRefs);
                return new TestBinaryReader(writer);
            }

            var reader = CreateSerializedData();

            using var deserializedWorld = new World("DeserializedChunksAreConsideredChangedOnlyOnce World 2");
            var deserializedManager = deserializedWorld.EntityManager;

            SerializeUtility.DeserializeWorld(deserializedManager.BeginExclusiveEntityTransaction(), reader);
            deserializedManager.EndExclusiveEntityTransaction();
            reader.Dispose();

            Assert.IsTrue(deserializedManager.UniversalQuery.CalculateEntityCount() == 1);
            Assert.IsTrue(deserializedManager.UniversalQueryWithSystems.CalculateEntityCount() == 1);
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        public class ManagedComponent : IComponentData
        {
            public string String;
            public Dictionary<string, int> Map;
        }

        public class ManagedComponent2 : IComponentData
        {
            public List<string> List;
        }

        [Test]
        [IgnoreTest_IL2CPP("DOTSE-1903 - Users Properties which is broken in non-generic sharing IL2CPP builds")]
        public void SerializeEntities_HandlesNullManagedComponents()
        {
            var e = m_Manager.CreateEntity(typeof(ManagedComponent));
            // disposed via reader
            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer);
            using (var reader = new TestBinaryReader(writer))
            using (var deserializedWorld = new World("SerializeEntities_HandlesNullManagedComponents Test World"))
            {
                var entityManager = deserializedWorld.EntityManager;

                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
                entityManager.EndExclusiveEntityTransaction();

                Assert.IsNull(entityManager.CreateEntityQuery(typeof(ManagedComponent)).GetSingleton<ManagedComponent>());
            }
        }

        [Test]
        [IgnoreTest_IL2CPP("DOTSE-1903 - Users Properties which is broken in non-generic sharing IL2CPP builds")]
        public void SerializeEntities_RemapsEntitiesInManagedComponents()
        {
            int numberOfEntitiesPerManager = 10000;

            var targetArchetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var targetEntities = new NativeArray<Entity>(numberOfEntitiesPerManager, Allocator.Temp);
            m_Manager.CreateEntity(targetArchetype, targetEntities);
            for (int i = 0; i != targetEntities.Length; i++)
                m_Manager.SetComponentData(targetEntities[i], new EcsTestData(i));

            var sourceArchetype = m_Manager.CreateArchetype(typeof(EcsTestManagedDataEntity));
            var sourceEntities = new NativeArray<Entity>(numberOfEntitiesPerManager, Allocator.Temp);
            m_Manager.CreateEntity(sourceArchetype, sourceEntities);
            for (int i = 0; i != sourceEntities.Length; i++)
            {
                int index = i & ~1;
                m_Manager.SetComponentData(sourceEntities[i],  new EcsTestManagedDataEntity("foo", targetEntities[index], index));
            }

            // Destroy ever 2nd target entity to ensure something changes when entity ids are compacted during serialization
            for (int i = 0; i != targetEntities.Length / 2; i++)
                m_Manager.DestroyEntity(targetEntities[i * 2 + 1]);

            using var deserializedWorld = new World("SerializeEntities_RemapsEntitiesInManagedComponents Test World");

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer);
            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(deserializedWorld.EntityManager.BeginExclusiveEntityTransaction(), reader);
                deserializedWorld.EntityManager.EndExclusiveEntityTransaction();
            }

            var entityManager = deserializedWorld.EntityManager;

            m_Manager.Debug.CheckInternalConsistency();
            entityManager.Debug.CheckInternalConsistency();


            var group = entityManager.CreateEntityQuery(typeof(EcsTestData));
            Assert.AreEqual(numberOfEntitiesPerManager / 2, group.CalculateEntityCount());

            var managedGroup = entityManager.CreateEntityQuery(typeof(EcsTestManagedDataEntity));
            Assert.AreEqual(numberOfEntitiesPerManager, managedGroup.CalculateEntityCount());


            var managedTestDataArray = managedGroup.ToComponentDataArray<EcsTestManagedDataEntity>();
            for (int i = 0; i != managedTestDataArray.Length; i++)
            {
                Assert.AreEqual(managedTestDataArray[i].value2, entityManager.GetComponentData<EcsTestData>(managedTestDataArray[i].value1).value);
            }

            targetEntities.Dispose();
            sourceEntities.Dispose();
        }

        [Test]
        [IgnoreTest_IL2CPP("DOTSE-1903 - Users Properties which is broken in non-generic sharing IL2CPP builds")]
        public void SerializeEntities_ManagedComponents()
        {
            int expectedEntityCount = 1000;
            for (int i = 0; i < expectedEntityCount; ++i)
            {
                var e1 = m_Manager.CreateEntity();

                var expectedManagedComponent = new ManagedComponent { String = i.ToString(), Map = new Dictionary<string, int>() };
                expectedManagedComponent.Map.Add("positive", i);
                expectedManagedComponent.Map.Add("negative", -i);

                var expectedManagedComponent2 = new ManagedComponent2() { List = new List<string>() };
                expectedManagedComponent2.List.Add("one");
                expectedManagedComponent2.List.Add("two");
                expectedManagedComponent2.List.Add("three");
                expectedManagedComponent2.List.Add("four");

                m_Manager.AddComponentData(e1, expectedManagedComponent);
                m_Manager.AddComponentData(e1, expectedManagedComponent2);
            }

            // disposed via reader
            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer);
            var reader = new TestBinaryReader(writer);

            var deserializedWorld = new World("SerializeEntities_ManagedComponents Test World");
            var entityManager = deserializedWorld.EntityManager;

            SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
            entityManager.EndExclusiveEntityTransaction();

            try
            {
                var allEntities = entityManager.GetAllEntities(Allocator.Temp);
                var actualEntityCount = allEntities.Length;
                allEntities.Dispose();

                Assert.AreEqual(expectedEntityCount, actualEntityCount);

                var group1 = entityManager.CreateEntityQuery(typeof(ManagedComponent));

                Assert.AreEqual(actualEntityCount, group1.CalculateEntityCount());

                using (var entities = group1.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {
                    for (int i = 0; i < entities.Length; ++i)
                    {
                        var e = entities[i];

                        var actualManagedComponent = entityManager.GetComponentData<ManagedComponent>(e);
                        Assert.AreEqual(i.ToString(), actualManagedComponent.String);
                        Assert.AreEqual(2, actualManagedComponent.Map.Count);
                        Assert.AreEqual(i, actualManagedComponent.Map["positive"]);
                        Assert.AreEqual(-i, actualManagedComponent.Map["negative"]);

                        var actualManagedComponent2 = entityManager.GetComponentData<ManagedComponent2>(e);
                        Assert.AreEqual(4, actualManagedComponent2.List.Count);
                        Assert.AreEqual("one", actualManagedComponent2.List[0]);
                        Assert.AreEqual("two", actualManagedComponent2.List[1]);
                        Assert.AreEqual("three", actualManagedComponent2.List[2]);
                        Assert.AreEqual("four", actualManagedComponent2.List[3]);
                    }
                }
            }
            finally
            {
                deserializedWorld.Dispose();
                reader.Dispose();
            }
        }

        public interface InterfaceType {}

        public class InterfaceImplType : InterfaceType
        {
            public string String;
            public InterfaceImplType()
            {
                String = null;
            }

            public InterfaceImplType(int i)
            {
                String = i.ToString();
            }
        }

        public class InterfaceImplType2 : InterfaceType
        {
            public int Int;
            public InterfaceType NestedInterfaceType;
            public InterfaceImplType2()
            {
                NestedInterfaceType = null;
            }

            public InterfaceImplType2(int i)
            {
                Int = i;
                NestedInterfaceType = new InterfaceImplType(i);
            }
        }

        public abstract class AbstractType {}
        public class AbstractImplType : AbstractType
        {
            public string String;

            public AbstractImplType()
            {
                String = null;
            }

            public AbstractImplType(int i)
            {
                String = i.ToString();
            }
        }

        public class NestedAbstractImplType : AbstractImplType
        {
            public int Int;

            public NestedAbstractImplType()
                : base()
            {
                Int = 0;
            }

            public NestedAbstractImplType(int i) : base(i)
            {
                Int = i;
            }
        }

        public class BaseType
        {
            public int Int;

            public BaseType()
            {
                Int = 0;
            }

            public BaseType(int i)
            {
                Int = i;
            }
        }

        public class ChildType : BaseType
        {
            public string String;

            public ChildType()
                : base()
            {
                String = null;
            }

            public ChildType(int i)
                : base(i)
            {
                String = (i + 1).ToString();
            }
        }

        public class GrandChildType : ChildType
        {
            public object BoxedByte;

            public GrandChildType()
                : base()
            {
                BoxedByte = (byte)0;
            }

            public GrandChildType(int i)
                : base(i)
            {
                BoxedByte = (byte)(i + 2);
            }
        }

        public class MyClass
        {
            public const int kIterations = 16;
            public int mId;
            public uint4 mInt4;
            public int[] mArray;
            public BaseType mBaseType;
            public InterfaceType mInterfaceType;
            public AbstractType mAbstractType;
            public List<InterfaceType> mInterfaceList;
            public List<AbstractType> mAbstractClassList;
            public List<BaseType> mBaseTypeList;

            public MyClass()
            {
                mId = 0;
                mInt4 = new uint4();
                mArray = null;
            }

            public MyClass(int v)
            {
                mId = v;
                mInt4 = new uint4(v + 1);
                mArray = new int[kIterations];
                for (int i = 0; i < kIterations; ++i)
                    mArray[i] = v + i;

                mInterfaceType = new InterfaceImplType(v);
                mAbstractType = new AbstractImplType(v);
                mBaseType = new ChildType(v);

                mInterfaceList = new List<InterfaceType>();
                for (int i = 0; i < kIterations; ++i)
                {
                    if ((i & 1) == 0)
                        mInterfaceList.Add(new InterfaceImplType(v + i));
                    else
                        mInterfaceList.Add(new InterfaceImplType2(v + i));
                }

                mAbstractClassList = new List<AbstractType>();
                for (int i = 0; i < kIterations; ++i)
                {
                    if ((i & 1) == 0)
                        mAbstractClassList.Add(new AbstractImplType(v + i));
                    else
                        mAbstractClassList.Add(new NestedAbstractImplType(v + i));
                }

                mBaseTypeList = new List<BaseType>();
                for (int i = 0; i < kIterations; ++i)
                {
                    if (i % 3 == 0)
                        mBaseTypeList.Add(new BaseType(v + i));
                    else if (i % 3 == 1)
                        mBaseTypeList.Add(new ChildType(v + i));
                    else if (i % 3 == 2)
                        mBaseTypeList.Add(new GrandChildType(v + i));
                }
            }

            public int GetId() { return mId; }
        }

        public class ManagedComponentCustomClass : IComponentData
        {
            public MyClass mClass;

            public ManagedComponentCustomClass()
            {
                mClass = null;
            }

            public ManagedComponentCustomClass(MyClass c)
            {
                mClass = c;
            }
        }

        [Test]
        [IgnoreTest_IL2CPP("DOTSE-1903 - Users Properties which is broken in non-generic sharing IL2CPP builds")]
        public void SerializeEntitiesManagedComponentWithCustomClass_ManagedComponents()
        {
            int expectedEntityCount = 100;
            for (int i = 0; i < expectedEntityCount; ++i)
            {
                var e1 = m_Manager.CreateEntity();

                var expectedManagedComponent = new ManagedComponentCustomClass(new MyClass(i));
                m_Manager.AddComponentData(e1, expectedManagedComponent);
            }

            // disposed via reader
            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer);
            var reader = new TestBinaryReader(writer);

            var deserializedWorld = new World("SerializeEntitiesManagedComponentWithCustomClass_ManagedComponents Test World");
            var entityManager = deserializedWorld.EntityManager;

            SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
            entityManager.EndExclusiveEntityTransaction();

            try
            {
                var allEntities = entityManager.GetAllEntities(Allocator.Temp);
                var actualEntityCount = allEntities.Length;
                allEntities.Dispose();

                Assert.AreEqual(expectedEntityCount, actualEntityCount);

                var group1 = entityManager.CreateEntityQuery(typeof(ManagedComponentCustomClass));

                Assert.AreEqual(actualEntityCount, group1.CalculateEntityCount());

                using (var entities = group1.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {
                    NativeParallelHashMap<int, int> idSet = new NativeParallelHashMap<int, int>(entities.Length, Allocator.Temp);
                    for (int i = 0; i < entities.Length; ++i)
                    {
                        var e = entities[i];

                        var actualManagedComponent = entityManager.GetComponentData<ManagedComponentCustomClass>(e);
                        var myClass = actualManagedComponent.mClass;
                        Assert.IsNotNull(actualManagedComponent);
                        Assert.IsNotNull(myClass);
                        int id = actualManagedComponent.mClass.GetId();
                        Assert.IsTrue(idSet.TryAdd(id, id));
                        Assert.IsTrue(myClass.mInt4.Equals(new uint4(id + 1)));
                        Assert.IsNotNull(myClass.mArray);
                        Assert.IsTrue(myClass.mArray.Length == MyClass.kIterations);
                        for (int j = 0; j < MyClass.kIterations; ++j)
                        {
                            Assert.IsTrue(myClass.mArray[j] == id + j);
                        }

                        // Polymorphic types
                        Assert.IsNotNull(myClass.mInterfaceType);
                        var concreteInterfaceType = (InterfaceImplType)myClass.mInterfaceType;
                        Assert.IsTrue(concreteInterfaceType.String == id.ToString());

                        Assert.IsNotNull(myClass.mAbstractType);
                        var concreteAbstractType = (AbstractImplType)myClass.mAbstractType;
                        Assert.IsTrue(concreteAbstractType.String == id.ToString());

                        Assert.IsNotNull(myClass.mBaseType);
                        var concreteBaseType = (ChildType)myClass.mBaseType;
                        Assert.IsTrue(concreteBaseType.Int == id);
                        Assert.IsTrue(concreteBaseType.String == (id + 1).ToString());

                        Assert.IsNotNull(myClass.mInterfaceList);
                        Assert.IsTrue(myClass.mInterfaceList.Count == MyClass.kIterations);
                        for (int j = 0; j < MyClass.kIterations; ++j)
                        {
                            var interfaceType = myClass.mInterfaceList[j];
                            if ((j & 1) == 0)
                            {
                                var concreteType = (InterfaceImplType)interfaceType;
                                Assert.AreEqual((id + j).ToString(), concreteType.String);
                            }
                            else
                            {
                                var concreteType = (InterfaceImplType2)interfaceType;
                                Assert.IsTrue(concreteType.Int == (id + j));
                                var nestedConcreteType = (InterfaceImplType)concreteType.NestedInterfaceType;
                                Assert.IsTrue(nestedConcreteType.String == (id + j).ToString());
                            }
                        }

                        Assert.IsNotNull(myClass.mAbstractClassList);
                        Assert.IsTrue(myClass.mAbstractClassList.Count == MyClass.kIterations);
                        for (int j = 0; j < MyClass.kIterations; ++j)
                        {
                            var abstractType = myClass.mAbstractClassList[j];
                            if ((j & 1) == 0)
                            {
                                var concreteType = (AbstractImplType)abstractType;
                                Assert.IsTrue(concreteType.String == (id + j).ToString());
                            }
                            else
                            {
                                var concreteType = (NestedAbstractImplType)abstractType;
                                Assert.IsTrue(concreteType.String == (id + j).ToString());
                                Assert.IsTrue(concreteType.Int == (id + j));
                            }
                        }

                        Assert.IsNotNull(myClass.mBaseTypeList);
                        Assert.IsTrue(myClass.mBaseTypeList.Count == MyClass.kIterations);
                        for (int j = 0; j < MyClass.kIterations; ++j)
                        {
                            var baseType = myClass.mBaseTypeList[j];
                            if ((j % 3) == 0)
                            {
                                var concreteType = (BaseType)baseType;
                                Assert.AreEqual((id + j), concreteType.Int);
                            }
                            else if ((j % 3) == 1)
                            {
                                var concreteType = (ChildType)baseType;
                                Assert.AreEqual((id + j), concreteType.Int);
                                Assert.AreEqual((id + j + 1).ToString(), concreteType.String);
                            }
                            else if ((j % 3) == 2)
                            {
                                var concreteType = (GrandChildType)baseType;
                                Assert.AreEqual((id + j), concreteType.Int);
                                Assert.AreEqual((id + j + 1).ToString(), concreteType.String);
                                Assert.AreEqual((id + j + 2), (byte)concreteType.BoxedByte);
                            }
                        }
                    }
                    idSet.Dispose();
                }
            }
            finally
            {
                deserializedWorld.Dispose();
            }
        }

#endif // !UNITY_DISABLE_MANAGED_COMPONENTS

        [Test]
        public void SerializeEntities_WithBlobAssetReferencesInSharedComponents()
        {
            using var blobRefA = BlobAssetReference<int>.Create(123);
            Entity a = m_Manager.CreateEntity();
            m_Manager.AddSharedComponent(a, new EcsTestSharedCompBlobAssetRef
            {
                value = blobRefA,
            });

            using var blobRefB = BlobAssetReference<int>.Create(123);
            Entity b = m_Manager.CreateEntity();
            m_Manager.AddSharedComponent(b, new EcsTestSharedCompBlobAssetRef
            {
                value = blobRefB,
            });

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            using (var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestSharedCompBlobAssetRef>().Build(entityManager))
            {
                Assert.AreEqual(2, query.CalculateChunkCount());
            }
        }

        enum TestEnum
        {
            Zero,
            Value1 = 5
        }

        struct TestUnmanagedStruct : ISharedComponentData
        {
            public int Value;
            public float3 Float3;
            public TestEnum EnumValue;
            public FixedString64Bytes StringValue;
        }

        [Test]
        public void SerializeEntities_WithUnmanagedSharedComponents_Works()
        {
            var s1 = new TestUnmanagedStruct
            {
                Value = 42,
                Float3 = new float3(1, 2, 3),
                EnumValue = TestEnum.Value1,
                StringValue = "boring string 漢漢"
            };
            var s2 = new TestUnmanagedStruct
            {
                Value = 43,
                Float3 = new float3(4, 5, 6),
                EnumValue = TestEnum.Zero,
                StringValue = "漢漢 Other"
            };

            Entity a = m_Manager.CreateEntity();
            m_Manager.AddSharedComponent(a, s1);

            Entity b = m_Manager.CreateEntity();
            m_Manager.AddSharedComponent(b, s2);

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            entityManager.GetAllUniqueSharedComponents<TestUnmanagedStruct>(out var sharedComponents, Allocator.Temp);
            Assert.AreEqual(3, sharedComponents.Length, "Serialization / Deserialization failed - unexpected number of shared components");
            CollectionAssert.AreEquivalent(new[] {default(TestUnmanagedStruct), s1, s2}, sharedComponents.AsArray().ToArray(), "The shared component values are not equal");
        }

        struct TestUnmanagedBlobStruct : ISharedComponentData
        {
            public int Value;
            public BlobAssetReference<BlobArray<float>> BlobAssetReference;
        }

        BlobAssetReference<BlobArray<float>> CreateBlobArrayRef(float[] values)
        {
            var builder1 = new BlobBuilder(Allocator.Temp);
            ref var blobArray = ref builder1.ConstructRoot<BlobArray<float>>();
            var array1 = builder1.Allocate(ref blobArray, values.Length);
            for (int index = 0; index < values.Length; index++)
                array1[index] = values[index];
            var reference = builder1.CreateBlobAssetReference<BlobArray<float>>(Allocator.Temp);
            builder1.Dispose();
            return reference;
        }

        [Test]
        public void SerializeEntities_WithUnmanagedSharedComponentsDifferentBlob_Works()
        {
            float[] array1 = {1.7f, 2.6f};
            var s1 = new TestUnmanagedBlobStruct
            {
                Value = 4,
                BlobAssetReference = CreateBlobArrayRef(array1)
            };

            float[] array2 = {2.7f, 3.6f};
            var s2 = new TestUnmanagedBlobStruct
            {
                Value = 6,
                BlobAssetReference = CreateBlobArrayRef(array2)
            };

            Entity a = m_Manager.CreateEntity();
            m_Manager.AddSharedComponent(a, s1);

            Entity b = m_Manager.CreateEntity();
            m_Manager.AddSharedComponent(b, s2);

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            entityManager.GetAllUniqueSharedComponents<TestUnmanagedBlobStruct>(out var sharedComponents, Allocator.Temp);
            // Index 0 is reserved for the default value for the shared component
            Assert.AreEqual(3, sharedComponents.Length, "Serialization / Deserialization failed - unexpected number of shared components");

            var readS1 = sharedComponents[1];
            Assert.AreEqual(4, readS1.Value, "Expected the same int value for s1");
            var readArray1 = readS1.BlobAssetReference.Value.ToArray();
            CollectionAssert.AreEqual(array1, readArray1);

            var readS2 = sharedComponents[2];
            Assert.AreEqual(6, readS2.Value, "Expected the same int value for s2");
            var readArray2 = readS2.BlobAssetReference.Value.ToArray();
            CollectionAssert.AreEqual(array2, readArray2);
        }

        [Test]
        public void SerializeEntities_WithUnmanagedSharedComponentsSameBlob_Works()
        {
            float[] array = {1.7f, 2.6f};
            var blobRef = CreateBlobArrayRef(array);
            var s1 = new TestUnmanagedBlobStruct
            {
                Value = 4,
                BlobAssetReference = blobRef
            };

            var s2 = new TestUnmanagedBlobStruct
            {
                Value = 6,
                BlobAssetReference = blobRef
            };

            Entity a = m_Manager.CreateEntity();
            m_Manager.AddSharedComponent(a, s1);

            Entity b = m_Manager.CreateEntity();
            m_Manager.AddSharedComponent(b, s2);

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            entityManager.GetAllUniqueSharedComponents<TestUnmanagedBlobStruct>(out var sharedComponents, Allocator.Temp);
            // Index 0 is reserved for the default value for the shared component
            Assert.AreEqual(3, sharedComponents.Length, "Serialization / Deserialization failed - unexpected number of shared components");

            var readS1 = sharedComponents[1];
            Assert.AreEqual(4, readS1.Value, "Expected the same int value for s1");
            var readArray1 = readS1.BlobAssetReference.Value.ToArray();
            CollectionAssert.AreEqual(array, readArray1);

            var readS2 = sharedComponents[2];
            Assert.AreEqual(6, readS2.Value, "Expected the same int value for s2");
            var readArray2 = readS2.BlobAssetReference.Value.ToArray();
            CollectionAssert.AreEqual(array, readArray2);

            // Make sure the blob is shared
            unsafe
            {
                Assert.IsTrue((byte*)readS1.BlobAssetReference.GetUnsafePtr() == (byte*)readS2.BlobAssetReference.GetUnsafePtr(), "Both components should point to the same blob");
            }
        }

        struct TestManagedStruct : ISharedComponentData, IEquatable<TestManagedStruct>
        {
            public int Value;
            public float3 Float3;
            public TestEnum EnumValue;
            public string StringValue;

            public bool Equals(TestManagedStruct other)
            {
                return Value == other.Value && Float3.Equals(other.Float3) && EnumValue == other.EnumValue && StringValue == other.StringValue;
            }

            public override bool Equals(object obj)
            {
                return obj is TestManagedStruct other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Value, Float3, (int) EnumValue, StringValue);
            }

            public static bool operator ==(TestManagedStruct left, TestManagedStruct right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(TestManagedStruct left, TestManagedStruct right)
            {
                return !left.Equals(right);
            }
        }

        [Test]
        public void SerializeEntities_WithManagedSharedComponents_Works()
        {
            var s1 = new TestManagedStruct
            {
                Value = 42,
                Float3 = new float3(1, 2, 3),
                EnumValue = TestEnum.Value1,
                StringValue = "boring string 漢漢"
            };
            var s2 = new TestManagedStruct
            {
                Value = 43,
                Float3 = new float3(4, 5, 6),
                EnumValue = TestEnum.Zero,
                StringValue = "漢漢 Other"
            };

            Entity a = m_Manager.CreateEntity();
            m_Manager.AddSharedComponentManaged(a, s1);

            Entity b = m_Manager.CreateEntity();
            m_Manager.AddSharedComponentManaged(b, s2);

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            List<TestManagedStruct> sharedComponentValues = new List<TestManagedStruct>();
            List<int> sharedComponentIndices = new List<int>();
            entityManager.GetAllUniqueSharedComponentsManaged<TestManagedStruct>(sharedComponentValues, sharedComponentIndices);
            Assert.AreEqual(3, sharedComponentValues.Count, "Serialization / Deserialization failed - unexpected number of shared components");
            CollectionAssert.AreEquivalent(new[] {new TestManagedStruct(), s1, s2}, sharedComponentValues, "The shared component values are not equal");
        }

#if SUPPORT_SHARED_COMPONENT_REMAPPING
        [Test]
        public void SerializeEntities_WithEntityReferencesInUnmanagedSharedComponents_RemapsEntities()
        {
            {
                var archetype = m_Manager.CreateArchetype(ComponentType.ReadWrite<EcsTestData>());
                var entities = m_Manager.CreateEntity(archetype, 20, Allocator.Temp);

                Entity a = m_Manager.CreateEntity();
                m_Manager.SetComponentData(entities[12], new EcsTestData(12345));
                m_Manager.AddSharedComponentManaged(a, new EcsTestSharedCompEntity(entities[12]));

                Entity b = m_Manager.CreateEntity();
                m_Manager.SetComponentData(entities[17], new EcsTestData(23456));
                m_Manager.AddSharedComponentManaged(b, new EcsTestSharedCompEntity(entities[17]));

                entities[12] = Entity.Null;
                entities[17] = Entity.Null;
                m_Manager.DestroyEntity(entities);
            }

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            Assert.DoesNotThrow(() => SerializeUtility.SerializeWorld(m_Manager, writer));

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            entityManager.GetAllUniqueSharedComponents<EcsTestSharedCompEntity>(out var sharedComponents, Allocator.Temp);
            Assert.AreEqual(3, sharedComponents.Length, "Serialization / Deserialization failed - unexpected number of shared components");
            Assert.IsTrue(entityManager.Exists(sharedComponents[1].value), "Entity remapping failed for unmanaged shared component");
            Assert.IsTrue(entityManager.Exists(sharedComponents[2].value), "Entity remapping failed for unmanaged shared component");
            CollectionAssert.AreEquivalent(new[] {12345, 23456}, new[] {entityManager.GetComponentData<EcsTestData>(sharedComponents[1].value).value, entityManager.GetComponentData<EcsTestData>(sharedComponents[2].value).value});
        }
#else
        [Test]
        public void SerializeEntities_WithEntityReferencesInUnmanagedSharedComponents_ThrowsArgumentException()
        {
            {
                var archetype = m_Manager.CreateArchetype(ComponentType.ReadWrite<EcsTestData>());
                var entities = m_Manager.CreateEntity(archetype, 20, Allocator.Temp);

                Entity a = m_Manager.CreateEntity();
                m_Manager.SetComponentData(entities[12], new EcsTestData(12345));
                m_Manager.AddSharedComponentManaged(a, new EcsTestSharedCompEntity(entities[12]));

                Entity b = m_Manager.CreateEntity();
                m_Manager.SetComponentData(entities[17], new EcsTestData(23456));
                m_Manager.AddSharedComponentManaged(b, new EcsTestSharedCompEntity(entities[17]));

                entities[12] = Entity.Null;
                entities[17] = Entity.Null;
                m_Manager.DestroyEntity(entities);
            }

            using (var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator))
            {
                Assert.Throws<ArgumentException>(() => SerializeUtility.SerializeWorld(m_Manager, writer));
            }
        }
#endif // SUPPORT_SHARED_COMPONENT_REMAPPING

#if !UNITY_DISABLE_MANAGED_COMPONENTS

        [Test]
        public void SerializeEntities_WithEntityReferencesInManagedSharedComponents_ThrowsArgumentException()
        {
            {
                var archetype = m_Manager.CreateArchetype(ComponentType.ReadWrite<EcsTestData>());
                var entities = m_Manager.CreateEntity(archetype, 20, Allocator.Temp);

                Entity a = m_Manager.CreateEntity();
                m_Manager.SetComponentData(entities[12], new EcsTestData(12345));
                m_Manager.AddSharedComponentManaged(a, new EcsTestSharedCompManagedEntity(entities[12], "First"));

                Entity b = m_Manager.CreateEntity();
                m_Manager.SetComponentData(entities[17], new EcsTestData(23456));
                m_Manager.AddSharedComponentManaged(b, new EcsTestSharedCompManagedEntity(entities[17], "Second"));

                entities[12] = Entity.Null;
                entities[17] = Entity.Null;
                m_Manager.DestroyEntity(entities);
            }

            using (var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator))
            {
                Assert.Throws<ArgumentException>(() => SerializeUtility.SerializeWorld(m_Manager, writer));
            }
        }
#endif // !UNITY_DISABLE_MANAGED_COMPONENTS

        public struct SharedComponentWithEntityReference : ISharedComponentData
        {
            public Entity Entity;
        }

        [ChunkSerializable]
        public struct SerializableSharedComponentWithEntityReference : ISharedComponentData
        {
            public Entity Entity;
        }

        [Test]
        public void SerializeEntities_SharedComponentWithEntityThrows()
        {
            var blobArchetype = m_Manager.CreateArchetype(typeof(SharedComponentWithEntityReference));
            m_Manager.CreateEntity(blobArchetype);

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            Assert.Throws<ArgumentException>(() => SerializeUtility.SerializeWorld(m_Manager, writer));
        }

        [Test]
        public void SerializeEntities_SerializableSharedComponentWithEntityDoesNotThrow()
        {
            var blobArchetype = m_Manager.CreateArchetype(typeof(SerializableSharedComponentWithEntityReference));
            m_Manager.CreateEntity(blobArchetype);

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            Assert.DoesNotThrow(() => SerializeUtility.SerializeWorld(m_Manager, writer));
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        [IgnoreTest_IL2CPP("DOTSE-1903 - Users Properties which is broken in non-generic sharing IL2CPP builds")]
        public void SerializeEntities_WithBlobAssetReferencesInManagedComponents()
        {
            using var blobRefA = BlobAssetReference<int>.Create(123);
            using var blobRefB = BlobAssetReference<int>.Create(234);
            {
                Entity a = m_Manager.CreateEntity();
                m_Manager.AddComponentData(a, new EcsTestManagedDataBlobAssetRef
                {
                    value = blobRefA,
                });

                Entity b = m_Manager.CreateEntity();
                m_Manager.AddComponentData(b, new EcsTestManagedDataBlobAssetRef
                {
                    value = blobRefB,
                });
            }

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            using (var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestManagedDataBlobAssetRef>().Build(entityManager))
            {
                var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(2, entities.Length);

                var a = entityManager.GetComponentData<EcsTestManagedDataBlobAssetRef>(entities[0]).value.Value;
                var b = entityManager.GetComponentData<EcsTestManagedDataBlobAssetRef>(entities[1]).value.Value;

                Assert.AreEqual(123 + 234, a + b);
            }
        }
#endif // !UNITY_DISABLE_MANAGED_COMPONENTS

        // Unity Obj Ref serialization
        public struct EcsTestComponentWithUnityObjectRef : IComponentData
        {
            public UnityObjectRef<UnityEngine.GameObject> value;
        }

        public struct EcsTestSharedComponentWithUnityObjectRef : ISharedComponentData
        {
            public UnityObjectRef<UnityEngine.GameObject> value;
        }

        public struct EcsTestBufferWithUnityObjectRef : IBufferElementData
        {
            public UnityObjectRef<UnityEngine.GameObject> value;
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        public class EcsTestManagedComponentWithUnityObjectRef : IComponentData
        {
            public UnityObjectRef<UnityEngine.GameObject> value;
        }

        public struct EcsTestManagedSharedComponentWithUnityObjectRef : ISharedComponentData, IEquatable<EcsTestManagedSharedComponentWithUnityObjectRef>
        {
            public UnityObjectRef<UnityEngine.GameObject> value;

            // Add a managed field to force the type manager to see this as a managed shared component
            public object ManagedField;

            public bool Equals(EcsTestManagedSharedComponentWithUnityObjectRef other)
            {
                return value.Equals(other.value) && Equals(ManagedField, other.ManagedField);
            }

            public override bool Equals(object obj)
            {
                return obj is EcsTestManagedSharedComponentWithUnityObjectRef other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(value, ManagedField);
            }
        }
#endif

        [Test]
        public void SerializeEntities_WithUnityObjRef_ComponentData()
        {
            var go = new UnityEngine.GameObject("Foo");
            {
                Entity a = m_Manager.CreateEntity();
                m_Manager.AddComponentData(a, new EcsTestComponentWithUnityObjectRef
                {
                    value = go
                });
            }

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer, out var referencedObjects);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            // Make sure we got one object out
            Assert.AreEqual(1, referencedObjects.Length);

            // Make sure it's the same object
            var referencedObj = (UnityEngine.GameObject)referencedObjects[0];
            Assert.AreEqual(go.GetInstanceID(), referencedObj.GetInstanceID());

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader, referencedObjects);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            using (var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestComponentWithUnityObjectRef>().Build(entityManager))
            {
                var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, entities.Length);

                var a = entityManager.GetComponentData<EcsTestComponentWithUnityObjectRef>(entities[0]).value.Value;

                Assert.AreEqual(go.GetInstanceID(), a.GetInstanceID());
            }
        }

        [Test]
        public void SerializeEntities_WithUnityObjRef_ComponentData_Default()
        {
            UnityEngine.GameObject go = null;
            {
                Entity a = m_Manager.CreateEntity();
                m_Manager.AddComponentData(a, new EcsTestComponentWithUnityObjectRef
                {
                    value = go
                });
            }

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer, out var referencedObjects);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            // Make sure we got no referenced objects
            Assert.AreEqual(0, referencedObjects.Length);

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader, referencedObjects);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            using (var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestComponentWithUnityObjectRef>().Build(entityManager))
            {
                var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, entities.Length);

                var a = entityManager.GetComponentData<EcsTestComponentWithUnityObjectRef>(entities[0]).value;

                Assert.IsTrue(a == go);
            }
        }

        [Test]
        public void SerializeEntities_WithUnityObjRef_SharedComponentData()
        {
            var go = new UnityEngine.GameObject("Foo");
            {
                Entity a = m_Manager.CreateEntity();
                m_Manager.AddSharedComponent(a, new EcsTestSharedComponentWithUnityObjectRef
                {
                    value = go
                });
            }

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer, out var referencedObjects);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            // Make sure we got one object out
            Assert.AreEqual(1, referencedObjects.Length);

            // Make sure it's the same object
            var referencedObj = (UnityEngine.GameObject)referencedObjects[0];
            Assert.AreEqual(go.GetInstanceID(), referencedObj.GetInstanceID());

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader, referencedObjects);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            using (var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestSharedComponentWithUnityObjectRef>().Build(entityManager))
            {
                var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, entities.Length);

                var a = entityManager.GetSharedComponent<EcsTestSharedComponentWithUnityObjectRef>(entities[0]).value.Value;

                Assert.AreEqual(go.GetInstanceID(), a.GetInstanceID());
            }
        }

        [Test]
        public void SerializeEntities_WithUnityObjRef_SharedComponentData_Default()
        {
            UnityEngine.GameObject go = null;
            {
                Entity a = m_Manager.CreateEntity();
                m_Manager.AddSharedComponent(a, new EcsTestSharedComponentWithUnityObjectRef
                {
                    value = go
                });
            }

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer, out var referencedObjects);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            // Make sure we got no referenced objects
            Assert.AreEqual(0, referencedObjects.Length);

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader, referencedObjects);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            using (var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestSharedComponentWithUnityObjectRef>().Build(entityManager))
            {
                var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, entities.Length);

                var a = entityManager.GetSharedComponent<EcsTestSharedComponentWithUnityObjectRef>(entities[0]).value;

                Assert.IsTrue(a == go);
            }
        }

        [Test]
        public void SerializeEntities_WithUnityObjRef_DynamicBuffer()
        {
            var go1 = new UnityEngine.GameObject("Foo1");
            var go2 = new UnityEngine.GameObject("Foo2");
            {
                Entity a = m_Manager.CreateEntity();
                var buffer = m_Manager.AddBuffer<EcsTestBufferWithUnityObjectRef>(a);
                buffer.Add(new EcsTestBufferWithUnityObjectRef { value = go1 });
                buffer.Add(new EcsTestBufferWithUnityObjectRef { value = go2 });
            }

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer, out var referencedObjects);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            // Make sure we got one object out
            Assert.AreEqual(2, referencedObjects.Length);

            // Make sure it's the same object
            var referencedObj1 = (UnityEngine.GameObject)referencedObjects[0];
            var referencedObj2 = (UnityEngine.GameObject)referencedObjects[1];
            Assert.AreEqual(go1.GetInstanceID(), referencedObj1.GetInstanceID());
            Assert.AreEqual(go2.GetInstanceID(), referencedObj2.GetInstanceID());

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader, referencedObjects);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            using (var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestBufferWithUnityObjectRef>().Build(entityManager))
            {
                var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, entities.Length);

                var buffer = entityManager.GetBuffer<EcsTestBufferWithUnityObjectRef>(entities[0]);
                var outObj1 = buffer[0].value.Value;
                var outObj2 = buffer[1].value.Value;

                Assert.AreEqual(go1.GetInstanceID(), outObj1.GetInstanceID());
                Assert.AreEqual(go2.GetInstanceID(), outObj2.GetInstanceID());
            }
        }

        [Test]
        public void SerializeEntities_WithUnityObjRef_DynamicBuffer_Default()
        {
            UnityEngine.GameObject go1 = null;
            UnityEngine.GameObject go2 = null;
            {
                Entity a = m_Manager.CreateEntity();
                var buffer = m_Manager.AddBuffer<EcsTestBufferWithUnityObjectRef>(a);
                buffer.Add(new EcsTestBufferWithUnityObjectRef { value = go1 });
                buffer.Add(new EcsTestBufferWithUnityObjectRef { value = go2 });
            }


            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer, out var referencedObjects);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            // Make sure we got no referenced objects
            Assert.AreEqual(0, referencedObjects.Length);

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader, referencedObjects);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            using (var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestBufferWithUnityObjectRef>().Build(entityManager))
            {
                var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, entities.Length);

                var buffer = entityManager.GetBuffer<EcsTestBufferWithUnityObjectRef>(entities[0]);
                var outObj1 = buffer[0].value;
                var outObj2 = buffer[1].value;

                Assert.IsTrue(outObj1 == go1);
                Assert.IsTrue(outObj2 == go2);
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void SerializeEntities_WithUnityObjRef_ManagedComponentData()
        {
            var go = new UnityEngine.GameObject("Foo");
            {
                Entity a = m_Manager.CreateEntity();
                m_Manager.AddComponentData(a, new EcsTestManagedComponentWithUnityObjectRef
                {
                    value = go
                });
            }

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer, out var referencedObjects);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            // Make sure we got one object out
            Assert.AreEqual(1, referencedObjects.Length);

            // Make sure it's the same object
            var referencedObj = (UnityEngine.GameObject)referencedObjects[0];
            Assert.AreEqual(go.GetInstanceID(), referencedObj.GetInstanceID());

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader, referencedObjects);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            using (var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestManagedComponentWithUnityObjectRef>().Build(entityManager))
            {
                var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, entities.Length);

                var a = entityManager.GetComponentData<EcsTestManagedComponentWithUnityObjectRef>(entities[0]).value.Value;

                Assert.AreEqual(go.GetInstanceID(), a.GetInstanceID());
            }
        }

        [Test]
        public void SerializeEntities_WithUnityObjRef_ManagedComponentData_Default()
        {
            UnityEngine.GameObject go = null;
            {
                Entity a = m_Manager.CreateEntity();
                m_Manager.AddComponentData(a, new EcsTestManagedComponentWithUnityObjectRef
                {
                    value = go
                });
            }

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer, out var referencedObjects);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            // Make sure we got no referenced objects
            Assert.AreEqual(0, referencedObjects.Length);

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader, referencedObjects);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            using (var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestManagedComponentWithUnityObjectRef>().Build(entityManager))
            {
                var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, entities.Length);

                var a = entityManager.GetComponentData<EcsTestManagedComponentWithUnityObjectRef>(entities[0]).value;

                Assert.IsTrue(a == go);
            }
        }

        [Test]
        public void SerializeEntities_WithUnityObjRef_ManagedSharedComponentData()
        {
            var go = new UnityEngine.GameObject("Foo");
            {
                Entity a = m_Manager.CreateEntity();
                m_Manager.AddSharedComponentManaged(a, new EcsTestManagedSharedComponentWithUnityObjectRef
                {
                    value = go
                });
            }

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer, out var referencedObjects);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            // Make sure we got one object out
            Assert.AreEqual(1, referencedObjects.Length);

            // Make sure it's the same object
            var referencedObj = (UnityEngine.GameObject)referencedObjects[0];
            Assert.AreEqual(go.GetInstanceID(), referencedObj.GetInstanceID());

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader, referencedObjects);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            using (var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestManagedSharedComponentWithUnityObjectRef>().Build(entityManager))
            {
                var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, entities.Length);

                var a = entityManager.GetSharedComponentManaged<EcsTestManagedSharedComponentWithUnityObjectRef>(entities[0]).value.Value;

                Assert.AreEqual(go.GetInstanceID(), a.GetInstanceID());
            }
        }

        [Test]
        public void SerializeEntities_WithUnityObjRef_ManagedSharedComponentData_Default()
        {
            UnityEngine.GameObject go = null;
            {
                Entity a = m_Manager.CreateEntity();
                m_Manager.AddSharedComponentManaged(a, new EcsTestManagedSharedComponentWithUnityObjectRef
                {
                    value = go
                });
            }

            var writer = new TestBinaryWriter(m_Manager.World.UpdateAllocator.ToAllocator);
            SerializeUtility.SerializeWorld(m_Manager, writer, out var referencedObjects);

            m_Manager.DestroyEntity(m_Manager.UniversalQuery);

            // Make sure we got no referenced objects
            Assert.AreEqual(0, referencedObjects.Length);

            using var deserializedWorld = new World("Deserialized World");
            var entityManager = deserializedWorld.EntityManager;

            using (var reader = new TestBinaryReader(writer))
            {
                SerializeUtility.DeserializeWorld(entityManager.BeginExclusiveEntityTransaction(), reader, referencedObjects);
                entityManager.EndExclusiveEntityTransaction();
            }

            entityManager.Debug.CheckInternalConsistency();

            using (var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestManagedSharedComponentWithUnityObjectRef>().Build(entityManager))
            {
                var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, entities.Length);

                var a = entityManager.GetSharedComponentManaged<EcsTestManagedSharedComponentWithUnityObjectRef>(entities[0]).value;

                Assert.IsTrue(a == go);
            }
        }
#endif

    }
}
