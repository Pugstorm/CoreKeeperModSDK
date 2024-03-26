namespace Unity.NetCode.Generators
{
    /// <summary>
    /// Contains the serialization rule declarations for all the basic/default types supported by netcode.
    /// In particular
    /// Primitive type:
    ///   - int
    ///   - uint
    ///   - byte
    ///   - sbyte
    ///   - float
    ///   - long
    ///   - ulong
    ///   - enum
    /// Mathematics:
    ///   - float2
    ///   - float3
    ///   - float4
    ///   - quaternion
    /// Fixed strings (32,64,128, 512,4096)
    /// Entity reference
    /// </summary>
    class DefaultTypes
    {
        public static readonly TypeRegistryEntry[] Registry = new[]
        {
            new TypeRegistryEntry
            {
                Type = "System.Int32",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueInt.cs"
            },
            new TypeRegistryEntry
            {
                Type = "System.UInt32",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueUInt.cs"
            },

            new TypeRegistryEntry
            {
                Type = "System.Int64",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueInt.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueLong.cs"
            },
            new TypeRegistryEntry
            {
                Type = "System.UInt64",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueUInt.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueULong.cs"
            },

            new TypeRegistryEntry
            {
                Type = "System.Int16",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueInt.cs"
            },
            new TypeRegistryEntry
            {
                Type = "System.UInt16",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueUInt.cs"
            },
            new TypeRegistryEntry
            {
                Type = "System.SByte",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueInt.cs"
            },
            new TypeRegistryEntry
            {
                Type = "System.Byte",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueUInt.cs"
            },
            new TypeRegistryEntry
            {
                Type = "System.Boolean",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueUInt.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueBool.cs"
            },
            new TypeRegistryEntry
            {
                Type = "System.Single",
                Quantized = true,
                Smoothing = SmoothingAction.Interpolate,
                SupportCommand = false,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueFloat.cs"
            },
            new TypeRegistryEntry
            {
                Type = "System.Single",
                Quantized = true,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = false,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueFloat.cs"
            },
            new TypeRegistryEntry
            {
                Type = "System.Single",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueFloatUnquantized.cs"
            },
            new TypeRegistryEntry
            {
                Type = "System.Single",
                Quantized = false,
                Smoothing = SmoothingAction.Interpolate,
                SupportCommand = false,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueFloatUnquantized.cs"
            },
            new TypeRegistryEntry
            {
                Type = "System.Double",
                Quantized = true,
                Smoothing = SmoothingAction.Interpolate,
                SupportCommand = false,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueDouble.cs"
            },
            new TypeRegistryEntry
            {
                Type = "System.Double",
                Quantized = true,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = false,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueDouble.cs"
            },
            new TypeRegistryEntry
            {
                Type = "System.Double",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueDoubleUnquantized.cs"
            },
            new TypeRegistryEntry
            {
                Type = "System.Double",
                Quantized = false,
                Smoothing = SmoothingAction.Interpolate,
                SupportCommand = false,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueDoubleUnquantized.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.float2",
                Quantized = true,
                Smoothing = SmoothingAction.Interpolate,
                SupportCommand = false,
                Composite = true,
                Template = "NetCode.GhostSnapshotValueFloat.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFloat2.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.float2",
                Quantized = true,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = false,
                Composite = true,
                Template = "NetCode.GhostSnapshotValueFloat.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFloat2.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.float2",
                Quantized = false,
                Smoothing = SmoothingAction.Interpolate,
                SupportCommand = false,
                Composite = true,
                Template = "NetCode.GhostSnapshotValueFloatUnquantized.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFloat2Unquantized.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.float2",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = true,
                Template = "NetCode.GhostSnapshotValueFloatUnquantized.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFloat2Unquantized.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.float3",
                Quantized = true,
                Smoothing = SmoothingAction.Interpolate,
                SupportCommand = false,
                Composite = true,
                Template = "NetCode.GhostSnapshotValueFloat.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFloat3.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.float3",
                Quantized = true,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = false,
                Composite = true,
                Template = "NetCode.GhostSnapshotValueFloat.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFloat3.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.float3",
                Quantized = false,
                Smoothing = SmoothingAction.Interpolate,
                SupportCommand = false,
                Composite = true,
                Template = "NetCode.GhostSnapshotValueFloatUnquantized.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFloat3Unquantized.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.float3",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = true,
                Template = "NetCode.GhostSnapshotValueFloatUnquantized.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFloat3Unquantized.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.float4",
                Quantized = true,
                Smoothing = SmoothingAction.Interpolate,
                SupportCommand = false,
                Composite = true,
                Template = "NetCode.GhostSnapshotValueFloat.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFloat4.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.float4",
                Quantized = true,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = false,
                Composite = true,
                Template = "NetCode.GhostSnapshotValueFloat.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFloat4.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.float4",
                Quantized = false,
                Smoothing = SmoothingAction.Interpolate,
                SupportCommand = false,
                Composite = true,
                Template = "NetCode.GhostSnapshotValueFloatUnquantized.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFloat4Unquantized.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.float4",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = true,
                Template = "NetCode.GhostSnapshotValueFloatUnquantized.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFloat4Unquantized.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.quaternion",
                Quantized = true,
                Smoothing = SmoothingAction.Interpolate,
                SupportCommand = false,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueQuaternion.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.quaternion",
                Quantized = true,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = false,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueQuaternion.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.quaternion",
                Quantized = false,
                Smoothing = SmoothingAction.Interpolate,
                SupportCommand = false,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueQuaternionUnquantized.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Mathematics.quaternion",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueQuaternionUnquantized.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Entities.Entity",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueEntity.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Collections.FixedString32Bytes",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueFixedString32Bytes.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Collections.FixedString64Bytes",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Template = "NetCode.GhostSnapshotValueFixedString32Bytes.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFixedString64Bytes.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Collections.FixedString128Bytes",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueFixedString32Bytes.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFixedString128Bytes.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Collections.FixedString512Bytes",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueFixedString32Bytes.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFixedString512Bytes.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.Collections.FixedString4096Bytes",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueFixedString32Bytes.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueFixedString4096Bytes.cs"
            },
            new TypeRegistryEntry
            {
                Type = "Unity.NetCode.NetworkTick",
                Quantized = false,
                Smoothing = SmoothingAction.Clamp,
                SupportCommand = true,
                Composite = false,
                Template = "NetCode.GhostSnapshotValueUInt.cs",
                TemplateOverride = "NetCode.GhostSnapshotValueNetworkTick.cs"
            },
        };
    }
}
