namespace Doc.CodeSamples.Tests
{
    #region example
    using Unity.Entities;
    using Unity.Entities.Content;
    using Unity.Transforms;
    using UnityEngine;
    using Unity.Entities.Serialization;

    public struct ObjectUntypedWeakReferenceIdData : IComponentData
    {
        public bool startedLoad;
        public UntypedWeakReferenceId mesh;
        public UntypedWeakReferenceId material;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct RenderFromUntypedWeakReferenceIdSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }
        public void OnDestroy(ref SystemState state) { }
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (transform, dec) in SystemAPI.Query<RefRW<LocalToWorld>, RefRW<ObjectUntypedWeakReferenceIdData>>())
            {
                if (!dec.ValueRO.startedLoad)
                {
                    RuntimeContentManager.LoadObjectAsync(dec.ValueRO.mesh);
                    RuntimeContentManager.LoadObjectAsync(dec.ValueRO.material);
                    dec.ValueRW.startedLoad = true;
                }
                if (RuntimeContentManager.GetObjectLoadingStatus(dec.ValueRO.mesh) == ObjectLoadingStatus.Completed &&
                    RuntimeContentManager.GetObjectLoadingStatus(dec.ValueRO.material) == ObjectLoadingStatus.Completed)
                {
                    Mesh mesh = RuntimeContentManager.GetObjectValue<Mesh>(dec.ValueRO.mesh);
                    Material material = RuntimeContentManager.GetObjectValue<Material>(dec.ValueRO.material);
                    Graphics.DrawMesh(mesh, transform.ValueRO.Value, material, 0);
                }
            }
        }
    }
    #endregion
}
