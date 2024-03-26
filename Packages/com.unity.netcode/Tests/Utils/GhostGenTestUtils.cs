using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.NetCode.Tests
{
    public static class GhostGenTestUtils
    {
        #region Types
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

        /// <summary>
        /// Clamped ghostfields that can be used in all supported replicated data
        /// They need to be split into multiple values, since there is a max size that commands can have
        /// </summary>
        public struct GhostGenTypesClamp_Values
        {
            [GhostField(Composite=true)] public int3 Composed_Int3;
            [GhostField] public int3 Int3;

            [GhostField(Composite=true)] public uint3 Composed_UInt3;
            [GhostField] public uint3 UInt3;
            [GhostField(Composite=true)] public partialUint3 ComposedPartial_UInt3;
            [GhostField] public partialUint3 Partial_UInt3;

            [GhostField(Quantization=10, Composite=true)] public floatX Composed_FloatX;
            [GhostField] public int IntValue;
            [GhostField] public uint UIntValue;
            [GhostField] public bool BoolValue;

            [GhostField] public long LongValue;
            [GhostField] public ulong ULongValue;

            [GhostField] public float Unquantized_FloatValue;
            [GhostField(Quantization=10)] public float FloatValue;

            [GhostField] public double Unquantized_DoubleValue;
            [GhostField(Quantization=10)] public double DoubleValue;

            [GhostField(Quantization=10)] public float2 Float2Value;
            [GhostField] public float2 Unquantized_Float2Value;

            [GhostField(Quantization=10)] public float3 Float3Value;
            [GhostField] public float3 Unquantized_Float3Value;

            [GhostField(Quantization=10)] public float4 Float4Value;
            [GhostField] public float4 Unquantized_Float4Value;

            [GhostField(Quantization=1000)] public quaternion QuaternionValue;
            [GhostField] public quaternion Unquantized_QuaternionValue;

            [GhostField] public Entity EntityValue;
        }

        public struct GhostGenTypesClamp_Strings
        {
            [GhostField] public FixedString32Bytes String32Value;
            [GhostField] public FixedString64Bytes String64Value;
            [GhostField] public FixedString128Bytes String128Value;
            [GhostField] public FixedString512Bytes String512Value;
            [GhostField] public FixedString4096Bytes String4096Value;
        }

        /// <summary>
        /// Interpolated ghostfields that are not supported for input, but for ghost snapshots
        /// </summary>
        public struct GhostGenTypesInterpolate
        {
            [GhostField(Quantization=10, Smoothing=SmoothingAction.Interpolate)] public floatX FloatX;

            [GhostField(Smoothing=SmoothingAction.Interpolate)] public float Unquantized_Interpolated_FloatValue;
            [GhostField(Quantization=10, Smoothing=SmoothingAction.Interpolate)] public float Interpolated_FloatValue;

            [GhostField(Smoothing=SmoothingAction.Interpolate)] public double Unquantized_Interpolated_DoubleValue;
            [GhostField(Quantization=10, Smoothing=SmoothingAction.Interpolate)] public float Interpolated_DoubleValue;

            [GhostField(Quantization=10, Smoothing=SmoothingAction.Interpolate)] public float2 Interpolated_Float2Value;
            [GhostField(Smoothing=SmoothingAction.Interpolate)] public float2 Interpolated_Unquantized_Float2Value;

            [GhostField(Quantization=10, Smoothing=SmoothingAction.Interpolate)] public float3 Interpolated_Float3Value;
            [GhostField(Smoothing=SmoothingAction.Interpolate)] public float3 Interpolated_Unquantized_Float3Value;

            [GhostField(Quantization=10, Smoothing=SmoothingAction.Interpolate)] public float4 Interpolated_Float4Value;
            [GhostField(Smoothing=SmoothingAction.Interpolate)] public float4 Interpolated_Unquantized_Float4Value;

            [GhostField(Quantization=1000, Smoothing=SmoothingAction.Interpolate)] public quaternion Interpolated_QuaternionValue;
            [GhostField(Smoothing=SmoothingAction.Interpolate)] public quaternion Interpolated_Unquantized_QuaternionValue;
        }

        public struct GhostGenTestType_IComponentData : IComponentData
        {
            [GhostField] public GhostGenTypesClamp_Values GhostGenTypesClamp_Values;
            [GhostField] public GhostGenTypesClamp_Strings GhostGenTypesClamp_Strings;
            [GhostField] public GhostGenTypesInterpolate GhostGenTypesInterpolate;
        }

        public class GhostGenTestTypesConverter_IComponentData : TestNetCodeAuthoring.IConverter
        {
            public void Bake(GameObject gameObject, IBaker baker)
            {
                var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
                baker.AddComponent(entity, new GhostGenTestType_IComponentData());
            }
        }

        public struct GhostGenTestType_ICommandData_Values : ICommandData
        {
            [GhostField] public NetworkTick Tick { get; set; }
            [GhostField] public GhostGenTypesClamp_Values GhostGenTypesClamp_Values;
        }

        public struct GhostGenTestType_ICommandData_Strings : ICommandData
        {
            [GhostField] public NetworkTick Tick { get; set; }
            [GhostField] public GhostGenTypesClamp_Strings GhostGenTypesClamp_Strings;
        }

        // InputEvent is already tested in the specific IInputComponentData tests, so they are not added here.
        public struct GhostGenTestType_IInputComponentData_Values : IInputComponentData
        {
            public GhostGenTestUtils.GhostGenTypesClamp_Values GhostGenTypesClamp_Values;
        }

        public struct GhostGenTestType_IInputComponentData_Strings : IInputComponentData
        {
            public GhostGenTestUtils.GhostGenTypesClamp_Strings GhostGenTypesClamp_Strings;
        }

        public class GhostGenTestTypesConverter_IInputComponentData_Values : TestNetCodeAuthoring.IConverter
        {
            public void Bake(GameObject gameObject, IBaker baker)
            {
                var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
                baker.AddComponent(entity, new GhostGenTestType_IInputComponentData_Values());
            }
        }

        public class GhostGenTestTypesConverter_IInputComponentData_Strings : TestNetCodeAuthoring.IConverter
        {
            public void Bake(GameObject gameObject, IBaker baker)
            {
                var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
                baker.AddComponent(entity, new GhostGenTestType_IInputComponentData_Strings());
            }
        }

        public struct GhostGenTestType_IRpc : IRpcCommand
        {
            public GhostGenTypesClamp_Values GhostGenTypesClamp_Values;
            public GhostGenTypesClamp_Strings GhostGenTypesClamp_Strings;
            public GhostGenTypesInterpolate GhostGenTypesInterpolate;
        }
        #endregion

        #region Verification
        // Verifies the clamped data is correct between server and client.
        // isCommandData is used since some of the ghostfields have DontSend=false
        public static void VerifyGhostValuesClamp_Values(bool hasPartialSupport, GhostGenTypesClamp_Values serverValues, GhostGenTypesClamp_Values clientValues, Entity serverGhost, Entity clientGhost)
        {
            Assert.AreEqual(serverValues.Int3, clientValues.Int3);
            Assert.AreEqual(serverValues.Composed_Int3, clientValues.Composed_Int3);

            Assert.AreEqual(serverValues.UInt3, clientValues.UInt3);
            Assert.AreEqual(serverValues.Composed_UInt3, clientValues.Composed_UInt3);

            Assert.AreEqual(serverValues.Partial_UInt3.x, clientValues.Partial_UInt3.x);
            Assert.AreEqual(serverValues.Partial_UInt3.y, clientValues.Partial_UInt3.y);
            Assert.AreEqual(hasPartialSupport ? serverValues.Partial_UInt3.z : 0, clientValues.Partial_UInt3.z);
            Assert.AreEqual(serverValues.ComposedPartial_UInt3.x, clientValues.ComposedPartial_UInt3.x);
            Assert.AreEqual(serverValues.ComposedPartial_UInt3.y, clientValues.ComposedPartial_UInt3.y);
            Assert.AreEqual(hasPartialSupport ? serverValues.ComposedPartial_UInt3.z : 0, clientValues.ComposedPartial_UInt3.z);
            Assert.AreEqual(serverValues.Composed_FloatX, clientValues.Composed_FloatX);

            Assert.AreEqual(serverValues.IntValue, clientValues.IntValue);
            Assert.AreEqual(serverValues.UIntValue, clientValues.UIntValue);
            Assert.AreEqual(serverValues.BoolValue, clientValues.BoolValue);

            Assert.AreEqual(serverValues.LongValue, clientValues.LongValue);
            Assert.AreEqual(serverValues.ULongValue, clientValues.ULongValue);

            AssertQuantizedDoubleIsWithinTolerance(0.001, serverValues.FloatValue, clientValues.FloatValue);
            Assert.AreEqual(serverValues.Unquantized_FloatValue, clientValues.Unquantized_FloatValue);

            AssertQuantizedDoubleIsWithinTolerance(0.001, serverValues.DoubleValue, clientValues.DoubleValue);
            Assert.AreEqual(serverValues.Unquantized_DoubleValue, clientValues.Unquantized_DoubleValue);

            Assert.AreEqual(serverValues.Float2Value, clientValues.Float2Value);
            Assert.AreEqual(serverValues.Unquantized_Float2Value, clientValues.Unquantized_Float2Value);

            Assert.AreEqual(serverValues.Float3Value, clientValues.Float3Value);
            Assert.AreEqual(serverValues.Unquantized_Float3Value, clientValues.Unquantized_Float3Value);

            Assert.AreEqual(serverValues.Float4Value, clientValues.Float4Value);
            Assert.AreEqual(serverValues.Unquantized_Float4Value, clientValues.Unquantized_Float4Value);

            Assert.Less(math.distance(serverValues.QuaternionValue.value, clientValues.QuaternionValue.value), 0.001f);
            Assert.AreEqual(serverValues.Unquantized_QuaternionValue, clientValues.Unquantized_QuaternionValue);

            Assert.AreEqual(serverGhost, serverValues.EntityValue);
            Assert.AreEqual(clientGhost, clientValues.EntityValue);
        }

        public static void VerifyGhostValuesClamp_Strings(GhostGenTypesClamp_Strings serverValues, GhostGenTypesClamp_Strings clientValues)
        {
            Assert.AreEqual(serverValues.String32Value,clientValues.String32Value);
            Assert.AreEqual(serverValues.String64Value,clientValues.String64Value);
            Assert.AreEqual(serverValues.String128Value,clientValues.String128Value);
            Assert.AreEqual(serverValues.String512Value,clientValues.String512Value);
            Assert.AreEqual(serverValues.String4096Value,clientValues.String4096Value);
        }

        static void AssertQuantizedDoubleIsWithinTolerance(double tolerance, double serverValue, double clientValue)
        {
            var delta = math.abs(serverValue - clientValue);
            Assert.IsTrue(delta < tolerance, $"Quantized value is similar enough:\nServer: {serverValue}\nClient:{clientValue}\ndelta: {delta}");
        }

        public static void VerifyGhostValuesInterpolate(GhostGenTypesInterpolate serverValues, GhostGenTypesInterpolate clientValues)
        {
            Assert.AreEqual(serverValues.FloatX, clientValues.FloatX);

            Assert.AreEqual(serverValues.Interpolated_FloatValue, clientValues.Interpolated_FloatValue);
            Assert.AreEqual(serverValues.Unquantized_Interpolated_FloatValue, clientValues.Unquantized_Interpolated_FloatValue);

            Assert.AreEqual(serverValues.Interpolated_DoubleValue, clientValues.Interpolated_DoubleValue);
            Assert.AreEqual(serverValues.Unquantized_Interpolated_DoubleValue, clientValues.Unquantized_Interpolated_DoubleValue);

            Assert.AreEqual(serverValues.Interpolated_Float2Value, clientValues.Interpolated_Float2Value);
            Assert.AreEqual(serverValues.Interpolated_Unquantized_Float2Value, clientValues.Interpolated_Unquantized_Float2Value);

            Assert.AreEqual(serverValues.Interpolated_Float3Value, clientValues.Interpolated_Float3Value);
            Assert.AreEqual(serverValues.Interpolated_Unquantized_Float3Value, clientValues.Interpolated_Unquantized_Float3Value);

            Assert.AreEqual(serverValues.Interpolated_Float4Value, clientValues.Interpolated_Float4Value);
            Assert.AreEqual(serverValues.Interpolated_Unquantized_Float4Value, clientValues.Interpolated_Unquantized_Float4Value);

            Assert.Less(math.distance(serverValues.Interpolated_QuaternionValue.value, clientValues.Interpolated_QuaternionValue.value), 0.001f);
            Assert.AreEqual(serverValues.Interpolated_Unquantized_QuaternionValue, clientValues.Interpolated_Unquantized_QuaternionValue);
        }

        public static void VerifyICommandData_Values(GhostGenTestType_ICommandData_Values serverValues,
            GhostGenTestType_ICommandData_Values clientValues, Entity serverGhost, Entity clientGhost)
        {
            VerifyGhostValuesClamp_Values(true, serverValues.GhostGenTypesClamp_Values, clientValues.GhostGenTypesClamp_Values, serverGhost, clientGhost);
        }

        public static void VerifyICommandData_Strings(GhostGenTestType_ICommandData_Strings serverValues,
            GhostGenTestType_ICommandData_Strings clientValues, Entity serverGhost, Entity clientGhost)
        {
            VerifyGhostValuesClamp_Strings(serverValues.GhostGenTypesClamp_Strings, clientValues.GhostGenTypesClamp_Strings);
        }

        public static void VerifyIInputComponentData_Values(GhostGenTestType_IInputComponentData_Values serverValues,
            GhostGenTestType_IInputComponentData_Values clientValues, Entity serverGhost, Entity clientGhost)
        {
            VerifyGhostValuesClamp_Values(true, serverValues.GhostGenTypesClamp_Values, clientValues.GhostGenTypesClamp_Values, serverGhost, clientGhost);
        }

        public static void VerifyIInputComponentData_Strings(GhostGenTestType_IInputComponentData_Strings serverValues,
            GhostGenTestType_IInputComponentData_Strings clientValues, Entity serverGhost, Entity clientGhost)
        {
            VerifyGhostValuesClamp_Strings(serverValues.GhostGenTypesClamp_Strings, clientValues.GhostGenTypesClamp_Strings);
        }

        public static void VerifyIRpc(GhostGenTestType_IRpc serverValues, GhostGenTestType_IRpc clientValues,
            Entity serverGhost, Entity clientGhost)
        {
            VerifyGhostValuesInterpolate(serverValues.GhostGenTypesInterpolate, clientValues.GhostGenTypesInterpolate);
            VerifyGhostValuesClamp_Values(true, serverValues.GhostGenTypesClamp_Values, clientValues.GhostGenTypesClamp_Values, serverGhost, clientGhost);
            VerifyGhostValuesClamp_Strings(serverValues.GhostGenTypesClamp_Strings, clientValues.GhostGenTypesClamp_Strings);
        }
        #endregion

        #region Field Generation
        public static GhostGenTypesClamp_Strings CreateTooLargeGhostValuesStrings()
        {
            return new GhostGenTypesClamp_Strings
            {
                String4096Value = @"
String that is so big it explodes the ICommandData max size (of 1024 bytes)!
String that is so big it explodes the ICommandData max size (of 1024 bytes)!
String that is so big it explodes the ICommandData max size (of 1024 bytes)!
String that is so big it explodes the ICommandData max size (of 1024 bytes)!
String that is so big it explodes the ICommandData max size (of 1024 bytes)!
String that is so big it explodes the ICommandData max size (of 1024 bytes)!
String that is so big it explodes the ICommandData max size (of 1024 bytes)!
String that is so big it explodes the ICommandData max size (of 1024 bytes)!
String that is so big it explodes the ICommandData max size (of 1024 bytes)!
String that is so big it explodes the ICommandData max size (of 1024 bytes)!
String that is so big it explodes the ICommandData max size (of 1024 bytes)!
String that is so big it explodes the ICommandData max size (of 1024 bytes)!
String that is so big it explodes the ICommandData max size (of 1024 bytes)!
String that is so big it explodes the ICommandData max size (of 1024 bytes)!
",
            };
        }

        public static GhostGenTypesClamp_Values CreateGhostValuesClamp_Values(int baseValue, Entity ghostEntity)
        {
            int i = 0;
            var values = new GhostGenTypesClamp_Values()
            {
                Int3 = new int3()
                {
                    x = baseValue,
                    y = baseValue + ++i,
                    z = baseValue + ++i
                },
                Composed_Int3 = new int3()
                {
                    x = baseValue + ++i,
                    y = baseValue + ++i,
                    z = baseValue + ++i,
                },
                UInt3 = new uint3()
                {
                    x = (uint)baseValue + (uint)++i,
                    y = (uint)baseValue + (uint)++i,
                    z = (uint)baseValue + (uint)++i
                },
                Composed_UInt3 = new uint3()
                {
                    x = (uint)baseValue + (uint)++i,
                    y = (uint)baseValue + (uint)++i,
                    z = (uint)baseValue + (uint)++i
                },
                Partial_UInt3 = new partialUint3()
                {
                    x = (uint)baseValue + (uint)++i,
                    y = (uint)baseValue + (uint)++i,
                    z = (uint)baseValue + (uint)++i
                },
                ComposedPartial_UInt3 = new partialUint3()
                {
                    x = (uint)baseValue + (uint)++i,
                    y = (uint)baseValue + (uint)++i,
                    z = (uint)baseValue + (uint)++i
                },
                Composed_FloatX = new floatX()
                {
                    x = new float2(baseValue + (uint)++i, baseValue + (uint)++i),
                    y = new float3(baseValue + (uint)++i, baseValue + (uint)++i, baseValue + (uint)++i),
                    z = new float4(baseValue + (uint)++i, baseValue + (uint)++i, baseValue + (uint)++i, baseValue + (uint)++i),
                },
                IntValue = baseValue + ++i,
                UIntValue = (uint)baseValue + (uint)++i,
                BoolValue = (baseValue & ++i) != 0,

                LongValue = baseValue + ++i,
                ULongValue = (ulong)baseValue + (uint)++i,

                FloatValue = baseValue + ++i,
                Unquantized_FloatValue = baseValue + ++i,

                DoubleValue = baseValue + ++i,
                Unquantized_DoubleValue = baseValue + ++i,

                Float2Value = new float2(baseValue + ++i, baseValue + ++i),
                Unquantized_Float2Value = new float2(baseValue + ++i, baseValue + ++i),

                Float3Value = new float3(baseValue + ++i, baseValue + ++i, baseValue + ++i),
                Unquantized_Float3Value = new float3(baseValue + ++i, baseValue + ++i, baseValue + ++i),

                Float4Value = new float4(baseValue + ++i, baseValue + ++i, baseValue + ++i, baseValue + ++i),
                Unquantized_Float4Value = new float4(baseValue + ++i, baseValue + ++i, baseValue + ++i, baseValue + ++i),

                QuaternionValue = math.normalize(new quaternion(0.4f, 0.4f, 0.4f, 0.6f)),
                Unquantized_QuaternionValue = math.normalize(new quaternion(0.6f, 0.6f, 0.6f, 0.6f)),

                EntityValue = ghostEntity,
            };
            UnityEngine.Debug.Log($"i is {i}");
            return values;
        }

        public static GhostGenTypesClamp_Strings CreateGhostValuesClamp_Strings(int baseValue)
        {
            int i = 0;
            var values = new GhostGenTypesClamp_Strings()
            {
                String32Value = new FixedString32Bytes($"bv:{baseValue + ++i}"),
                String64Value = new FixedString64Bytes($"bv:{baseValue + ++i}"),
                String128Value = new FixedString128Bytes($"bv:{baseValue + ++i}"),
                String512Value = new FixedString512Bytes($"bv:{baseValue + ++i}"),
                String4096Value = new FixedString4096Bytes($"bv:{baseValue + ++i}"),
            };
            UnityEngine.Debug.Log($"i is {i}");
            return values;
        }

        public static GhostGenTypesInterpolate CreateGhostValuesInterpolate(int baseValue)
        {
            int i = 0;
            var values = new GhostGenTypesInterpolate
            {
                FloatX = new floatX()
                {
                    x = new float2(baseValue + (uint)++i, baseValue + (uint)++i),
                    y = new float3(baseValue + (uint)++i, baseValue + (uint)++i, baseValue + (uint)++i),
                    z = new float4(baseValue + (uint)++i, baseValue + (uint)++i, baseValue + (uint)++i, baseValue + (uint)++i),
                },

                Interpolated_FloatValue = baseValue + ++i,
                Unquantized_Interpolated_FloatValue = baseValue + ++i,

                Interpolated_DoubleValue = baseValue + ++i,
                Unquantized_Interpolated_DoubleValue = baseValue + ++i,

                Interpolated_Float2Value = new float2(baseValue + ++i, baseValue + ++i),
                Interpolated_Unquantized_Float2Value = new float2(baseValue + ++i, baseValue + ++i),

                Interpolated_Float3Value = new float3(baseValue + ++i, baseValue + ++i, baseValue + ++i),
                Interpolated_Unquantized_Float3Value = new float3(baseValue + ++i, baseValue + ++i, baseValue + ++i),

                Interpolated_Float4Value = new float4(baseValue + ++i, baseValue + ++i, baseValue + ++i, baseValue + ++i),
                Interpolated_Unquantized_Float4Value = new float4(baseValue + ++i, baseValue + ++i, baseValue + ++i, baseValue + ++i),

                Interpolated_QuaternionValue = math.normalize(new quaternion(0.5f, 0.5f, 0.5f, 0.5f)),
                Interpolated_Unquantized_QuaternionValue = math.normalize(new quaternion(0.5f, 0.5f, 0.5f, 0.5f))
            };
            UnityEngine.Debug.Log($"i is {i}");
            return values;
        }

        // The functions below have the specific contract from the test source.
        public static GhostGenTestType_ICommandData_Values CreateICommandDataValues_Values(NetworkTick tick, int baseValue, Entity ghostEntity)
        {
            return new GhostGenTestType_ICommandData_Values()
                { Tick = tick, GhostGenTypesClamp_Values = CreateGhostValuesClamp_Values(baseValue, ghostEntity) };
        }

        public static GhostGenTestType_ICommandData_Strings CreateICommandDataValues_Strings(NetworkTick tick, int baseValue, Entity ghostEntity)
        {
            return new GhostGenTestType_ICommandData_Strings()
                { Tick = tick, GhostGenTypesClamp_Strings = CreateGhostValuesClamp_Strings(baseValue) };
        }

        public static GhostGenTestType_IInputComponentData_Values CreateIInputComponentDataValues_Values(int baseValue, Entity ghostEntity)
        {
            return new GhostGenTestType_IInputComponentData_Values
            {
                GhostGenTypesClamp_Values = CreateGhostValuesClamp_Values(baseValue, ghostEntity)
            };
        }

        public static GhostGenTestType_IInputComponentData_Strings CreateIInputComponentDataValues_Strings(int baseValue, Entity ghostEntity)
        {
            return new GhostGenTestType_IInputComponentData_Strings
            {
                GhostGenTypesClamp_Strings = CreateGhostValuesClamp_Strings(baseValue)
            };
        }

        public static GhostGenTestType_IRpc CreateIRpcValues(int baseValue, Entity ghostEntity)
        {
            return new GhostGenTestType_IRpc
            {
                GhostGenTypesInterpolate = CreateGhostValuesInterpolate(baseValue),
                GhostGenTypesClamp_Strings = CreateGhostValuesClamp_Strings(baseValue),
                GhostGenTypesClamp_Values = CreateGhostValuesClamp_Values(baseValue, ghostEntity)
            };
        }
        #endregion
    }
}
