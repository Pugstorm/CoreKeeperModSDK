using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Entities.Tests;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class AddTransformUsageFlag : MonoBehaviour
    {
        public TransformUsageFlags flags;

        public class Baker : Baker<AddTransformUsageFlag>
        {
            public override void Bake(AddTransformUsageFlag authoring)
            {
                AddTransformUsageFlags(authoring.flags);
            }
        }
    }
}
