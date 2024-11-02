using System;
using UnityEngine.LowLevel;

namespace Unity.Entities.Editor
{
    using SystemWrapper = ScriptBehaviourUpdateOrder.DummyDelegateWrapper;

    class PlayerLoopSystemGraph : SystemGraph
    {
        public WorldProxyManager WorldProxyManager { get; set; }

        internal void BuildCurrentGraph()
        {
            ResetFromPlayerLoop(PlayerLoop.GetCurrentPlayerLoop());
        }

        /// <summary>
        /// Parse through the player loop system to get all system list and their parent-children relationship,
        /// which will be used to build the tree view.
        /// </summary>
        /// <param name="rootPlayerLoopSystem"></param>
        void ResetFromPlayerLoop(PlayerLoopSystem rootPlayerLoopSystem)
        {
            Reset();
            AddFromPlayerLoop(rootPlayerLoopSystem);
        }

        void AddFromPlayerLoop(PlayerLoopSystem playerLoopSystem, IPlayerLoopNode parent = null)
        {
            // The integration of `ComponentSystemBase` into the player loop is done through a wrapper type.
            // If the target of the player loop system is the wrapper type, we will parse this as a `ComponentSystemBase`.
            if (null != playerLoopSystem.updateDelegate && playerLoopSystem.updateDelegate.Target is SystemWrapper wrapper)
            {
                var systemWorld = wrapper.System.World;
                if (systemWorld is not {IsCreated: true})
                    return;

                AddSystem(new SystemProxy(wrapper.System, WorldProxyManager.GetWorldProxyForGivenWorld(systemWorld)), parent);
                return;
            }

            // Add the player loop system to the graph if it is not the root one.
            if (null != playerLoopSystem.type)
            {
                var playerLoopSystemNode = Pool<PlayerLoopSystemNode>.GetPooled();
                playerLoopSystemNode.Value = playerLoopSystem;
                var node = playerLoopSystemNode;
                AddNode(node, parent);
                parent = node;
            }

            if (null == playerLoopSystem.subSystemList)
                return;

            foreach (var subSystem in playerLoopSystem.subSystemList)
            {
                AddFromPlayerLoop(subSystem, parent);
            }
        }
    }
}
