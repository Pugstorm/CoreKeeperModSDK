using System;

using System.Linq;
using UnityEngine.LowLevel;

namespace Unity.Entities
{
    internal static class RuntimeApplication
    {
        /// <summary>
        /// Event invoked before a frame update.
        /// </summary>
        public static event Action PreFrameUpdate;

        /// <summary>
        /// Event invoked after a frame update.
        /// </summary>
        public static event Action PostFrameUpdate;

        internal static void InvokePreFrameUpdate() => PreFrameUpdate?.Invoke();
        internal static void InvokePostFrameUpdate() => PostFrameUpdate?.Invoke();

        sealed class UpdatePreFrame { }
        sealed class UpdatePostFrame { }

        internal static void RegisterFrameUpdateToCurrentPlayerLoop()
        {
            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            var playerLoopSystems = playerLoop.subSystemList.ToList();
            if (!playerLoopSystems.Any(s => s.type == typeof(UpdatePreFrame)))
            {
                playerLoopSystems.Insert(0, new PlayerLoopSystem
                {
                    type = typeof(UpdatePreFrame),
                    updateDelegate = InvokePreFrameUpdate
                });
            }
            if (!playerLoopSystems.Any(s => s.type == typeof(UpdatePostFrame)))
            {
                playerLoopSystems.Add(new PlayerLoopSystem
                {
                    type = typeof(UpdatePostFrame),
                    updateDelegate = InvokePostFrameUpdate
                });
            }
            playerLoop.subSystemList = playerLoopSystems.ToArray();
            PlayerLoop.SetPlayerLoop(playerLoop);
        }

        internal static void UnregisterFrameUpdateToCurrentPlayerLoop()
        {
            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            var playerLoopSystems = playerLoop.subSystemList.ToList();
            playerLoopSystems.RemoveAll(s => s.type == typeof(UpdatePreFrame));
            playerLoopSystems.RemoveAll(s => s.type == typeof(UpdatePostFrame));
            playerLoop.subSystemList = playerLoopSystems.ToArray();
            PlayerLoop.SetPlayerLoop(playerLoop);
        }
    }
}
