using UnityEngine.LowLevel;

namespace Unity.Entities.Editor
{
    interface IPlayerLoopSystemData
    {
        PlayerLoopSystem PlayerLoopSystem { get; }
    }

    class PlayerLoopSystemNode : PlayerLoopNode<PlayerLoopSystem>, IPlayerLoopSystemData
    {
        public override string Name
        {
            get
            {
                var type = Value.type;
                return type == null ? string.Empty : UnityEditor.ObjectNames.NicifyVariableName(Properties.TypeUtility.GetTypeDisplayName(type));
            }
        }
        public override string NameWithWorld => Name;
        public override string FullName
        {
            get
            {
                var type = Value.type;
                return type == null ? string.Empty : $"{type.Namespace}{(null == type.Namespace ? "" : ".")}{Properties.TypeUtility.GetTypeDisplayName(type)}";
            }
        }
        public override bool Enabled
        {
            get => true;
            set { }
        }

        public override bool EnabledInHierarchy => Enabled && (Parent?.EnabledInHierarchy ?? true);
        public override int Hash => FullName.GetHashCode();

        public PlayerLoopSystem PlayerLoopSystem => Value;

        public override bool ShowForWorldProxy(WorldProxy worldProxy)
        {
            if (null == worldProxy)
                return true;

            foreach (var child in Children)
            {
                if (child.ShowForWorldProxy(worldProxy))
                    return true;
            }

            return false;
        }

        public override bool IsRunning => true;

        public override void ReturnToPool()
        {
            base.ReturnToPool();
            Pool<PlayerLoopSystemNode>.Release(this);
        }
    }
}
