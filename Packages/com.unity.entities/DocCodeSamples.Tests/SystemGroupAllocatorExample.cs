using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Core;

namespace Doc.CodeSamples.Tests
{
    #region create-allocator
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    public partial class FixedStepTestSimulationSystemGroup : ComponentSystemGroup
    {
        // Set the timestep use by this group, in seconds. The default value is 1/60 seconds.
        // This value will be clamped to the range [0.0001f ... 10.0f].
        public float Timestep
        {
            get => RateManager != null ? RateManager.Timestep : 0;
            set
            {
                if (RateManager != null)
                    RateManager.Timestep = value;
            }
        }

        // Default constructor
        public FixedStepTestSimulationSystemGroup()
        {
            float defaultFixedTimestep = 1.0f / 60.0f;

            // Set FixedRateSimpleManager to be the rate manager and create a system group allocator
            SetRateManagerCreateAllocator(new RateUtils.FixedRateSimpleManager(defaultFixedTimestep));
        }
    }
    #endregion

    #region group-allocator
    public unsafe class FixedRateSimpleManager : IRateManager
    {
        const float MinFixedDeltaTime = 0.0001f;
        const float MaxFixedDeltaTime = 10.0f;

        float m_FixedTimestep;
        public float Timestep
        {
            get => m_FixedTimestep;
            set => m_FixedTimestep = math.clamp(value, MinFixedDeltaTime, MaxFixedDeltaTime);
        }

        double m_LastFixedUpdateTime;
        bool m_DidPushTime;

        DoubleRewindableAllocators* m_OldGroupAllocators = null;

        public FixedRateSimpleManager(float fixedDeltaTime)
        {
            Timestep = fixedDeltaTime;
        }

        public bool ShouldGroupUpdate(ComponentSystemGroup group)
        {
            // if this is true, means we're being called a second or later time in a loop.
            if (m_DidPushTime)
            {
                group.World.PopTime();
                m_DidPushTime = false;

                // Update the group allocators and restore the old allocator
                group.World.RestoreGroupAllocator(m_OldGroupAllocators);

                return false;
            }

            group.World.PushTime(new TimeData(
                elapsedTime: m_LastFixedUpdateTime,
                deltaTime: m_FixedTimestep));

            m_LastFixedUpdateTime += m_FixedTimestep;

            m_DidPushTime = true;

            // Back up current world or group allocator.
            m_OldGroupAllocators = group.World.CurrentGroupAllocators;
            // Replace current world or group allocator with this system group allocator.
            group.World.SetGroupAllocator(group.RateGroupAllocators);

            return true;
        }
    }
    #endregion
}
