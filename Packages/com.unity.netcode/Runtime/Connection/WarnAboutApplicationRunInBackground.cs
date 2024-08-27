#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif

#if NETCODE_DEBUG
using Unity.Entities;

namespace Unity.NetCode
{
    /// <inheritdoc cref="NetDebug.SuppressApplicationRunInBackgroundWarning"/>>
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WarnAboutApplicationRunInBackground : ISystem, ISystemStartStop
    {
        /// <summary>
        /// Require user to be connected to show this warning.
        /// </summary>
        /// <param name="state"></param>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkId>();
        }

        /// <summary>
        /// Handle raising the warning.
        /// </summary>
        /// <param name="state"></param>
        public void OnUpdate(ref SystemState state)
        {
            ref var netDebug = ref SystemAPI.GetSingletonRW<NetDebug>().ValueRW;
            if (netDebug.SuppressApplicationRunInBackgroundWarning || netDebug.HasWarnedAboutApplicationRunInBackground)
                return;

            // @FIXME: Singleplayer via two world support needs to suppress this.
            if (!UnityEngine.Application.runInBackground)
            {
                netDebug.HasWarnedAboutApplicationRunInBackground = true;
                UnityEngine.Debug.LogError($"[{state.WorldUnmanaged.Name}] Netcode detected that you don't have Application.runInBackground enabled during multiplayer gameplay. This will lead to your multiplayer stalling (and disconnecting) if and when the application loses focus (e.g. by the player tabbing out). It is highly recommended to enable \"Run in Background\" via `Application.runInBackground = true;` when connecting, or project-wide via 'Project Settings > Resolution & Presentation > Run in Background'.\nSuppress this advice log via `NetDebug.SuppressApplicationRunInBackgroundWarning`.");
            }
        }

        /// <summary>Reset the warning as we've disconnected.</summary>
        /// <param name="state"></param>
        public void OnStartRunning(ref SystemState state)
        {
            ref var netDebug = ref SystemAPI.GetSingletonRW<NetDebug>().ValueRW;
            netDebug.HasWarnedAboutApplicationRunInBackground = false;
        }

        /// <summary>Does nothing.</summary>
        /// <param name="state"></param>
        public void OnStopRunning(ref SystemState state)
        {
        }
    }
}
#endif
