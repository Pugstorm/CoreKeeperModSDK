using System;
using System.Collections.Generic;

namespace Unity.Entities.Editor
{
    abstract class SystemGraph
    {
        public readonly List<IPlayerLoopNode> Roots = new List<IPlayerLoopNode>();
        public readonly List<SystemProxy> AllSystems = new List<SystemProxy>();

        public virtual void Reset()
        {
            foreach (var root in Roots)
            {
                root.ReturnToPool();
            }
            Roots.Clear();
            AllSystems.Clear();
        }

        protected void AddNode(IPlayerLoopNode node, IPlayerLoopNode parent = null)
        {
            if (null == parent)
            {
                Roots.Add(node);
            }
            else
            {
                node.Parent = parent;
                parent.Children.Add(node);
            }
        }

        protected void AddSystem(SystemProxy systemProxy, IPlayerLoopNode parent = null)
        {
            IPlayerLoopNode node;

            AllSystems.Add(systemProxy);

            if ((systemProxy.Category & SystemCategory.SystemGroup) != 0)
            {
                var groupNode = Pool<ComponentGroupNode>.GetPooled();
                groupNode.Value = systemProxy;
                node = groupNode;

                var worldData = systemProxy.WorldProxy;
                var firstChild = systemProxy.FirstChildIndexInWorld;

                for (var i = 0; i < systemProxy.ChildCount; i++)
                {
                    AddSystem(worldData.AllSystems[i + firstChild], node);
                }
            }
            else
            {
                var systemNode = Pool<SystemHandleNode>.GetPooled();
                systemNode.Value = systemProxy;

                node = systemNode;
            }

            AddNode(node, parent);
        }

        public float GetAverageRunningTime(SystemProxy systemProxy)
        {
            return systemProxy.RunTimeMillisecondsForDisplay;
        }
    }
}
