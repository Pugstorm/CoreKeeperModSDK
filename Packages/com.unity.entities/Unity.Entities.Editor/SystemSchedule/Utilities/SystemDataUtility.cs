using System;
using System.Runtime.InteropServices;

namespace Unity.Entities.Editor
{
    [Flags]
    enum SystemStateFlags : uint
    {
        None = 0,
        Enabled = 1 << 1,
        IsRunning = 1 << 2,
    }

    [Flags]
    enum SystemCategory
    {
        // No known category
        Unknown = 0,

        // Unmanaged
        Unmanaged = 1 << 0,

        // SystemBase -- note some overlap with Unmanaged, technically something that's managed should always be a systembase
        SystemBase = 1 << 1,

        // EntityCommandBufferSystem (_not_ SystemBase)
        EntityCommandBufferSystem = 1 << 2,

        // ComponentSystemGroup
        SystemGroup = 1 << 3,

        // ECBBegin (if scheduling is begin); together with EntityCommandBufferSystem
        ECBSystemBegin = 1 << 4,

        // ECBBegin (if scheduling is end); together with EntityCommandBufferSystem
        ECBSystemEnd = 1 << 5,
    }

    static class SystemUtils
    {
        internal static SystemCategory GetSystemCategory(ComponentSystemBase b)
        {
            var flags = SystemCategory.SystemBase;
            switch (b)
            {
                case EntityCommandBufferSystem _:
                    switch (SystemDependencyUtilities.GetECBSystemScheduleInfo(b.GetType()))
                    {
                        case SystemDependencyUtilities.ECBSystemSchedule.Begin:
                            flags |= SystemCategory.ECBSystemBegin;
                            break;
                        case SystemDependencyUtilities.ECBSystemSchedule.End:
                            flags |= SystemCategory.ECBSystemEnd;
                            break;
                        case SystemDependencyUtilities.ECBSystemSchedule.None:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    break;
                case ComponentSystemGroup _:
                    flags |= SystemCategory.SystemGroup;
                    break;
            }

            return flags;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SystemFrameData
    {
        public int EntityCount;
        public float LastFrameRuntimeMilliseconds;
        public SystemStateFlags Flags;

        public bool Enabled
        {
            get => (Flags & SystemStateFlags.Enabled) != 0;
            set => Flags = value ? Flags | SystemStateFlags.Enabled : Flags & ~SystemStateFlags.Enabled;
        }

        public bool IsRunning
        {
            get => (Flags & SystemStateFlags.IsRunning) != 0;
            set => Flags = value ? Flags | SystemStateFlags.IsRunning :Flags & ~SystemStateFlags.IsRunning;
        }
    }
}
