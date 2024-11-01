using Unity.Entities;
using UnityEngine;
namespace Unity.Scenes.Editor.Tests
{
    public class TypeDependencyCacheAuthoring : MonoBehaviour
    {
    }
    public class TypeDependencyCacheBaker : Baker<TypeDependencyCacheAuthoring>
    {
        public override void Bake(TypeDependencyCacheAuthoring authoring)
        {
        }
    }
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial class TypeDependencyCacheSystem : SystemBase
    {
        protected override void OnUpdate()
        {
        }
    }
}


