using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public class NetcodeTransformUsageFlagsTestAuthoring : MonoBehaviour
{
    public class Baker : Baker<NetcodeTransformUsageFlagsTestAuthoring>
    {
        public override void Bake(NetcodeTransformUsageFlagsTestAuthoring authoring)
        {
            AddTransformUsageFlags(TransformUsageFlags.Dynamic);
        }
    }
}
