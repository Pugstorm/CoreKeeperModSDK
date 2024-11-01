using UnityEngine;
using NUnit.Framework;
using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Entities;
using Unity.Entities.Tests;
using Unity.Collections.LowLevel.Unsafe;
using Assert = NUnit.Framework.Assert;
using Unity.Mathematics;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Entities.Serialization;
using Unity.Entities.LowLevel.Unsafe;
using Unity.IO.LowLevel.Unsafe;

public partial class BlobTests : ECSTestsFixture
{
    BlobTestSystemEFE _blobTestSystemEFE => World.CreateSystemManaged<BlobTestSystemEFE>();
    partial class BlobTestSystemEFE : SystemBase
    {
        public JobHandle ValidateBlobData_Job(JobHandle inputDependency = default)
        {
            return Entities.ForEach((ref ComponentWithBlobData data) =>
            {
                ValidateBlobData(ref data.blobAsset.Value);
                data.blobAsset.Dispose();
                data.DidSucceed = true;
            }).Schedule(inputDependency);
        }
        protected override void OnUpdate() {}
    }

    BlobTestSystemIJobEntity _blobTestSystemIJobEntity => World.CreateSystemManaged<BlobTestSystemIJobEntity>();
    partial class BlobTestSystemIJobEntity : SystemBase
    {
        public JobHandle ValidateBlobInComponent(bool expectException = false, JobHandle inputDependency = default)
        {
            var job = new ValidateBlobInComponentJob { ExpectException = expectException };
            var jobHandle = job.Schedule(inputDependency);
            return jobHandle;
        }

        public partial struct ValidateBlobInComponentJob : IJobEntity
        {
            public bool ExpectException;
            public unsafe void Execute(ref ComponentWithBlobData componentWithBlobData)
            {
                if (ExpectException)
                {
                    var blobAsset = componentWithBlobData.blobAsset;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                    Assert.Throws<InvalidOperationException>(() => { blobAsset.GetUnsafePtr(); });
#endif
                }
                else
                {
                    ValidateBlobData(ref componentWithBlobData.blobAsset.Value);
                }
                componentWithBlobData.DidSucceed = true;
            }
        }
        protected override void OnUpdate() {}
    }
    //@TODO: Test Prevent NativeArray and other containers inside of Blob data
    //@TODO: Test Prevent BlobPtr, BlobArray onto job struct
    //@TODO: Various tests trying to break the Allocator. eg. mix multiple BlobAllocator in the same BlobRoot...

    struct MyData
    {
        public BlobArray<float> floatArray;
        public BlobPtr<float> nullPtr;
        public BlobPtr<float3> oneVector3;
        public float embeddedFloat;
        public BlobArray<BlobArray<int>> nestedArray;
        public BlobString str;
        public BlobString emptyStr;
    }

    static unsafe BlobBuilder ConstructBlobBuilder()
    {
        var builder = new BlobBuilder(Allocator.Temp);

        ref var root = ref builder.ConstructRoot<MyData>();

        var floatArray = builder.Allocate(ref root.floatArray, 3);
        ref float3 oneVector3 = ref builder.Allocate(ref root.oneVector3);
        var nestedArrays = builder.Allocate(ref root.nestedArray, 2);

        var nestedArray0 = builder.Allocate(ref nestedArrays[0], 1);
        var nestedArray1 = builder.Allocate(ref nestedArrays[1], 2);

        builder.AllocateString(ref root.str, "Blah");
        builder.AllocateString(ref root.emptyStr, "");

        nestedArray0[0] = 0;
        nestedArray1[0] = 1;
        nestedArray1[1] = 2;

        floatArray[0] = 0;
        floatArray[1] = 1;
        floatArray[2] = 2;

        root.embeddedFloat = 4;
        oneVector3 = new float3(3, 3, 3);

        return builder;
    }

