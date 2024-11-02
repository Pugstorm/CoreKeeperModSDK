using System;
using Unity.Entities;
using Unity.Scenes.Hybrid.Tests;
using UnityEngine;

namespace Unity.Scenes.Hybrid.Tests
{
    public struct RuntimeUnmanaged : IComponentData
    {
        public int Value;
    }

    [DisallowMultipleComponent]
    public class AuthoringWithUnmanaged : MonoBehaviour
    {
        public int Value;
    }

    public class AuthoringWithUnmanagedBaker : Baker<AuthoringWithUnmanaged>
    {
        public override void Bake(AuthoringWithUnmanaged authoring)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new RuntimeUnmanaged{ Value = authoring.Value});
        }
    }
}
