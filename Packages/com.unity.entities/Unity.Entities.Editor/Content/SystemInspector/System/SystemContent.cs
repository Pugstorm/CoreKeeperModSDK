using System;

namespace Unity.Entities.Editor
{
    class SystemContent
    {
        public SystemContent(World world, SystemProxy systemProxy)
        {
            World = world;
            SystemProxy = systemProxy;
        }

        public World World { get; }
        public SystemProxy SystemProxy { get; }
    }
}
