using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;

namespace Unity.NetCode
{
    /// <summary>
    /// Default serialization variant for the PhysicsVelocity. Necessary to synchronize physics
    /// </summary>
    [GhostComponentVariation(typeof(PhysicsVelocity), nameof(PhysicsVelocity))]
    [GhostComponent(PrefabType = GhostPrefabType.All, SendTypeOptimization = GhostSendType.OnlyPredictedClients)]
    public struct PhysicsVelocityDefaultVariant
    {
        /// <summary>
        /// The rigid body linear velocity in world space. Measured in m/s.
        /// </summary>
        [GhostField(Quantization = 1000)] public float3 Linear;
        /// <summary>
        /// The body angular velocity in world space. Measured in radiant/s
        /// </summary>
        [GhostField(Quantization = 1000)] public float3 Angular;
    }


    /// <summary>
    /// Default serialization variant for the PhysicsGraphicalSmoothing which disables smoothing on interpolated clients.
    /// Ghost are controled by the server rather than physics on interpolated clients, which makes the physics smoothing incorrect.
    /// </summary>
    [GhostComponentVariation(typeof(PhysicsGraphicalSmoothing), nameof(PhysicsGraphicalSmoothing))]
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PhysicsGraphicalSmoothingDefaultVariant
    {
    }

    /// <summary>
    /// Optionally register the default variant to use for the <see cref="Unity.Physics.PhysicsVelocity"/> and the
    /// <see cref="Unity.Physics.GraphicsIntegration.PhysicsGraphicalSmoothing"/>.
    /// <remarks>
    /// It will never override the default assignment for the `PhysicsVelocity` nor the `PhysicsGraphicalSmoothing` components
    /// if they are already present in the <see cref="GhostComponentSerializerCollectionData.DefaultVariants"/> map.
    /// <para>Any system deriving from <see cref="DefaultVariantSystemBase"/> will take precendence, even if they are created
    /// after this system.</para>
    /// </remarks>
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.BakingSystem)]
    [CreateAfter(typeof(GhostComponentSerializerCollectionSystemGroup))]
    [UpdateInGroup(typeof(DefaultVariantSystemGroup), OrderLast = true)]
    public sealed partial class PhysicsDefaultVariantSystem : SystemBase
    {
        protected override void OnCreate()
        {
            var rules = World.GetExistingSystemManaged<GhostComponentSerializerCollectionSystemGroup>().DefaultVariantRules;
            rules.TrySetDefaultVariant(ComponentType.ReadWrite<PhysicsVelocity>(), DefaultVariantSystemBase.Rule.OnlyParents(typeof(PhysicsVelocityDefaultVariant)), this);
            rules.TrySetDefaultVariant(ComponentType.ReadWrite<PhysicsGraphicalSmoothing>(), DefaultVariantSystemBase.Rule.OnlyParents(typeof(PhysicsGraphicalSmoothingDefaultVariant)), this);
            Enabled = false;
        }

        protected override void OnUpdate()
        {
        }
    }
}
