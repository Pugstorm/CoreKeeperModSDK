using Unity.Entities;
using Unity.PerformanceTesting;
using Unity.Profiling;

namespace Unity.NetCode.Tests
{
#if UNITY_EDITOR
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateAfter(typeof(PrespawnGhostSystemGroup))]
    [UpdateAfter(typeof(GhostCollectionSystem))]
    [UpdateAfter(typeof(NetDebugSystem))]
    [UpdateBefore(typeof(GhostReceiveSystem))]
    public partial class GhostReceiveSystemProxy : ComponentSystemGroup
    {
        static readonly ProfilerMarker k_Update = new ProfilerMarker("GhostReceiveSystem_OnUpdate");
        static readonly ProfilerMarker k_CompleteTrackedJobs = new ProfilerMarker("GhostReceiveSystem_CompleteAllTrackedJobs");

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<GhostCollection>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            var ghostReceiveSystem = World.GetExistingSystem<GhostUpdateSystem>();
            var ghostSimulationSystemGroup = World.GetExistingSystemManaged<GhostSimulationSystemGroup>();
            ghostSimulationSystemGroup.RemoveSystemFromUpdateList(ghostReceiveSystem);
            AddSystemToUpdateList(ghostReceiveSystem);
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
