using Unity.Core;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace Unity.Entities
{
    /// <summary>
    /// A system that updates the <see cref="WorldTime"/> value, based on the elapsed time since the previous frame.
    /// </summary>
    /// <remarks>By default, the deltaTime is read from <see cref="UnityEngine.Time.deltaTime"/>.</remarks>
    [Preserve]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class UpdateWorldTimeSystem : SystemBase
    {
        /// <summary>Runs just before the system's first update after it is enabled.</summary>
        /// <remarks>
        /// Ensure that the final elapsedTime of the very first OnUpdate call is the
        /// original Time.ElapsedTime value (usually zero) without a deltaTime applied.
        /// Effectively, this code preemptively counteracts the first OnUpdate call.
        /// </remarks>
        protected override void OnStartRunning()
        {
            var currentElapsedTime = SystemAPI.Time.ElapsedTime;
            var deltaTime = math.min(UnityEngine.Time.deltaTime, World.MaximumDeltaTime);
            World.SetTime(new TimeData(
                elapsedTime: currentElapsedTime-deltaTime,
                deltaTime: deltaTime
            ));
        }

        /// <summary>
        /// Updates the world time
        /// </summary>
        protected override void OnUpdate()
        {
            var currentElapsedTime = SystemAPI.Time.ElapsedTime;
            var deltaTime = math.min(UnityEngine.Time.deltaTime, World.MaximumDeltaTime);
            World.SetTime(new TimeData(
                elapsedTime: currentElapsedTime + deltaTime,
                deltaTime: deltaTime
            ));
        }
    }
}
