using Unity.Entities;
using Unity.Profiling;

namespace Unity.NetCode.Tests
{
#if UNITY_EDITOR
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateAfter(typeof(GhostReceiveSystem))]
    [UpdateBefore(typeof(GhostSpawnClassificationSystemGroup))]
    [UpdateBefore(typeof(GhostInputSystemGroup))]
    public partial class GhostUpdateSystemProxy : ComponentSystemGroup
    {
        static readonly ProfilerMarker k_Update = new ProfilerMarker("GhostUpdateSystem_OnUpdate");
        static readonly ProfilerMarker k_CompleteTrackedJobs = new ProfilerMarker("GhostUpdateSystem_CompleteAllTrackedJobs");

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<NetworkStreamInGame>();
            RequireForUpdate<GhostCollection>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            var ghostUpdateSystem = World.GetExistingSystem<GhostUpdateSystem>();
            var ghostSimulationSystemGroup = World.GetExistingSystemManaged<GhostSimulationSystemGroup>();
            ghostSimulationSystemGroup.RemoveSystemFromUpdateList(ghostUpdateSystem);
            AddSystemToUpdateList(ghostUpdateSystem);
        }

        protected override void OnUpdate()
        {
            EntityManager.CompleteAllTrackedJobs();

            k_CompleteTrackedJobs.Begin();
            k_Update.Begin();
            base.OnUpdate();
            k_Update.End();
            EntityManager.CompleteAllTrackedJobs();
            k_CompleteTrackedJobs.End();
        }
    }
#endif
}
