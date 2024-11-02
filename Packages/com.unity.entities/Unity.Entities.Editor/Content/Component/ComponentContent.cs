using System;

namespace Unity.Entities.Editor
{
    class ComponentContent
    {
        public Type Type { get; }

        public ComponentContent(Type type)
        {
            Type = type;
        }
    }
}
