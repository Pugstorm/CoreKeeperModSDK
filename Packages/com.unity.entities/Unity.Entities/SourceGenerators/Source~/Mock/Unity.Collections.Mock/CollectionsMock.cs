namespace Unity.Collections
{
    public enum NativeArrayOptions
    {
        UninitializedMemory,
        ClearMemory,
    }

    public class CollectionsMock
    {

    }

    public enum Allocator
    {
        Invalid = 0,
        None = 1,
        Temp = 2,
        TempJob = 3,
        Persistent = 4,
        AudioKernel = 5,
        FirstUserIndex = 64, // 0x00000040
    }

    public struct AllocatorHandle
    {
        public static implicit operator AllocatorHandle(Allocator a) => default;
    }
}
