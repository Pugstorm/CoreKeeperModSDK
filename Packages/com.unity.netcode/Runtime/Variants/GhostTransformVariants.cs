using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Scripting;

namespace Unity.NetCode
{
    /// <summary>
    /// The default serialization strategy for the <see cref="Unity.Transforms.LocalTransform"/> components provided by the NetCode package.
    /// </summary>
    [Preserve]
    [GhostComponentVariation(typeof(Transforms.LocalTransform), "Transform - 3D")]
    [GhostComponent(PrefabType=GhostPrefabType.All, SendTypeOptimization=GhostSendType.AllClients)]
    public struct TransformDefaultVariant
    {
        /// <summary>
        /// The position value is replicated with a default quantization unit of 1000 (so roughly 1mm precision per component).
        /// The replicated position value support both interpolation and extrapolation
        /// </summary>
        [GhostField(Quantization=1000, Smoothing=SmoothingAction.InterpolateAndExtrapolate)]
        public float3 Position;

        /// <summary>
        /// The scale value is replicated with a default quantization unit of 1000.
        /// The replicated scale value support both interpolation and extrapolation
        /// </summary>
        [GhostField(Quantization=1000, Smoothing=SmoothingAction.InterpolateAndExtrapolate)]
        public float Scale;

        /// <summary>
        /// The rotation quaternion is replicated and the resulting floating point data use for replication the rotation is quantized with good precision (10 or more bits per component)
        /// </summary>
        [GhostField(Quantization=1000, Smoothing=SmoothingAction.InterpolateAndExtrapolate)]
        public quaternion Rotation;
    }
    /// <summary>
    /// A serialization strategy for <see cref="Unity.Transforms.LocalTransform"/> that replicates only the entity
    /// <see cref="Unity.Transforms.LocalTransform.Position"/>.
    /// </summary>
    [Preserve]
    [GhostComponentVariation(typeof(Transforms.LocalTransform), "PositionOnly - 3D")]
    [GhostComponent(PrefabType=GhostPrefabType.All, SendTypeOptimization=GhostSendType.AllClients)]
    public struct PositionOnlyVariant
    {
        /// <summary>
        /// The position value is replicated with a default quantization unit of 1000 (so roughly 1mm precision per component).
        /// The replicated position value support both interpolation and extrapolation
        /// </summary>
        [GhostField(Quantization=1000, Smoothing=SmoothingAction.InterpolateAndExtrapolate)]
        public float3 Position;
    }
    /// <summary>
    /// A serialization strategy for <see cref="Unity.Transforms.LocalTransform"/> that replicates only the entity
    /// <see cref="Unity.Transforms.LocalTransform.Rotation"/>.
    /// </summary>
    [Preserve]
    [GhostComponentVariation(typeof(Transforms.LocalTransform), "RotationOnly - 3D")]
    [GhostComponent(PrefabType=GhostPrefabType.All, SendTypeOptimization=GhostSendType.AllClients)]
    public struct RotationOnlyVariant
    {
        /// <summary>
        /// The rotation quaternion is replicated and the resulting floating point data use for replication the rotation is quantized with good precision (10 or more bits per component)
        /// </summary>
        [GhostField(Quantization=1000, Smoothing=SmoothingAction.InterpolateAndExtrapolate)]
        public quaternion Rotation;
    }
    /// <summary>
    /// A serialization strategy that replicates the entity <see cref="Unity.Transforms.LocalTransform.Position"/> and
    /// <see cref="Unity.Transforms.LocalTransform.Rotation"/> properties.
    /// </summary>
    [Preserve]
    [GhostComponentVariation(typeof(Transforms.LocalTransform), "PositionAndRotation - 3D")]
    [GhostComponent(PrefabType=GhostPrefabType.All, SendTypeOptimization=GhostSendType.AllClients)]
    public struct PositionRotationVariant
    {
        /// <summary>
        /// The position value is replicated with a default quantization unit of 1000 (so roughly 1mm precision per component).
        /// The replicated position value support both interpolation and extrapolation
        /// </summary>
        [GhostField(Quantization=1000, Smoothing=SmoothingAction.InterpolateAndExtrapolate)]
        public float3 Position;

