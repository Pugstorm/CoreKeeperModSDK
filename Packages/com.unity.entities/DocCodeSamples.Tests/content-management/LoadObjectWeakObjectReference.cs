namespace Doc.CodeSamples.Tests
{
    #region example
    using Unity.Entities;
    using Unity.Entities.Content;
    using Unity.Transforms;
    using UnityEngine;

    public struct WeakObjectReferenceData : IComponentData
    {
        public bool startedLoad;
        public WeakObjectReference<Mesh> mesh;
        public WeakObjectReference<Material> material;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct RenderFromWeakObjectReferenceSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }
        public void OnDestroy(ref SystemState state) { }
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (transform, dec) in SystemAPI.Query<RefRW<LocalToWorld>, RefRW<WeakObjectReferenceData>>())
            {
                if (!dec.ValueRW.startedLoad)
                {
                    dec.ValueRW.mesh.LoadAsync();
                    dec.ValueRW.material.LoadAsync();
                    dec.ValueRW.startedLoad = true;
                }
                if (dec.ValueRW.mesh.LoadingStatus == ObjectLoadingStatus.Completed &&
                    dec.ValueRW.material.LoadingStatus == ObjectLoadingStatus.Completed)
                {
                    Graphics.DrawMesh(dec.ValueRO.mesh.Result,
                        transform.ValueRO.Value, dec.ValueRO.material.Result, 0);
                }
            }
        }
    }
    #endregion
}
