using Unity.Collections;

namespace Unity.Entities.Editor.Tests
{
    static class ResultAccessorHelper
    {
        public static (Entity, T) GetAddedComponent<T>(this ComponentDataDiffer.ChangeSet result, int index) where T : unmanaged
        {
            var (entities, componentData) = result.GetAddedComponents<T>(Allocator.Temp);
            var r = (entities[index], componentData[index]);
            entities.Dispose();
            componentData.Dispose();
            return r;
        }

        public static (Entity, T) GetRemovedComponent<T>(this ComponentDataDiffer.ChangeSet result, int index) where T : unmanaged
        {
            var (entities, componentData) = result.GetRemovedComponents<T>(Allocator.Temp);
            var r = (entities[index], componentData[index]);
            entities.Dispose();
            componentData.Dispose();
            return r;
        }

        public static (Entity, T) GetAddedSharedComponent<T>(this UnmanagedSharedComponentDataDiffer.ChangeSet result, int index) where T : unmanaged, ISharedComponentData
        {
            return (result.GetAddedComponentEntity(index), result.GetAddedComponentData<T>(index));
        }

        public static (Entity, T) GetRemovedSharedComponent<T>(this UnmanagedSharedComponentDataDiffer.ChangeSet result, int index) where T : unmanaged, ISharedComponentData
        {
            return (result.GetRemovedComponentEntity(index), result.GetRemovedComponentData<T>(index));
        }
    }
}
