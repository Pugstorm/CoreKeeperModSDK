using System;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class DependencyTestAuthoring : MonoBehaviour
    {
        public GameObject GameObject;
        public UnityEngine.Object Asset;
        public Material Material;
        public Texture Texture;

        class Baker : Baker<DependencyTestAuthoring>
        {
            public override void Bake(DependencyTestAuthoring authoring)
            {
                DependsOn(authoring.Asset);
                DependsOn(authoring.GameObject);
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ConversionDependencyData
                {
                    MaterialColor = DependsOn(authoring.Material) != null ? authoring.Material.color : default,
                    HasMaterial = DependsOn(authoring.Material) != null,
                    TextureFilterMode = DependsOn(authoring.Texture) != null ? authoring.Texture.filterMode : default,
                    HasTexture = DependsOn(authoring.Texture) != null
                });
            }
        }
    }

    public struct ConversionDependencyData : IComponentData
    {
        public Color MaterialColor;
        public FilterMode TextureFilterMode;
        public bool HasMaterial;
        public bool HasTexture;
    }
}
