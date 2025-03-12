using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    /// <summary>
    ///     Baking system for colliders, called after all other collider baking systems.
    /// </summary>
    [UpdateAfter(typeof(BeginColliderBakingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial class EndColliderBakingSystem : SystemBase
    {
        BeginColliderBakingSystem m_BeginColliderBakingSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_BeginColliderBakingSystem = World.GetOrCreateSystemManaged<BeginColliderBakingSystem>();
        }

        protected override void OnUpdate()
        {
            m_BeginColliderBakingSystem.BlobComputationContext.Dispose();
        }
    }
}
