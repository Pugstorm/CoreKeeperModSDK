using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Editor.Bridge;
using Unity.Scenes;

namespace Unity.Entities.Editor
{
    class SystemTreeViewItem : ITreeViewItem
    {
        internal static readonly BasicPool<SystemTreeViewItem> Pool = new BasicPool<SystemTreeViewItem>(() => new SystemTreeViewItem());

        public IPlayerLoopNode Node;
        public WorldProxy WorldProxy;
        SystemGraph Graph;
        readonly List<ITreeViewItem> m_CachedChildren = new List<ITreeViewItem>();
        public enum SystemToggleState
        {
            AllEnabled,
            Mixed,
            Disabled
        }

        SystemTreeViewItem() { }

        public static SystemTreeViewItem Acquire(SystemGraph graph, IPlayerLoopNode node, SystemTreeViewItem parent, WorldProxy worldProxy)
        {
            var item = Pool.Acquire();

            item.WorldProxy = worldProxy;
            item.Graph = graph;
            item.Node = node;
            item.parent = parent;

            return item;
        }

        public SystemProxy SystemProxy
        {
            get
            {
                if (Node is ISystemHandleNode systemHandleNode)
                    return systemHandleNode.SystemProxy;

                return default;
            }
        }

        public bool HasChildren => Node.Children.Count > 0;

        public string GetSystemName() => Node?.Name;

        public string GetWorldName()
        {
            if (Node is ISystemHandleNode _)
                return SystemProxy.World.Name;

            return string.Empty;
        }

        public string GetNamespace()
        {
            if (Node is ISystemHandleNode _)
                return SystemProxy.Namespace;

            return string.Empty;
        }

        public bool GetParentState()
        {
            return Node.EnabledInHierarchy;
        }

        public void SetSystemEnabled(bool value)
        {
            if (Node.Enabled == value)
                return;

            Node.Enabled = value;
            EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
        }

        public SystemToggleState GetSystemToggleState()
        {
            // Root group nodes, ini, update, prelate update.
            if (!SystemProxy.Valid)
                return HasMixedState() ? SystemToggleState.Mixed : SystemToggleState.AllEnabled;

            // No children
            if (!Node.Children.Any())
                return SystemProxy.Enabled ? SystemToggleState.AllEnabled : SystemToggleState.Disabled;

            // With children
            // current node is disabled.
            if (!SystemProxy.Enabled)
                return SystemToggleState.Disabled;

            // any children is in mixed state.
            if (HasMixedState())
               return SystemToggleState.Mixed;

            return SystemToggleState.AllEnabled;
        }

        bool HasMixedState()
        {
            return children.Select(child => child as SystemTreeViewItem).Any(childItem =>
            {
                var toggleState = childItem?.GetSystemToggleState();
                return toggleState == SystemToggleState.Mixed || toggleState == SystemToggleState.Disabled;
            });
        }

        public string GetEntityMatches()
        {
            if (HasChildren) // Group system do not need entity matches.
                return string.Empty;

            if (!SystemProxy.Valid)
                return string.Empty;

            if (!Node.Enabled || !NodeParentsAllEnabled(Node))
            {
                return Constants.SystemSchedule.k_Dash;
            }

            return SystemProxy.TotalEntityMatches.ToString();
        }

        float GetAverageRunningTime(SystemProxy systemProxy)
        {
            return Graph.GetAverageRunningTime(systemProxy);
        }

        public string GetRunningTime(bool morePrecision)
        {
            if (Node is IPlayerLoopSystemData)
                return string.Empty;

            // if the node is disabled, not running, or if its parents are all disabled
            // TODO this isn't really great; we should read the raw profile data, if something is taking up time we should know it regardless of enabled etc state
            if (!Node.IsRunning || !Node.Enabled || !NodeParentsAllEnabled(Node))
                return Constants.SystemSchedule.k_Dash;

            // if the node is a group node, it's just its own time (it has its own profiler marker)
            if (Node is ComponentGroupNode groupNode)
            {
                return AdjustPrecision(GetAverageRunningTime(groupNode.SystemProxy));
            }

            // if it has any children, it's the sum of all of its children that are SystemHandleNodes
            if (children.Any())
            {
                var sum = Node.Children
                    .OfType<ISystemHandleNode>()
                    .Sum(child => GetAverageRunningTime(child.SystemProxy));

                return AdjustPrecision(sum);
            }

            // if it's not a system handle at this point, we can't show anything useful
            if (Node is ISystemHandleNode sysNode)
            {
                return AdjustPrecision(sysNode.SystemProxy.RunTimeMillisecondsForDisplay);
            }

            return Constants.SystemSchedule.k_Dash;

            string AdjustPrecision(float sum)
            {
                return morePrecision ? sum.ToString("f4") : sum.ToString("f2");
            }
        }

        bool NodeParentsAllEnabled(IPlayerLoopNode node)
        {
            if (node.Parent != null)
            {
                if (!node.Parent.Enabled) return false;
                if (!NodeParentsAllEnabled(node.Parent)) return false;
            }

            return true;
        }

        public int id => Node.Hash;
        public ITreeViewItem parent { get; internal set; }
        public IEnumerable<ITreeViewItem> children => m_CachedChildren;
        bool ITreeViewItem.hasChildren => HasChildren;

        public void AddChild(ITreeViewItem child)
        {
            throw new NotImplementedException();
        }

        public void AddChildren(IList<ITreeViewItem> children)
        {
            throw new NotImplementedException();
        }

        public void RemoveChild(ITreeViewItem child)
        {
            throw new NotImplementedException();
        }

        public void PopulateChildren()
        {
            m_CachedChildren.Clear();

            foreach (var child in Node.Children)
            {
                if (!child.ShowForWorldProxy(WorldProxy))
                    continue;

                var item = Acquire(Graph, child, this, WorldProxy);
                m_CachedChildren.Add(item);
            }
        }

        public void Release()
        {
            WorldProxy = null;
            Graph = null;
            Node = null;
            parent = null;
            foreach (var child in m_CachedChildren.OfType<SystemTreeViewItem>())
            {
                child.Release();
            }

            m_CachedChildren.Clear();

            Pool.Release(this);
        }
    }
}
