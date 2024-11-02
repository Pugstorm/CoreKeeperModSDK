using System;

namespace Unity.Entities.Editor
{
    interface ISystemHandleNode
    {
        SystemProxy SystemProxy { get; }
    }

    abstract class SystemHandleNode<TNode> : PlayerLoopNode<SystemProxy>, ISystemHandleNode
        where TNode : SystemHandleNode<TNode>, new()
    {
        public override string Name => Value.NicifiedDisplayName;
        public override string FullName => Value.TypeFullName;
        public override string NameWithWorld => Name + " (" + Value.World?.Name + ")";

        public override bool Enabled
        {
            get => Value.Enabled;
            set => Value.SetEnabled(value);
        }

        public override bool EnabledInHierarchy => Enabled && (Parent?.EnabledInHierarchy ?? true);

        public SystemProxy SystemProxy
        {
            get
            {
                if (Value != default && Value is SystemProxy systemProxy)
                    return systemProxy;

                return default;
            }
        }

        public override int Hash
        {
            get
            {
                unchecked
                {
                    var worldName = Value.World.Name;
                    const StringComparison comp = StringComparison.InvariantCultureIgnoreCase;
                    if (worldName.IndexOf("Editor World", comp) >= 0 || worldName.IndexOf("Default World", comp) >=  0)
                        worldName = "Editor And Default World";

                    if (worldName.IndexOf("Client", comp) >= 0)
                        worldName = "All Client Worlds";

                    var hashCode = 17;
                    hashCode = hashCode * 31 + FullName.GetHashCode();
                    hashCode = hashCode * 31 + (Parent?.Name.GetHashCode() ?? 0);
                    hashCode = hashCode * 31 + worldName.GetHashCode();

                    return hashCode;
                }
            }
        }

        public override bool ShowForWorldProxy(WorldProxy worldProxy)
        {
            if (!Value.Valid)
                return false;

            if (worldProxy == null)
                return true;

            foreach (var child in Children)
            {
                if (child.ShowForWorldProxy(worldProxy))
                    return true;
            }

            return Value.WorldProxy.Equals(worldProxy);
        }

        public override void Reset()
        {
            base.Reset();
            Value = default;
        }

        public override void ReturnToPool()
        {
            base.ReturnToPool();
            Pool<TNode>.Release((TNode)this);
        }

        public override bool IsRunning => Value.IsRunning;
    }

    class ComponentGroupNode : SystemHandleNode<ComponentGroupNode>
    {
    }

    class SystemHandleNode : SystemHandleNode<SystemHandleNode>
    {
    }
}
