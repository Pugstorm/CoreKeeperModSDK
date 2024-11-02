using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Entities;
using Unity.Entities.Tests;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Burst.Intrinsics;

[assembly: RegisterGenericComponentType(typeof(EcsTestGeneric<int>))]
[assembly: RegisterGenericComponentType(typeof(EcsTestGeneric<float>))]
[assembly: RegisterGenericComponentType(typeof(EcsTestGenericTag<int>))]
[assembly: RegisterGenericComponentType(typeof(EcsTestGenericTag<float>))]

namespace Unity.Entities.Tests
{
    // In case we need a generic way to access the first int of the EcsTestData* structures
    interface IGetValue
    {
        int GetValue();
    }

    internal struct Character : IComponentData
    {
        public Entity Entity;
        public int MovementSpeed;
    }

    internal readonly partial struct CharacterAspect : IAspect
    {
        readonly RefRW<Character> m_Character;
        public ref Character Character => ref m_Character.ValueRW;
    }

    internal struct EcsTestData : IComponentData, IGetValue
    {
        public int value;

        public EcsTestData(int inValue)
        {
            value = inValue;
        }

        public override string ToString()
        {
            return value.ToString();
        }

        public int GetValue() => value;
    }

    internal struct EcsTestData2 : IComponentData, IGetValue
    {
        public int value0;
        public int value1;

        public EcsTestData2(int inValue)
        {
            value1 = value0 = inValue;
        }

        public int GetValue() => value0;
    }

    internal struct EcsTestData3 : IComponentData, IGetValue
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestData3(int inValue)
        {
            value2 = value1 = value0 = inValue;
        }