    static unsafe BlobAssetReference<MyData> ConstructBlobData()
    {
        var builder = ConstructBlobBuilder();
        var blobAsset = builder.CreateBlobAssetReference<MyData>(Allocator.Persistent);
        builder.Dispose();

        return blobAsset;
    }

    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    static void ValidateBlobData(ref MyData root)
    {
        // not using Assert.AreEqual here because the asserts have to execute in burst jobs

        if (3 != root.floatArray.Length)
            throw new AssertionException("ValidateBlobData didn't match");
        if (0 != root.floatArray[0])
            throw new AssertionException("ValidateBlobData didn't match");
        if (1 != root.floatArray[1])
            throw new AssertionException("ValidateBlobData didn't match");
        if (2 != root.floatArray[2])
            throw new AssertionException("ValidateBlobData didn't match");
        if (!new float3(3, 3, 3).Equals(root.oneVector3.Value))
            throw new AssertionException("ValidateBlobData didn't match");

        if (4 != root.embeddedFloat)
            throw new AssertionException("ValidateBlobData didn't match");

        if (1 != root.nestedArray[0].Length)
            throw new AssertionException("ValidateBlobData didn't match");
        if (2 != root.nestedArray[1].Length)
            throw new AssertionException("ValidateBlobData didn't match");
        if (0 != root.nestedArray[0][0])
            throw new AssertionException("ValidateBlobData didn't match");
        if (1 != root.nestedArray[1][0])
            throw new AssertionException("ValidateBlobData didn't match");
        if (2 != root.nestedArray[1][1])
            throw new AssertionException("ValidateBlobData didn't match");
        ValidateBlobString(ref root);
    }

    [BurstDiscard]
    static void ValidateBlobString(ref MyData root)
    {
        var str = root.str.ToString();
        if ("Blah" != str)
            throw new AssertionException("ValidateBlobData didn't match");
        var emptyStr = root.emptyStr.ToString();
        if ("" != emptyStr)
            throw new AssertionException("ValidateBlobData didn't match");
    }

    static void ValidateBlobDataBurst(ref MyData root)
    {
        Assert.AreEqual(3, root.floatArray.Length);
        Assert.AreEqual(0, root.floatArray[0]);
        Assert.AreEqual(1, root.floatArray[1]);
        Assert.AreEqual(2, root.floatArray[2]);
        Assert.AreEqual(new float3(3, 3, 3), root.oneVector3.Value);
        Assert.AreEqual(4, root.embeddedFloat);

        Assert.AreEqual(1, root.nestedArray[0].Length);
        Assert.AreEqual(2, root.nestedArray[1].Length);

        Assert.AreEqual(0, root.nestedArray[0][0]);
        Assert.AreEqual(1, root.nestedArray[1][0]);
        Assert.AreEqual(2, root.nestedArray[1][1]);
    }

    [Test]
    public void BlobNullComparison()
    {
        BlobAssetReference<int> defaultBlobAssetReference = default;
        Assert.AreEqual(defaultBlobAssetReference.GetHashCode(), BlobAssetReference<int>.Null.GetHashCode());
        Assert.AreEqual(defaultBlobAssetReference, BlobAssetReference<int>.Null);
    }

    [Test]
    public unsafe void CreateBlobData()
    {
        var blob = ConstructBlobData();
        ValidateBlobData(ref blob.Value);

        blob.Dispose();
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks("Test requires data validation checks")]
    public unsafe void BlobAccessAfterReleaseThrows()
    {
        var blob = ConstructBlobData();
        var blobCopy = blob;
        blob.Dispose();

        Assert.Throws<InvalidOperationException>(() => { blobCopy.GetUnsafePtr(); });
        Assert.IsTrue(blob.GetUnsafePtr() == null);

        Assert.Throws<InvalidOperationException>(() => { var p = blobCopy.Value.embeddedFloat; });
        Assert.Throws<InvalidOperationException>(() => { var p = blobCopy.Value.embeddedFloat; });

        Assert.Throws<InvalidOperationException>(() => { blobCopy.Dispose(); });
        Assert.Throws<NullReferenceException>(() => { blob.Dispose(); });
    }

    struct ComponentWithBlobData : IComponentData
    {
        public bool DidSucceed;
        public BlobAssetReference<MyData> blobAsset;
    }

    // Cannot be compiled by Burst because of the call to `builder.AllocateString` in `ConstructBlobBuilder`.
    //[BurstCompile(CompileSynchronously = true)]
    struct ConstructAccessAndDisposeBlobData : IJob
    {
        public void Execute()
        {
            var blobData = ConstructBlobData();
            ValidateBlobData(ref blobData.Value);
            blobData.Dispose();
        }
    }

    [Test]
    public void BurstedConstructionAndAccess()
    {
        new ConstructAccessAndDisposeBlobData().Schedule().Complete();
    }

