using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityComponent = UnityEngine.Component;

namespace Unity.Physics.Authoring
{
    /// <summary>
    ///     Baking system, called after all baking systems that produce <see cref="PhysicsJoint"/> components.
    /// </summary>
    [UpdateAfter(typeof(BeginJointBakingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct EndJointBakingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
