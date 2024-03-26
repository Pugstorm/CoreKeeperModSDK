using Unity.Entities;
using Unity.Profiling;

namespace Unity.NetCode.Tests
{
#if UNITY_EDITOR
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(GhostCollectionSystem))]
    public partial class GhostCollectionSystemProxy : ComponentSystemGroup
    {
        static readonly ProfilerMarker k_Update = new ProfilerMarker("GhostCollectionSystem_OnUpdate");
        static readonly ProfilerMarker k_CompleteTrackedJobs = new ProfilerMarker("GhostCollectionSystem_CompleteAllTrackedJobs");

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<NetworkStreamInGame>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            var ghostCollectionSystem = World.GetExistingSystem<GhostCollectionSystem>();
            var simulationSystemGroup = World.GetExistingSystemManaged<GhostSimulationSystemGroup>();
            simulationSystemGroup.RemoveSystemFromUpdateList(ghostCollectionSystem);
            AddSystemToUpdateList(ghostCollectionSystem);
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
