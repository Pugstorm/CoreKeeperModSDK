
namespace Unity.Entities.Editor
{
    class EntityQueryContent
    {
        public World World { get; }
        public EntityQuery Query { get; }
        public SystemProxy SystemProxy { get; }
        public int QueryOrder { get;  }

        public EntityQueryContentTab Tab { get; }

        public EntityQueryContent(World world, EntityQuery query, SystemProxy systemProxy, int queryOrder, EntityQueryContentTab tab)
        {
            World = world;
            Query = query;
            SystemProxy = systemProxy;
            QueryOrder = queryOrder;
            Tab = tab;
        }
    }
}