    [Test]
    public  void ReadAndDestroyBlobDataFromBurstJob()
    {
        var entities = CreateUniqueBlob();

        _blobTestSystemEFE.ValidateBlobData_Job().Complete();

        foreach (var e in entities)
        {
            Assert.IsTrue(m_Manager.GetComponentData<ComponentWithBlobData>(e).DidSucceed);
            Assert.IsFalse(m_Manager.GetComponentData<ComponentWithBlobData>(e).blobAsset.IsCreated);
        }
    }

    [Test]
    public unsafe void ParallelBlobAccessFromEntityJob()
    {
        var blob = CreateSharedBlob();
        var handle = _blobTestSystemIJobEntity.ValidateBlobInComponent();

        ValidateBlobData(ref blob.Value);

        handle.Complete();

        blob.Dispose();
    }

    [Test]
    public void DestroyedBlobAccessFromEntityJobThrows()
    {
        var blob = CreateSharedBlob();
        blob.Dispose();
        var handle = _blobTestSystemIJobEntity.ValidateBlobInComponent(expectException: true);
        handle.Complete();
    }

    [Test]
    public void BlobAssetReferenceIsComparable()
    {
        var blob1 = ConstructBlobData();
        var blob2 = ConstructBlobData();
        var blobNull = new BlobAssetReference<MyData>();

        var temp1 = blob1;

        Assert.IsTrue(blob1 != blob2);
        Assert.IsTrue(blob1 != BlobAssetReference<MyData>.Null);
        Assert.IsTrue(blobNull == BlobAssetReference<MyData>.Null);
        Assert.IsTrue(blob1 == temp1);
        Assert.IsTrue(blob2 != temp1);

        blob1.Dispose();
        blob2.Dispose();
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks("Test requires data validation checks")]
    public void SourceBlobArrayThrowsOnIndex()
    {
        var builder = new BlobBuilder(Allocator.Temp);

        Assert.Throws<IndexOutOfRangeException>(() =>
        {
            //can't access ref variable if it's created outside of the lambda
            ref var root = ref builder.ConstructRoot<MyData>();
            builder.Allocate(ref root.floatArray, 3);

            // Throw on access expected here
            root.floatArray[0] = 7;
        });

        builder.Dispose();
    }

    [Test]
    public void BlobArrayToArrayCopiesResults()
    {
        var blob = ConstructBlobData();
        ref MyData root = ref blob.Value;

        var floatArray = root.floatArray.ToArray();
        Assert.AreEqual(new float[] { 0, 1, 2 }, floatArray);

        blob.Dispose();
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks("Test requires data validation checks")]
    public void SourceBlobPtrThrowsOnDereference()
    {
        var builder = new BlobBuilder(Allocator.Temp);

        Assert.Throws<InvalidOperationException>(() =>
        {
            //can't access ref variable if it's created outside of the lambda
            ref var root = ref builder.ConstructRoot<MyData>();
            builder.Allocate(ref root.oneVector3);

            // Throw on access expected here
            root.oneVector3.Value = float3.zero;
        });

        builder.Dispose();
    }

    struct AlignmentTest
    {
        public BlobPtr<short> shortPointer;
        public BlobPtr<int> intPointer;
        public BlobPtr<byte> bytePointer;
        public BlobArray<int> intArray;
    }

    static unsafe void AssertAlignment(void* p, int alignment)
    {
        ulong mask = (ulong)alignment - 1;
        Assert.IsTrue(((ulong)(IntPtr)p & mask) == 0);
    }

    [Test]
    public unsafe void BasicAlignmentWorks()
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<BlobArray<AlignmentTest>>();
        Assert.AreEqual(4, UnsafeUtility.AlignOf<int>());

        const int count = 100;
        var topLevelArray = builder.Allocate(ref root, count);
        for (int x = 0; x < count; ++x)
        {
            builder.Allocate(ref topLevelArray[x].shortPointer);
            builder.Allocate(ref topLevelArray[x].intPointer);
            builder.Allocate(ref topLevelArray[x].bytePointer);
            builder.Allocate(ref topLevelArray[x].intArray, x + 1);
        }

        var blob = builder.CreateBlobAssetReference<BlobArray<AlignmentTest>>(Allocator.Temp);
        builder.Dispose();

        for (int x = 0; x < count; ++x)
        {
            AssertAlignment(blob.Value[x].shortPointer.GetUnsafePtr(), 2);
            AssertAlignment(blob.Value[x].intPointer.GetUnsafePtr(), 4);
            AssertAlignment(blob.Value[x].intArray.GetUnsafePtr(), 4);
        }

