using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Scenes.Editor.Tests
{
    /// <summary>
    /// Copy of TransformAuthoring but pushed into the live world and stripped of indeterministic version numbers
    /// This is useful for letting the differ detect any incremental conversion bugs. (They are not detected by default since TransformAuthoring is automatically ignored by the differ)
    /// </summary>
    struct TransformAuthoringCopyForTest : IComponentData
    {
        public TransformAuthoring Value;

        public RuntimeTransformComponentFlags RuntimeTransformUsage => Value.RuntimeTransformUsage;
    }

    [DisableAutoCreation]
    partial class TransformAuthoringCopyForTestSystem : SystemBase
    {
        EntityQuery _RemoveQuery;
        EntityQuery _AddQuery;
        EntityQuery _UpdateQuery;

        protected override void OnCreate()
        {
            base.OnCreate();

            _RemoveQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] {typeof(TransformAuthoringCopyForTest)},
                None = new ComponentType[] {typeof(TransformAuthoring)},
                Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
            });

            _AddQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] {typeof(TransformAuthoring)},
                None = new ComponentType[] {typeof(TransformAuthoringCopyForTest)},
                Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
            });

            _UpdateQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] {typeof(TransformAuthoring), typeof(TransformAuthoringCopyForTest)},
                Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
            });
        }

        protected override unsafe void OnUpdate()
        {
            EntityManager.AddComponent<TransformAuthoringCopyForTest>(_AddQuery);
            EntityManager.RemoveComponent<TransformAuthoringCopyForTest>(_RemoveQuery);

            using var transforms = _UpdateQuery.ToComponentDataArray<TransformAuthoring>(Allocator.TempJob);
            var ptr = (TransformAuthoring*)transforms.GetUnsafePtr();
            for (int i = 0; i != transforms.Length; i++)
                ptr[i].ChangeVersion = 0;
            _UpdateQuery.CopyFromComponentDataArray(transforms.Reinterpret<TransformAuthoringCopyForTest>());
        }
    }
}
