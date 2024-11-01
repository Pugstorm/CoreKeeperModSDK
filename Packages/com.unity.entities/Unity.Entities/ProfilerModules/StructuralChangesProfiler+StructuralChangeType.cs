#if ENABLE_PROFILER
namespace Unity.Entities
{
    partial class StructuralChangesProfiler
    {
        public enum StructuralChangeType : int
        {
            CreateEntity,
            DestroyEntity,
            AddComponent,
            RemoveComponent,
            SetSharedComponent
        }
    }
}
#endif
