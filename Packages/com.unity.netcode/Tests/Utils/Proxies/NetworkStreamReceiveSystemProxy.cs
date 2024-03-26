using Unity.Entities;
using Unity.Profiling;

namespace Unity.NetCode.Tests
{
#if UNITY_EDITOR
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(NetworkReceiveSystemGroup))]
    [UpdateBefore(typeof(NetworkStreamReceiveSystem))]
    public partial class NetworkStreamReceiveSystemProxy : ComponentSystemGroup
    {
        static readonly ProfilerMarker k_Update = new ProfilerMarker("NetworkStreamReceiveSystem_OnUpdate");
        static readonly ProfilerMarker k_CompleteTrackedJobs = new ProfilerMarker("NetworkStreamReceiveSystem_CompleteAllTrackedJobs");

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<NetworkStreamInGame>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            var networkStreamReceiveSystem = World.GetExistingSystem<NetworkStreamReceiveSystem>();
            var simulationSystemGroup = World.GetExistingSystemManaged<NetworkReceiveSystemGroup>();
            simulationSystemGroup.RemoveSystemFromUpdateList(networkStreamReceiveSystem);
            AddSystemToUpdateList(networkStreamReceiveSystem);
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