        public int GetValue() => value0;
    }

    internal struct EcsTestData4 : IComponentData, IGetValue
    {
        public int value0;
        public int value1;
        public int value2;
        public int value3;

        public EcsTestData4(int inValue)
        {
            value3 = value2 = value1 = value0 = inValue;
        }

        public int GetValue() => value0;
    }

    internal struct EcsTestData5 : IComponentData, IGetValue
    {
        public int value0;
        public int value1;
        public int value2;
        public int value3;
        public int value4;

        public EcsTestData5(int inValue)
        {
            value4 = value3 = value2 = value1 = value0 = inValue;
        }

        public int GetValue() => value0;
    }

    internal struct EcsTestData6 : IComponentData { public int value; }
    internal struct EcsTestData7 : IComponentData { public int value; }
    internal struct EcsTestData8 : IComponentData { public int value; }
    internal struct EcsTestData9 : IComponentData { public int value; }
    internal struct EcsTestData10 : IComponentData { public int value; }
    internal struct EcsTestData11 : IComponentData { public int value; }

    internal struct EcsTestDataEnableable : IComponentData, IGetValue, IEnableableComponent
    {
        public int value;

        public EcsTestDataEnableable(int inValue)
        {
            value = inValue;
        }

        public override string ToString()
        {
            return value.ToString();
        }

        public int GetValue() => value;
    }

    internal struct EcsTestDataEnableable2 : IComponentData, IGetValue, IEnableableComponent
    {
        public int value0;
        public int value1;

        public EcsTestDataEnableable2(int inValue)
        {
            value1 = value0 = inValue;
        }

        public int GetValue() => value0;
    }

    internal struct EcsTestDataEnableable3 : IComponentData, IGetValue, IEnableableComponent
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestDataEnableable3(int inValue)
        {
            value2 = value1 = value0 = inValue;
        }

        public int GetValue() => value0;
    }

    internal struct EcsTestDataEnableable4 : IComponentData, IGetValue, IEnableableComponent
    {
        public int value0;
        public int value1;
        public int value2;
        public int value3;

        public EcsTestDataEnableable4(int inValue)
        {
            value3 = value2 = value1 = value0 = inValue;
        }

        public int GetValue() => value0;
    }

    internal struct EcsTestDataEnableable5 : IComponentData, IGetValue, IEnableableComponent
    {
        public int value0;
        public int value1;
        public int value2;
        public int value3;
        public int value4;

        public EcsTestDataEnableable5(int inValue)
        {
            value4 = value3 = value2 = value1 = value0 = inValue;
        }

        public int GetValue() => value0;
    }

    internal struct EcsTestEmptyEnableable1 : IComponentData, IEnableableComponent{}

    internal struct EcsTestEmptyEnableable2 : IComponentData, IEnableableComponent{}

    internal struct EcsTestEnableableBuffer1 : IBufferElementData, IEnableableComponent
    {
        public int Value;
    }
    internal struct EcsTestEnableableBuffer2 : IBufferElementData, IEnableableComponent
    {
        public int Value;
    }

    internal struct EcsTestNonComponent
    {
        public int Value;
    }

    internal struct EcsTestFloatData : IComponentData
    {
        public float Value;
    }

    internal struct EcsTestFloatData2 : IComponentData
    {
        public float Value0;
        public float Value1;
    }

    internal struct EcsTestFloatData3 : IComponentData
    {
        public float Value0;
        public float Value1;
        public float Value2;
    }


    internal struct EcsTestSharedComp : ISharedComponentData
    {
        public int value;

        public EcsTestSharedComp(int inValue)
        {
            value = inValue;
        }
    }

    internal struct EcsTestSharedComp2 : ISharedComponentData
    {
        public int value0;
        public int value1;

        public EcsTestSharedComp2(int inValue)
        {
            value0 = value1 = inValue;
        }
    }

    internal struct EcsTestSharedComp3 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp3(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    // need many shared types for testing that we don't exceed kMaxNumSharedComponentCount
    internal struct EcsTestSharedComp4 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp4(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    internal struct EcsTestSharedComp5 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp5(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    internal struct EcsTestSharedComp6 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp6(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    internal struct EcsTestSharedComp7 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp7(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    internal struct EcsTestSharedComp8 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp8(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    internal struct EcsTestSharedComp9 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp9(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    internal struct EcsTestSharedComp10 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp10(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    internal struct EcsTestSharedComp11 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp11(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    internal struct EcsTestSharedComp12 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp12(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    internal struct EcsTestSharedComp13 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp13(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    internal struct EcsTestSharedComp14 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp14(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    internal struct EcsTestSharedComp15 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp15(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    internal struct EcsTestSharedComp16 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp16(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    internal struct EcsTestSharedComp17 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp17(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    internal struct EcsTestSharedCompManaged : ISharedComponentData, IEquatable<EcsTestSharedCompManaged>
    {
        public string value;
        public EcsTestSharedCompManaged(string inValue) => value = inValue;
        public bool Equals(EcsTestSharedCompManaged other) => value == other.value;
        public override int GetHashCode() => value.GetHashCode();
    }

    internal struct EcsTestSharedCompManaged2 : ISharedComponentData, IEquatable<EcsTestSharedCompManaged2>
    {
        public string value0;
        public string value1;
        public EcsTestSharedCompManaged2(string inValue) => value0 = value1 = inValue;
        public bool Equals(EcsTestSharedCompManaged2 other) => value0 == other.value0 && value1 == other.value1;
        public override int GetHashCode() => value0.GetHashCode() ^ value1.GetHashCode();
    }

    internal struct EcsTestSharedCompManaged3 : ISharedComponentData, IEquatable<EcsTestSharedCompManaged3>
    {
        public string value0;
        public string value1;
        public string value2;
        public EcsTestSharedCompManaged3(string inValue) => value0 = value1 = value2 = inValue;
        public bool Equals(EcsTestSharedCompManaged3 other) => value0 == other.value0 && value1 == other.value1 && value2 == other.value2;
        public override int GetHashCode() => value0.GetHashCode() ^ value1.GetHashCode() ^ value2.GetHashCode();
    }

    internal struct EcsTestSharedCompManagedEntity : ISharedComponentData, IEquatable<EcsTestSharedCompManagedEntity>
    {
        public Entity Value;
        public string ManagedValue;

        public EcsTestSharedCompManagedEntity(Entity entity, string stringValue)
        {
            Value = entity;
            ManagedValue = stringValue;
        }

        public bool Equals(EcsTestSharedCompManagedEntity other)
        {
            return Value == other.Value && ManagedValue == other.ManagedValue;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Value, ManagedValue);
        }
    }

    [MaximumChunkCapacity(127)]
    struct EcsTestSharedCompWithMaxChunkCapacity : ISharedComponentData
    {
        public int Value;

        public EcsTestSharedCompWithMaxChunkCapacity(int value)
        {
            Value = value;
        }
    }

    [ChunkSerializable]
    internal unsafe struct EcsTestSharedCompWithRefCount : ISharedComponentData, IRefCounted
    {
        readonly int* RefCount;

        public EcsTestSharedCompWithRefCount(int* refCount)
        {
            Assert.IsTrue(refCount != null);
            this.RefCount = refCount;
        }

        public void Retain()
        {
            Assert.IsTrue(RefCount != null);
            Interlocked.Increment(ref *RefCount);
        }

        public void Release()
        {
            Assert.IsTrue(RefCount != null);
            Interlocked.Decrement(ref *RefCount);
        }
    }

    internal struct EcsTestDataEntity : IComponentData
    {
        public int value0;
        public Entity value1;

        public EcsTestDataEntity(int inValue0, Entity inValue1)
        {
            value0 = inValue0;
            value1 = inValue1;
        }
    }

    internal struct EcsTestDataEntity2 : IComponentData
    {
        public int value0;
        public Entity value1;
        public Entity value2;
    }

    internal struct EcsTestDataBlobAssetRef : IComponentData
    {
        public BlobAssetReference<int> value;
    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    internal class EcsTestDataBlobAssetRefClass : IComponentData
    {
        public BlobAssetReference<int> value;
        public BlobAssetReference<int> value2;
    }
#endif

    internal struct EcsTestDataBlobAssetRefShared : ISharedComponentData
    {
        public BlobAssetReference<int> value;
        public BlobAssetReference<int> value2;
    }

    internal struct EcsTestDataBlobAssetRef2 : IComponentData
    {
        public BlobAssetReference<int> value;
        public BlobAssetReference<int> value2;
    }

    internal struct EcsTestDataBlobAssetArray : IComponentData
    {
        public BlobAssetReference<BlobArray<float>> array;
    }

    internal struct EcsTestDataBlobAssetElement : IBufferElementData
    {
        public BlobAssetReference<int> blobElement;
    }

    internal struct EcsTestDataBlobAssetElement2 : IBufferElementData
    {
        public BlobAssetReference<int> blobElement;
        byte pad;
        public BlobAssetReference<int> blobElement2;
    }

    internal struct EcsTestSharedCompEntity : ISharedComponentData
    {
        public Entity value;

        public EcsTestSharedCompEntity(Entity inValue)
        {
            value = inValue;
        }
    }

    internal struct EcsCleanup1 : ICleanupComponentData
    {
        public int Value;

        public EcsCleanup1(int value)
        {
            Value = value;
        }
    }

    internal struct EcsCleanupShared1 : ICleanupSharedComponentData
    {
        public int Value;

        public EcsCleanupShared1(int value)
        {
            Value = value;
        }
    }

    internal struct EcsCleanupTag1 : ICleanupComponentData
    {
    }

    [InternalBufferCapacity(8)]
    internal struct EcsIntElement : IBufferElementData
    {
        public static implicit operator int(EcsIntElement e)
        {
            return e.Value;
        }

        public static implicit operator EcsIntElement(int e)
        {
            return new EcsIntElement {Value = e};
        }

        public int Value;
    }

    [InternalBufferCapacity(8)]
    internal struct EcsIntElement2 : IBufferElementData
    {
        public int Value0;
        public int Value1;
    }

    [InternalBufferCapacity(8)]
    internal struct EcsIntElement3 : IBufferElementData
    {
        public int Value0;
        public int Value1;
        public int Value2;
    }

    [InternalBufferCapacity(8)]
    internal struct EcsIntElement4 : IBufferElementData
    {
        public int Value0;
        public int Value1;
        public int Value2;
        public int Value3;
    }

    [InternalBufferCapacity(8)]
    internal struct EcsIntElementEnableable : IBufferElementData, IEnableableComponent
    {
        public static implicit operator int(EcsIntElementEnableable e)
        {
            return e.Value;
        }

        public static implicit operator EcsIntElementEnableable(int e)
        {
            return new EcsIntElementEnableable {Value = e};
        }

        public int Value;
    }

    [InternalBufferCapacity(8)]
    internal struct EcsIntElementEnableable2 : IBufferElementData, IEnableableComponent
    {
        public int Value0;
        public int Value1;
    }

    [InternalBufferCapacity(8)]
    internal struct EcsIntElementEnableable3 : IBufferElementData, IEnableableComponent
    {
        public int Value0;
        public int Value1;
        public int Value2;
    }

    [InternalBufferCapacity(8)]
    internal struct EcsIntElementEnableable4 : IBufferElementData, IEnableableComponent
    {
        public int Value0;
        public int Value1;
        public int Value2;
        public int Value3;
    }

    [InternalBufferCapacity(8)]
    internal struct EcsIntCleanupElement : ICleanupBufferElementData
    {
        public static implicit operator int(EcsIntCleanupElement e)
        {
            return e.Value;
        }

        public static implicit operator EcsIntCleanupElement(int e)
        {
            return new EcsIntCleanupElement {Value = e};
        }

        public int Value;
    }

    [InternalBufferCapacity(4)]
    internal struct EcsComplexEntityRefElement : IBufferElementData
    {
        public int Dummy;
        public Entity Entity;
    }

    internal struct EcsTestTag : IComponentData
    {
    }

    internal struct AnotherEcsTestTag : IComponentData
    {
    }

    internal struct EcsTestTagEnableable : IComponentData, IEnableableComponent
    {
    }

    internal struct EcsTestTagEnableable2 : IComponentData, IEnableableComponent
    {
    }

    internal struct EcsTestSharedTag : ISharedComponentData
    {
        public int Value;
    }

    internal struct EcsTestComponentWithBool : IComponentData, IEquatable<EcsTestComponentWithBool>
    {
        public bool value;

        public override int GetHashCode()
        {
            return value ? 0x11001100 : 0x22112211;
        }

        public bool Equals(EcsTestComponentWithBool other)
        {
            return other.value == value;
        }
    }

    internal struct EcsStringSharedComponent : ISharedComponentData, IEquatable<EcsStringSharedComponent>
    {
        public string Value;

        public bool Equals(EcsStringSharedComponent other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    internal struct EcsTestGeneric<T> : IComponentData
        where T : struct
    {
        public T value;
    }

    internal struct EcsTestGenericTag<T> : IComponentData
        where T : struct
    {
    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS

    internal sealed class ClassWithString
    {
        public string String;
    }
    internal sealed class ClassWithClassFields
    {
        public ClassWithString ClassWithString;
    }

    internal class EcsTestManagedDataEntity : IComponentData
    {
        public string value0;
        public Entity value1;
        public int value2;
        public ClassWithClassFields nullField;

        public EcsTestManagedDataEntity()
        {
        }

        public EcsTestManagedDataEntity(string inValue0, Entity inValue1, int inValue2 = 0)
        {
            value0 = inValue0;
            value1 = inValue1;
            value2 = inValue2;
            nullField = null;
        }
    }

    internal class EcsTestManagedDataEntityCollection : IComponentData
    {
        public List<string> value0;
        public List<Entity> value1;
        public List<ClassWithClassFields> nullField;

        public EcsTestManagedDataEntityCollection()
        {
        }

        public EcsTestManagedDataEntityCollection(string[] inValue0, Entity[] inValue1)
        {
            value0 = new List<string>(inValue0);
            value1 = new List<Entity>(inValue1);
            nullField = null;
        }
    }

    internal class EcsTestManagedComponent : IComponentData
    {
        public string value;
        public ClassWithClassFields nullField;

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public bool Equals(EcsTestManagedComponent other)
        {
            return value == other.value;
        }
    }

    internal class EcsTestManagedComponent2 : EcsTestManagedComponent
    {
        public string value2;
    }

    internal class EcsTestManagedComponent3 : EcsTestManagedComponent2
    {
        public string value3;
    }

    internal class EcsTestManagedComponent4 : EcsTestManagedComponent3
    {
        public string value4;
    }

    class EcsTestDisposableManagedComponent : IComponentData, IDisposable
    {
        public string Value;
        public void Dispose()
        {
            Value = "disposed";
        }
    }

    internal unsafe class EcsTestManagedCompWithRefCount : IComponentData, ICloneable, IDisposable
    {
        public int RefCount;

        public EcsTestManagedCompWithRefCount()
        {
            RefCount = 1;
        }

        public object Clone()
        {
            Interlocked.Increment(ref RefCount);
            return this;
        }

        public void Dispose()
        {
            Interlocked.Decrement(ref RefCount);
        }
    }

    internal class EcsTestManagedComponentEnableable : IComponentData, IEnableableComponent
    {
        public string value;
        public ClassWithClassFields nullField;

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public bool Equals(EcsTestManagedComponentEnableable other)
        {
            return value == other.value;
        }
    }

    internal class EcsTestManagedComponentEnableable2 : EcsTestManagedComponentEnableable
    {
        public string value2;
    }

#endif

    internal struct EcsTestContainerData : IComponentData
    {
        public NativeArray<int> data;
        public void Create()
        {
            data = new NativeArray<int>(8, Allocator.Persistent);
            data[0] = 73;
            data[1] = 443322;
        }
        public void Destroy() => data.Dispose();
    }

    internal struct EcsTestContainerElement : IBufferElementData
    {
        public NativeArray<int> data;
        public void Create()
        {
            data = new NativeArray<int>(8, Allocator.Persistent);
            data[0] = 73;
            data[1] = 443322;
        }
        public void Destroy() => data.Dispose();
    }

    internal struct EcsTestContainerSharedComp : ISharedComponentData
    {
        public NativeArray<int> data;
        public void Create()
        {
            data = new NativeArray<int>(8, Allocator.Persistent);
            data[0] = 73;
            data[1] = 443322;
        }
        public void Destroy() => data.Dispose();
    }

    internal partial struct EcsTestUpdateOneComponentJob : IJobEntity
    {
        public void Execute(ref EcsTestData ecsTestData)
        {
            ecsTestData.value++;
        }
    }

    internal partial struct EcsTestUpdateTwoComponentsJob : IJobEntity
    {
        public void Execute(ref EcsTestData ecsTestData, ref EcsTestData2 ecsTestData2)
        {
            ecsTestData.value++;
            ecsTestData2.value0++;
        }
    }

    internal partial struct EcsTestUpdateThreeComponentsJob : IJobEntity
    {
        public void Execute(ref EcsTestData ecsTestData, ref EcsTestData2 ecsTestData2, ref EcsTestData3 ecsTestData3)
        {
            ecsTestData.value++;
            ecsTestData2.value0++;
            ecsTestData3.value0++;
        }
    }

    internal partial struct EcsTestUpdateOneComponentWithValuesFromOtherComponentsJob : IJobEntity
    {
        public void Execute(ref EcsTestData ecsTestData, in EcsTestData2 ecsTestData2, in EcsTestData3 ecsTestData3)
        {
            ecsTestData.value = ecsTestData2.value0 * ecsTestData3.value0 +
                                ecsTestData2.value1 * ecsTestData3.value1 +
                                ecsTestData3.value2;
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Default, CompileSynchronously = true)]
    internal partial struct EcsTestSetComponentValueTo10 : IJobEntity
    {
        public void Execute(ref EcsTestData ecsTestData)
        {
            ecsTestData = new EcsTestData {value = 10};
        }
    }

    internal partial struct EcsTestSetFirstComponentValueTo10 : IJobEntity
    {
        public void Execute(ref EcsTestData ecsTestData, ref EcsTestData2 ecsTestData2)
        {
            ecsTestData = new EcsTestData {value = 10};
        }
    }

    internal partial struct EcsTestSetFirstComponentValueTo10_WithSharedComponent : IJobEntity
    {
        public void Execute(ref EcsTestData ecsTestData, ref EcsTestData2 ecsTestData2)
        {
            ecsTestData = new EcsTestData {value = 10};
        }
    }

    internal readonly partial struct EcsTestAspect0RO : IAspect
    {
        public readonly RefRO<EcsTestData> EcsTestData;
    }

    internal readonly partial struct EcsTestAspect0RW : IAspect
    {
        public readonly RefRW<EcsTestData> EcsTestData;
    }
}
