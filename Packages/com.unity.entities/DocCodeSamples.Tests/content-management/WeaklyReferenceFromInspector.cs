namespace Doc.CodeSamples.Tests
{
    #region example
    using Unity.Entities;
    using Unity.Entities.Content;
    using UnityEngine;

    public class MeshRefSample : MonoBehaviour
    {
        public WeakObjectReference<Mesh> mesh;
        class MeshRefSampleBaker : Baker<MeshRefSample>
        {
            public override void Bake(MeshRefSample authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MeshComponentData { mesh = authoring.mesh });
            }
        }
    }

    public struct MeshComponentData : IComponentData
    {
        public WeakObjectReference<Mesh> mesh;
    }
    #endregion
}
