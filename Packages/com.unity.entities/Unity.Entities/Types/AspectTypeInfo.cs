using System;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    struct AspectTypeInfo : IDisposable
    {
        public UnsafeList<AspectType> AspectTypes;
        public UnsafeParallelMultiHashMap<AspectType, ComponentType> AspectRequiredComponents;
        public UnsafeParallelMultiHashMap<AspectType, ComponentType> AspectExcludedComponents;

        public void Dispose()
        {
            if (AspectTypes.IsCreated)
                AspectTypes.Dispose();

            if (AspectRequiredComponents.IsCreated)
                AspectRequiredComponents.Dispose();

            if (AspectExcludedComponents.IsCreated)
                AspectExcludedComponents.Dispose();
        }
    }
}
