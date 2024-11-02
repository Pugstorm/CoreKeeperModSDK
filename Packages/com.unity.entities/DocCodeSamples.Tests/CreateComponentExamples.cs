using System;
using Unity.Entities;

namespace Doc.CodeSamples.Tests
{
    #region buffer
    [InternalBufferCapacity(16)]
    public struct ExampleBufferComponent : IBufferElementData
    {
        public int Value;
    }
    #endregion

    #region chunk
    public struct ExampleChunkComponent : IComponentData
    {
        public int Value;
    }
    #endregion

    #region system-state-shared
    public struct ExampleSharedCleanupComponent : ICleanupSharedComponentData
    {
        public int Value;
    }
    #endregion

    #region system-state
    public struct ExampleCleanupComponent : ICleanupComponentData
    {

    }
    #endregion

    #region tag
    public struct ExampleTagComponent : IComponentData
    {

    }
    #endregion

    #region unmanaged
    public struct ExampleUnmanagedComponent : IComponentData
    {
        public int Value;
    }
    #endregion

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    #region managed
    public class ExampleManagedComponent : IComponentData
    {
        public int Value;
    }
    #endregion
#endif

    #region shared-unmanaged
    public struct ExampleUnmanagedSharedComponent : ISharedComponentData
    {
        public int Value;
    }
    #endregion

    #region shared-managed
    public struct ExampleManagedSharedComponent : ISharedComponentData, IEquatable<ExampleManagedSharedComponent>
    {
        public string Value; // A managed field type

        public bool Equals(ExampleManagedSharedComponent other)
        {
            return Value.Equals(other.Value);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
    #endregion
}