        blob.Dispose();
    }

    struct AlignmentTest2
    {
        public BlobArray<byte> byteArray;
        public BlobArray<int> intArray;
    }

    [Test]
    public unsafe void AllocationWithOverriddenAlignmentWorks([Values(2, 4, 8, 16)] int alignment)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<BlobArray<AlignmentTest2>>();
        var rootArray = builder.Allocate(ref root, 7);

        for (int i = 0; i < 7; ++i)
        {
            builder.Allocate(ref rootArray[i].byteArray, 19+i, alignment);
        }

        var blob = builder.CreateBlobAssetReference<BlobArray<AlignmentTest2>>(Allocator.Temp);
        builder.Dispose();

        for (int i = 0; i < 7; ++i)
        {
            AssertAlignment(blob.Value[i].byteArray.GetUnsafePtr(), alignment);
        }

        blob.Dispose();
    }

    [Test]
    public unsafe void AlignmentWorksWithAllocationLargerThanChunkSize()
    {
        var builder = new BlobBuilder(Allocator.Temp, 4096);
        ref var root = ref builder.ConstructRoot<AlignmentTest2>();
        Assert.AreEqual(4, UnsafeUtility.AlignOf<int>());

        builder.Allocate(ref root.byteArray, 1);
        builder.Allocate(ref root.intArray, 2000);

        var blob = builder.CreateBlobAssetReference<AlignmentTest2>(Allocator.Temp);
        builder.Dispose();

        AssertAlignment(blob.Value.intArray.GetUnsafePtr(), 4);

        blob.Dispose();
    }

    [Test]
    public unsafe void UnalignedChunkSize_Doesnt_Break_Alignment()
    {
        var builder = new BlobBuilder(Allocator.Temp, 17);
        ref var root = ref builder.ConstructRoot<AlignmentTest2>();
        Assert.AreEqual(4, UnsafeUtility.AlignOf<int>());

        builder.Allocate(ref root.byteArray, 1);
        var intArray = builder.Allocate(ref root.intArray, 2000);

        var blob = builder.CreateBlobAssetReference<AlignmentTest2>(Allocator.Temp);
        builder.Dispose();

        AssertAlignment(blob.Value.intArray.GetUnsafePtr(), 4);

        blob.Dispose();
    }

    [Test]
    public unsafe void CreatedBlobsAre16ByteAligned()
    {
        using var blobAssetReference = BlobAssetReference<int>.Create(42);
        AssertAlignment(blobAssetReference.GetUnsafePtr(), 16);
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks("Test requires data validation checks")]
    public void BlobBuilderArrayThrowsOnOutOfBoundsIndex()
    {
        using (var builder = new BlobBuilder(Allocator.Temp, 128))
        {
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                ref var root = ref builder.ConstructRoot<BlobArray<int>>();
                var array = builder.Allocate(ref root, 100);
                array[100] = 7;
            });
        }
    }

    [Test]
    public void AllocationsLargerThanChunkSizeWorks()
    {
        var builder = new BlobBuilder(Allocator.Temp, 128);
        ref var root = ref builder.ConstructRoot<BlobArray<int>>();
        const int count = 100;
        var array = builder.Allocate(ref root, count);
        for (int i = 0; i < count; i++)
            array[i] = i;

        var blob = builder.CreateBlobAssetReference<BlobArray<int>>(Allocator.Temp);
        builder.Dispose();

        for (int i = 0; i < count; i++)
            Assert.AreEqual(i, blob.Value[i]);

        blob.Dispose();
    }

    [Test]
    public void CreatingLargeBlobAssetWorks()
    {
        var builder = new BlobBuilder(Allocator.Temp, 512);
        ref var root = ref builder.ConstructRoot<BlobArray<BlobArray<BlobArray<BlobPtr<int>>>>>();

        const int topLevelCount = 100;

        int expectedValue = 42;
        var level0 = builder.Allocate(ref root, topLevelCount);
        for (int x = 0; x < topLevelCount; x++)
        {
            var level1 = builder.Allocate(ref level0[x], x + 1);
            for (int y = 0; y < x + 1; y++)
            {
                var level2 = builder.Allocate(ref level1[y], y + 1);
                for (int z = 0; z < y + 1; z++)
                {
                    ref var i = ref builder.Allocate(ref level2[z]);
                    i = expectedValue++;
                }
            }
        }

        var blob = builder.CreateBlobAssetReference<BlobArray<BlobArray<BlobArray<BlobPtr<int>>>>>(Allocator.Temp);
        builder.Dispose();

        expectedValue = 42;
        for (int x = 0; x < topLevelCount; x++)
        {
            for (int y = 0; y < x + 1; y++)
            {
                for (int z = 0; z < y + 1; z++)
                {
                    int value = blob.Value[x][y][z].Value;

                    if (expectedValue != value)
                        Assert.AreEqual(expectedValue, value);
                    expectedValue++;
                }
            }
        }

        blob.Dispose();
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    public struct TestStruct256bytes
    {
        [FieldOffset(0)] public BlobArray<int> intArray;
        [FieldOffset(252)] public BlobPtr<int> intPointer;
    }

    [Test]
    public void BlobAssetWithRootLargerThanChunkSizeWorks()
    {
        Assert.AreEqual(256, UnsafeUtility.SizeOf<TestStruct256bytes>());
        var builder = new BlobBuilder(Allocator.Temp, 128);
        ref var root = ref builder.ConstructRoot<TestStruct256bytes>();

        var array = builder.Allocate(ref root.intArray, 100);
        for (int i = 0; i < array.Length; ++i)
        {
            array[i] = i;
        }

        builder.Allocate(ref root.intPointer);

        var blob = builder.CreateBlobAssetReference<TestStruct256bytes>(Allocator.Temp);
        builder.Dispose();

        for (int i = 0; i < blob.Value.intArray.Length; ++i)
        {
            if (i != blob.Value.intArray[i])
                Assert.AreEqual(i, blob.Value.intArray[i]);
        }

        blob.Dispose();
    }

    BlobAssetReference<MyData> CreateSharedBlob()
    {
        var blob = ConstructBlobData();

        for (int i = 0; i != 32; i++)
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddComponentData(entity, new ComponentWithBlobData() {blobAsset = blob});
        }
        return blob;
    }

    NativeArray<Entity> CreateUniqueBlob()
    {
        var entities = new NativeArray<Entity>(32, Allocator.Temp);
        for (int i = 0; i != entities.Length; i++)
        {
            entities[i] = m_Manager.CreateEntity();
            m_Manager.AddComponentData(entities[i], new ComponentWithBlobData() {blobAsset = ConstructBlobData()});
        }

        return entities;
    }

    const int kVersion = 51;
    const int kIncorrectVersion = 13;