        /// <summary>
        /// The position value is replicated with a default quantization unit of 100 (so roughly 1cm precision per component).
        /// The replicated position value support both interpolation and extrapolation
        /// </summary>
        [GhostField(Quantization=1000, Smoothing=SmoothingAction.InterpolateAndExtrapolate)]
        public quaternion Rotation;
    }
    /// <summary>
    /// A serialization strategy that replicates the entity <see cref="Unity.Transforms.LocalTransform.Position"/> and
    /// <see cref="Unity.Transforms.LocalTransform.Scale"/> properties.
    /// </summary>
    [Preserve]
    [GhostComponentVariation(typeof(Transforms.LocalTransform), "PositionScale - 3D")]
    [GhostComponent(PrefabType=GhostPrefabType.All, SendTypeOptimization=GhostSendType.AllClients)]
    public struct PositionScaleVariant
    {
        /// <summary>
        /// The position value is replicated with a default quantization unit of 1000 (so roughly 1mm precision per component).
        /// The replicated position value support both interpolation and extrapolation
        /// </summary>
        [GhostField(Quantization=1000, Smoothing=SmoothingAction.InterpolateAndExtrapolate)]
        public float3 Position;

        /// <summary>
        /// The scale value is replicated with a default quantization unit of 1000, and support both interpolation and exrapolation.
        /// </summary>
        [GhostField(Quantization=1000, Smoothing=SmoothingAction.InterpolateAndExtrapolate)]
        public float Scale;
    }
    /// <summary>
    /// A serialization strategy that replicates the entity <see cref="Unity.Transforms.LocalTransform.Rotation"/> and
    /// <see cref="Unity.Transforms.LocalTransform.Scale"/> properties.
    /// </summary>
    [Preserve]
    [GhostComponentVariation(typeof(Transforms.LocalTransform), "RotationScale - 3D")]
    [GhostComponent(PrefabType=GhostPrefabType.All, SendTypeOptimization=GhostSendType.AllClients)]
    public struct RotationScaleVariant
    {
        /// <summary>
        /// The position value is replicated with a default quantization unit of 1000 (so roughly 1mm precision per component).
        /// The replicated position value support both interpolation and extrapolation
        /// </summary>
        [GhostField(Quantization=1000, Smoothing=SmoothingAction.InterpolateAndExtrapolate)]
        public quaternion Rotation;

        /// <summary>
        /// The scale value is replicated with a default quantization unit of 1000, and support both interpolation and exrapolation.
        /// </summary>
        [GhostField(Quantization=1000, Smoothing=SmoothingAction.InterpolateAndExtrapolate)]
        public float Scale;
    }

    /// <summary>
    /// System that optinally setup the Netcode default variants used for transform components in case a default is not already present.
    /// The following variants are set by default by the package:
    /// - <see cref="Unity.Transforms.LocalTransform"/>
    /// - <see cref="Unity.Transforms.Translation"/>
    /// - <see cref="Unity.Transforms.Rotation"/>
    /// <remarks>
    /// It will never override the default assignment for the transform components if they are already present in the
    /// <see cref="GhostComponentSerializerCollectionData.DefaultVariants"/> map.
    /// <para>Any system deriving from <see cref="DefaultVariantSystemBase"/> will take precendence, even if they are created
    /// after this system.</para>
    /// </remarks>
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.BakingSystem)]
    [CreateAfter(typeof(GhostComponentSerializerCollectionSystemGroup))]
    [UpdateInGroup(typeof(DefaultVariantSystemGroup), OrderLast = true)]
    public sealed partial class TransformDefaultVariantSystem : SystemBase
    {
        protected override void OnCreate()
        {
            var rules = World.GetExistingSystemManaged<GhostComponentSerializerCollectionSystemGroup>().DefaultVariantRules;
            rules.TrySetDefaultVariant(ComponentType.ReadWrite<LocalTransform>(), DefaultVariantSystemBase.Rule.OnlyParents(typeof(TransformDefaultVariant)), this);

            Enabled = false;
        }

        protected override void OnUpdate()
        {
        }
    }
}
