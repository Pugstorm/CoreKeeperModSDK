using System;
using System.Collections.Generic;
using Unity.Transforms;

namespace Unity.Entities.Editor
{
    class EntityInspectorComponentsComparer: IComparer<string>
    {
        static readonly string[] k_TopComponents =
        {
            nameof(LocalTransform),
            nameof(PostTransformMatrix),
            nameof(LocalToWorld),
            nameof(Parent),
        };

        public static EntityInspectorComponentsComparer Instance { get; } = new();

        public int Compare(string x, string y)
            => InspectorUtility.Compare(x, y, k_TopComponents);
    }
}
