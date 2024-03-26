namespace Unity.NetCode.GeneratorTests
{
    internal static class TestDataSource
    {
        public const string TestComponentsData = @"
using Unity.NetCode;
using Unity.Entities;
using Unity.Mathematics;

public enum GlobalEnum8 : byte
{
    Value0,
    Value1
}
public enum GlobalEnum16 : short
{
    Value0,
    Value1
}
public enum GlobalEnum32 : int
{
    Value0,
    Value1
}
public enum GlobalEnum64 : long
{
    Value0,
    Value1
}

public struct PrimitiveTypeTest : IComponentData
{
    [GhostField] public GlobalEnum8 EnumValue8;
    [GhostField] public GlobalEnum16 EnumValue16;
    [GhostField] public GlobalEnum32 EnumValue32;
    [GhostField] public GlobalEnum64 EnumValue64;
    [GhostField] public int IntValue;
    [GhostField] public uint UIntValue;
    [GhostField] public long LongValue;
    [GhostField] public ulong ULongValue;
    [GhostField] public short ShortValue;
    [GhostField] public ushort UShortValue;
    [GhostField] public sbyte SByteValue;
    [GhostField] public byte ByteValue;
    [GhostField] public bool BoolValue;
    [GhostField] public float FloatValue;
    [GhostField(Smoothing=SmoothingAction.Interpolate)] public float InterpolatedFloat;
    [GhostField(Quantization = 10)] public float QuantizedFloat;
    [GhostField(Quantization = 10, Smoothing=SmoothingAction.Interpolate)] public float InterpolatedQuantizedFloat;
}";

        public static string AllComponentsTypesData = @"
using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;

namespace Unity.NetCode { public struct NetworkTick { } }

public struct MyTest : IComponentData
{
    [GhostField] public int IntValue;
}
public struct MyRpcType : IRpcCommand
{
    public int IntValue;
}

public struct MyCommandType : ICommandData
{
    public Unity.NetCode.NetworkTick Tick { get; set; }
    public byte up;
    public byte down;
    public byte left;
    public byte right;
    public byte button;
}";

        public const string MathematicsTestData = @"
using Unity.NetCode;
using Unity.Entities;
using Unity.Mathematics;

public struct MathTest : IComponentData
{
    [GhostField]public float2 Float2Value;
    [GhostField]public float3 Float3Value;
    [GhostField]public float4 Float4Value;
    [GhostField]public quaternion QuaternionValue;
    [GhostField(Smoothing=SmoothingAction.Interpolate)] public float2 iFloat2Value;
    [GhostField(Smoothing=SmoothingAction.Interpolate)] public float3 iFloat3Value;
    [GhostField(Smoothing=SmoothingAction.Interpolate)] public float4 iFloat4Value;
    [GhostField(Smoothing=SmoothingAction.Interpolate)] public quaternion iQuaternionValue;

    [GhostField(Quantization = 10)] public float2 qFloat2Value;
    [GhostField(Quantization = 10)] public float3 qFloat3Value;
    [GhostField(Quantization = 10)] public float4 qFloat4Value;
    [GhostField(Quantization = 10)] public quaternion qQuaternionValue;
    [GhostField(Quantization = 10, Smoothing=SmoothingAction.Interpolate)] public float2 iqFloat2Value;
    [GhostField(Quantization = 10, Smoothing=SmoothingAction.Interpolate)] public float3 iqFloat3Value;
    [GhostField(Quantization = 10, Smoothing=SmoothingAction.Interpolate)] public float4 iqFloat4Value;
    [GhostField(Quantization = 10, Smoothing=SmoothingAction.Interpolate)] public quaternion iqQuaternionValue;
}";

        public static string FlatTypeTest = @"
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.NetCode;

public struct int3
{
    public int x;
    public int y;
    public int z;
}

public struct uint3
{
    public uint x;
    public uint y;
    public uint z;
}
public struct partialUint3
{
    public uint x;
    public uint y;
    [GhostField(SendData = false)] public uint z;
}
public struct floatX
{
    public float2 x;
    public float3 y;
    public float4 z;
}
public struct FlatType : IComponentData
{
    [GhostField(Composite=true)] public int3 Composed_Int3;
    [GhostField] public int3 Int3;

    [GhostField(Composite=true)] public uint3 Composed_UInt3;
    [GhostField] public uint3 UInt3;
    [GhostField(Composite=true)] public partialUint3 ComposedPartial_UInt3;
    [GhostField] public partialUint3 Partial_UInt3;

    [GhostField(Quantization = 10, Composite=true)] public floatX Composed_FloatX;
    [GhostField(Quantization = 10, Smoothing=SmoothingAction.Interpolate)] public floatX FloatX;
    [GhostField] public int IntValue;
    [GhostField] public uint UIntValue;
    [GhostField] public bool BoolValue;

    [GhostField] public float Unquantized_FloatValue;
    [GhostField(Smoothing=SmoothingAction.Interpolate)] public float Unquantized_Interpolated_FloatValue;
    [GhostField(Quantization=10)] public float FloatValue;
    [GhostField(Quantization=10, Smoothing=SmoothingAction.Interpolate)] public float Interpolated_FloatValue;

    [GhostField(Quantization=10)] public float2 Float2Value;
    [GhostField(Quantization=10, Smoothing=SmoothingAction.Interpolate)] public float2 Interpolated_Float2Value;
    [GhostField] public float2 Unquantized_Float2Value;
    [GhostField(Smoothing=SmoothingAction.Interpolate)] public float2 Interpolated_Unquantized_Float2Value;

    [GhostField(Quantization=10)] public float3 Float3Value;
    [GhostField(Quantization=10, Smoothing=SmoothingAction.Interpolate)] public float3 Interpolated_Float3Value;
    [GhostField] public float3 Unquantized_Float3Value;
    [GhostField(Smoothing=SmoothingAction.Interpolate)] public float3 Interpolated_Unquantized_Float3Value;

    [GhostField(Quantization=10)] public float4 Float4Value;
    [GhostField(Quantization=10, Smoothing=SmoothingAction.Interpolate)] public float4 Interpolated_Float4Value;
    [GhostField] public float4 Unquantized_Float4Value;
    [GhostField(Smoothing=SmoothingAction.Interpolate)] public float4 Interpolated_Unquantized_Float4Value;

    [GhostField(Quantization=1000)] public quaternion QuaternionValue;
    [GhostField(Quantization=1000, Smoothing=SmoothingAction.Interpolate)] public quaternion Interpolated_QuaternionValue;
    [GhostField] public quaternion Unquantized_QuaternionValue;
    [GhostField(Smoothing=SmoothingAction.Interpolate)] public quaternion Interpolated_Unquantized_QuaternionValue;

    [GhostField] public FixedString32Bytes String32Value;
    [GhostField] public FixedString64Bytes String64Value;
    [GhostField] public FixedString128Bytes String128Value;
    [GhostField] public FixedString512Bytes String512Value;
    [GhostField] public FixedString4096Bytes String4096Value;
    [GhostField] public Entity EntityValue;
}";

        public static readonly string CustomTemplate = @"
#region __GHOST_IMPORTS__
#endregion

#region __GHOST_FIELD__
public int __GHOST_FIELD_NAME__X;
public int __GHOST_FIELD_NAME__Y;
#endregion

#region __GHOST_PREDICT__
snapshot.__GHOST_FIELD_NAME__X = predictor.PredictInt(snapshot.__GHOST_FIELD_NAME__X, baseline1.__GHOST_FIELD_NAME__X, baseline2.__GHOST_FIELD_NAME__X);
snapshot.__GHOST_FIELD_NAME__Y = predictor.PredictInt(snapshot.__GHOST_FIELD_NAME__Y, baseline1.__GHOST_FIELD_NAME__Y, baseline2.__GHOST_FIELD_NAME__Y);
#endregion

#region __GHOST_WRITE__
if ((changeMask & (1 << __GHOST_MASK_INDEX__)) != 0)
{
    writer.WritePackedIntDelta(snapshot.__GHOST_FIELD_NAME__X, baseline.__GHOST_FIELD_NAME__X, compressionModel);
    writer.WritePackedIntDelta(snapshot.__GHOST_FIELD_NAME__Y, baseline.__GHOST_FIELD_NAME__Y, compressionModel);
}
#endregion

#region __GHOST_READ__
if ((changeMask & (1 << __GHOST_MASK_INDEX__)) != 0)
{
    snapshot.__GHOST_FIELD_NAME__X = reader.ReadPackedIntDelta(baseline.__GHOST_FIELD_NAME__X, compressionModel);
    snapshot.__GHOST_FIELD_NAME__Y = reader.ReadPackedIntDelta(baseline.__GHOST_FIELD_NAME__Y, compressionModel);
}
else
{
    snapshot.__GHOST_FIELD_NAME__X = baseline.__GHOST_FIELD_NAME__X;
    snapshot.__GHOST_FIELD_NAME__Y = baseline.__GHOST_FIELD_NAME__Y;
}
#endregion

#region __GHOST_COPY_TO_SNAPSHOT__
snapshot.__GHOST_FIELD_NAME__X = (int)(component.__GHOST_FIELD_REFERENCE__.x * __GHOST_QUANTIZE_SCALE__);
snapshot.__GHOST_FIELD_NAME__Y = (int)(component.__GHOST_FIELD_REFERENCE__.y * __GHOST_QUANTIZE_SCALE__);
#endregion


#region __GHOST_COPY_FROM_SNAPSHOT__
component.Value = new float3(snapshotBefore.__GHOST_FIELD_NAME__X * __GHOST_DEQUANTIZE_SCALE__, snapshotBefore.__GHOST_FIELD_NAME__Y * __GHOST_DEQUANTIZE_SCALE__, 0.0f);
#endregion

#region __GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE__
component.__GHOST_FIELD_REFERENCE__ = math.lerp(
    new float3(snapshotBefore.__GHOST_FIELD_NAME__X * __GHOST_DEQUANTIZE_SCALE__, snapshotBefore.__GHOST_FIELD_NAME__Y * __GHOST_DEQUANTIZE_SCALE__, 0.0f),
    new float3(snapshotAfter.__GHOST_FIELD_NAME__X * __GHOST_DEQUANTIZE_SCALE__, snapshotAfter.__GHOST_FIELD_NAME__Y * __GHOST_DEQUANTIZE_SCALE__, 0.0f),
    snapshotInterpolationFactor);
#endregion

 #region __GHOST_RESTORE_FROM_BACKUP__
component.__GHOST_FIELD_REFERENCE__ = backup.__GHOST_FIELD_REFERENCE__;
#endregion

#region __GHOST_CALCULATE_CHANGE_MASK_ZERO__
changeMask = (snapshot.__GHOST_FIELD_NAME__X != baseline.__GHOST_FIELD_NAME__X ||
            snapshot.__GHOST_FIELD_NAME__Y != baseline.__GHOST_FIELD_NAME__Y)
            ? 1u : 0;
#endregion
#region __GHOST_CALCULATE_CHANGE_MASK__
changeMask |= (snapshot.__GHOST_FIELD_NAME__X != baseline.__GHOST_FIELD_NAME__X ||
            snapshot.__GHOST_FIELD_NAME__Y != baseline.__GHOST_FIELD_NAME__Y)
            ? (1u<<__GHOST_MASK_INDEX__) : 0;
#endregion

#region __GHOST_REPORT_PREDICTION_ERROR__
errors[errorIndex] = math.max(errors[errorIndex], math.distance(component.__GHOST_FIELD_REFERENCE__, backup.__GHOST_FIELD_REFERENCE__));
++errorIndex;
#endregion

#region __GHOST_GET_PREDICTION_ERROR_NAME__
if (nameCount != 0)
    names.Append(new FixedString32Bytes("",""));
    names.Append(new FixedString64Bytes(""__GHOST_FIELD_REFERENCE__""));
    ++nameCount;
#endregion
";
    }
}

