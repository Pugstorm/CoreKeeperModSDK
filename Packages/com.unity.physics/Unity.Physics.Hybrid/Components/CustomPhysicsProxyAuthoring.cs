using System;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// This authoring component will create multiple proxy entities in different physics worlds.
    /// </summary>
#if UNITY_2021_2_OR_NEWER
    [Icon(k_IconPath)]
#endif
    [AddComponentMenu("Entities/Physics/Custom Physics Proxy")]
    [HelpURL(HelpURLs.CustomPhysicsProxyAuthoring)]
    public sealed class CustomPhysicsProxyAuthoring : MonoBehaviour
    {
#if UNITY_2021_2_OR_NEWER
        const string k_IconPath = "Packages/com.unity.physics/Unity.Physics.Editor/Editor Default Resources/Icons/d_Rigidbody@64.png";
#endif

        /// <summary>
        /// First Order Gain <para/>
        /// Coefficient in range [0,1] denoting how much the client body will be driven by position (teleported), while the rest of the position diff will be velocity-driven."
        /// </summary>
        [Range(0f, 1f)]
        [Tooltip("Coefficient in range [0,1] denoting how much the client body will be driven by position (teleported), while the rest of the position diff will be velocity-driven.")]
        public float FirstOrderGain = 0.0f;

        /// <summary>
        /// A bitmask enum for physics world indices.
        /// </summary>
        [Flags]
        public enum TargetWorld : byte
        {
            /// <summary> World with index 0 (default)</summary>
            DefaultWorld = 1,
            /// <summary> World with index 1 </summary>
            World1 = 2,
            /// <summary> World with index 2 </summary>
            World2 = 4,
            /// <summary> World with index 3 </summary>
            World3 = 8
        }
        /// <summary>
        /// A mask of physics world indices in which proxy entities should be created.
        /// </summary>
        public TargetWorld TargetPhysicsWorld = TargetWorld.World1;
    }


    class CustomPhysicsProxyBaker : Baker<CustomPhysicsProxyAuthoring>
    {
        public override void Bake(CustomPhysicsProxyAuthoring authoring)
        {
            for (int i = 0; i < 4; ++i)
            {
                if (((int)authoring.TargetPhysicsWorld & (1 << i)) == 0)
                    continue;
                var proxyEnt = CreateAdditionalEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.WorldSpace);
                AddComponent(proxyEnt, new CustomPhysicsProxyDriver {rootEntity = GetEntity(TransformUsageFlags.Dynamic), FirstOrderGain = authoring.FirstOrderGain});

                AddComponent(proxyEnt, default(LocalTransform));

                AddComponent(proxyEnt, default(PhysicsMass));
                AddComponent(proxyEnt, default(PhysicsVelocity));
                AddComponent(proxyEnt, default(PhysicsCollider));
                AddSharedComponent(proxyEnt, new PhysicsWorldIndex((uint)i));
            }
        }
    }
}