#if !UNITY_GAMECORE // (disabled as gamecore has permission issues DOTS-7038)
    [Test]
    // Unstable on PS4: https://jira.unity3d.com/browse/DOTS-7693
    [UnityEngine.TestTools.UnityPlatform(exclude = new [] { RuntimePlatform.PS4 })]
    public void BlobAssetReferenceTryRead()
    {
        string fileName = "BlobAssetReferenceIOTestData.blob";
        string writePath = Path.Combine(Application.persistentDataPath, fileName);

        if (File.Exists(writePath))
            File.Delete(writePath);
        try
        {
            var blobBuilder = ConstructBlobBuilder();
            BlobAssetReference<MyData>.Write(blobBuilder, writePath, kVersion);
            using(var reader = new StreamBinaryReader(writePath))
                Assert.IsFalse(BlobAssetReferenceTryReadVersion(reader, kIncorrectVersion));
            using(var reader = new StreamBinaryReader(writePath))
                Assert.IsTrue(BlobAssetReferenceTryReadVersion(reader, kVersion));
        }
        finally
        {
            if (File.Exists(writePath))
                File.Delete(writePath);
        }
    }
#endif

    bool BlobAssetReferenceTryReadVersion<T>(T reader, int version) where T : Unity.Entities.Serialization.BinaryReader
    {
        var result = BlobAssetReference<MyData>.TryRead(reader, version, out var blobResult);
        if(result == false)
            return false;
        ValidateBlobData(ref blobResult.Value);
        blobResult.Dispose();
        return true;
    }

    [Test]
    public void BlobAssetReferenceTryReadNoFile()
    {
        unsafe
        {
            var writer = new MemoryBinaryWriter();
            var blobBuilder = ConstructBlobBuilder();
            BlobAssetReference<MyData>.Write(writer, blobBuilder, kVersion);
            Assert.IsFalse(BlobAssetReferenceTryReadVersion(new MemoryBinaryReader(writer.Data, writer.Length), kIncorrectVersion));
            Assert.IsTrue(BlobAssetReferenceTryReadVersion(new MemoryBinaryReader(writer.Data, writer.Length), kVersion));
        }
    }

    public struct Node
    {
        BlobPtr<byte> _parent;
        BlobArray<byte> _children;
        public int id;

        // Core CLR can't handle cyclic references.
        // https://github.com/dotnet/runtime/issues/5479
        // Thus we do this workaround for blob ptr
        unsafe public ref BlobPtr<Node> parent
        {
            get
            {
                return ref UnsafeUtility.AsRef<BlobPtr<Node>>(UnsafeUtility.AddressOf(ref _parent));
            }
        }

        unsafe public ref BlobArray<Node> children
        {
           get
            {
                return ref UnsafeUtility.AsRef<BlobArray<Node>>(UnsafeUtility.AddressOf(ref _children));
            }
        }
    }

    [Test]
    public void BlobSetPointer()
    {
        BlobBuilder builder = new BlobBuilder(Allocator.Temp);
        {
            ref var rootNode = ref builder.ConstructRoot<Node>();

            rootNode.id = 0;

            var children = builder.Allocate(ref rootNode.children, 2);

            builder.SetPointer(ref children[0].parent, ref rootNode);
            children[0].id = 1;
            builder.SetPointer(ref children[1].parent, ref rootNode);
            children[1].id = 2;

            var grandChildren = builder.Allocate(ref children[1].children, 1);

            builder.SetPointer(ref grandChildren[0].parent, ref children[1]);
            grandChildren[0].id = 3;
        }

        var blob = builder.CreateBlobAssetReference<Node>(Allocator.Temp);

        builder.Dispose();

        {
            ref var rootNode = ref blob.Value;

            ref var child0 = ref rootNode.children[0];
            ref var child1 = ref rootNode.children[1];

            ref var grandChild = ref child1.children[0];

            Assert.AreEqual(0, rootNode.id);
            Assert.AreEqual(1, child0.id);
            Assert.AreEqual(2, child1.id);
            Assert.AreEqual(3, grandChild.id);

            Assert.IsFalse(rootNode.parent.IsValid);
            Assert.AreEqual(0, child0.parent.Value.id);
            Assert.AreEqual(0, child1.parent.Value.id);
            Assert.AreEqual(2, grandChild.parent.Value.id);
        }

        blob.Dispose();
    }

    [Test]
    unsafe public void UnsafeUntypedBlobCasting()
    {
        var blobData = ConstructBlobData();

        var untyped = UnsafeUntypedBlobAssetReference.Create(blobData);

        var reinterpreted = untyped.Reinterpret<MyData>();

        ValidateBlobData(ref reinterpreted.Value);
        Assert.IsTrue(reinterpreted.GetUnsafePtr() == blobData.GetUnsafePtr());
        Assert.IsTrue(UnsafeUtility.MemCmp(UnsafeUtility.AddressOf(ref blobData), UnsafeUtility.AddressOf(ref untyped), UnsafeUtility.SizeOf<UnsafeUntypedBlobAssetReference>()) == 0);
        Assert.IsTrue(UnsafeUtility.MemCmp(UnsafeUtility.AddressOf(ref blobData), UnsafeUtility.AddressOf(ref reinterpreted), UnsafeUtility.SizeOf<UnsafeUntypedBlobAssetReference>()) == 0);
        Assert.AreEqual(UnsafeUtility.SizeOf<UnsafeUntypedBlobAssetReference>(), UnsafeUtility.SizeOf<BlobAssetReference<MyData>>());

        blobData.Dispose();
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks("Requires blob safety checks")]
    public void BlobArray_WithNestedArrays_ToArray_ThrowsInvalidOperationException()
    {
        var blobBuilder = new BlobBuilder(Allocator.Temp);
        ref var root = ref blobBuilder.ConstructRoot<BlobArray<BlobString>>();
        var arrayBuilder = blobBuilder.Allocate(ref root, 2);
        blobBuilder.AllocateString(ref arrayBuilder[0], "hello");
        blobBuilder.AllocateString(ref arrayBuilder[1], "world");
        using var blobRef = blobBuilder.CreateBlobAssetReference<BlobArray<BlobString>>(Allocator.Persistent);

        Assert.Throws<InvalidOperationException>(() => blobRef.Value.ToArray(), "No exception was thrown when creating array copy from BlobArray<BlobString>.");
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks("Requires blob safety checks")]
    public void BlobArray_WithNestedBlobPointers_ToArray_ThrowsInvalidOperationException()
    {
        var blobBuilder = new BlobBuilder(Allocator.Temp);
        ref var root = ref blobBuilder.ConstructRoot<BlobArray<BlobPtr<float3>>>();
        var arrayBuilder = blobBuilder.Allocate(ref root, 2);

        ref float3 elem1 = ref blobBuilder.Allocate(ref arrayBuilder[0]);
        elem1 = new float3(1, 2, 3);
        ref float3 elem2 = ref blobBuilder.Allocate(ref arrayBuilder[1]);
        elem2 = new float3(9, 7, 8);
        using var blobRef = blobBuilder.CreateBlobAssetReference<BlobArray<BlobPtr<float3>>>(Allocator.Persistent);

        Assert.Throws<InvalidOperationException>(() => blobRef.Value.ToArray(), "No exception was thrown when creating array copy from BlobArray<BlobPtr<>>.");
    }

    // Not running on DOTS runtime because the temp allocator isn't predictably generating contiguous allocations
    // and only contiguous allocations can trigger the issue that this regression test is guarding against
    [Test]
    public void BlobBuilder_RegressionTestDOTS7887()
    {
        // note that the size of the internal chunks used by the builder is set to 64 bytes
        // nothing special with 64 but there's no reason to use large chunks (default is 64k)
        var blobBuilder = new BlobBuilder(Allocator.Temp, 64);

        // constructing the root creates the first chunk (1st allocation)
        ref var root = ref blobBuilder.ConstructRoot<BlobArray<BlobArray<BlobString>>>();

        // an array is 8 bytes (4 bytes offset + 4 bytes length)
        // adding an array of 7 elements after the root array gets us to exactly 64 bytes
        var arrayOfArraysBuilder = blobBuilder.Allocate(ref root, 7);

        // next we allocate a very large array (strings are arrays)
        // because it's larger than a chunk it requires its own allocation (2nd allocation)
        // because it's larger than what the temp allocator can provide, it goes to the fallback allocator
        // the size of the array is 128 MB to ensure a fallback on every platform (cf. kBlockSizeMax)
        blobBuilder.Allocate(ref arrayOfArraysBuilder[0], 16 * 1024 * 1024);

        // finally we allocate a small array (that fits a chunk)
        // this creates a 2nd chunk (3rd allocation)
        var secondStringArrayBuilder = blobBuilder.Allocate(ref arrayOfArraysBuilder[1], 8);

        unsafe
        {
            // ensure that the two small chunks are consecutive
            // this is the expected behaviour when using the temp allocator
            // the fallback allocation is in a different address range
            var alloc1 = (IntPtr)arrayOfArraysBuilder.GetUnsafePtr() - 8;
            var alloc3 = (IntPtr)secondStringArrayBuilder.GetUnsafePtr();

            // if this assert fails, it doesn't mean there's anything wrong with the code
            // but that the test is not testing the right thing and needs to be updated
            Assert.AreEqual(alloc1 + 64, alloc3);
        }

        // adding a string as the first element of the second array
        // this string registers a patch instruction to update the array offset which
        // is located at the very beginning of the second chunk (3rd allocation)
        blobBuilder.AllocateString(ref secondStringArrayBuilder[0], "Hello World");

        // creating the final blob, which concatenates the three allocations in the order they've been created
        // but the patches are processed in address order, since the 2nd allocation is in a different range
        // the allocations will be handled in the order 1st, 3rd, 2nd OR 2nd, 1st, 3rd but NOT 1st, 2nd, 3rd
        using var blobRef = blobBuilder.CreateBlobAssetReference<BlobArray<BlobArray<BlobString>>>(Allocator.Persistent);

        // because of an off by one error, the array offset for the string was handled as if it was just past the
        // end of the first chunk. But because the 2nd allocation is between the two chunks in the blob,
        // the string offset would end up being patched at the beginning of the large array instead.
        Assert.AreEqual("Hello World", blobRef.Value[1][0].ToString());

        // This error would not happen without the large allocation. Because when two chunks have consecutive
        // addresses, they usually end up after each other in the finalized blob, causing the off by one error
        // to be completely harmless.
    }
}
