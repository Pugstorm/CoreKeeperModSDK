using System;
using System.Collections.Generic;
using Unity.Transforms;

namespace Unity.Entities.Editor
{
    class EntityInspectorAspectsComparer: IComparer<string>
    {
        static readonly string[] k_TopAspects =
        {
            // No built-in aspects currently need to be sorted at the top of the inspector
        };

        public static EntityInspectorAspectsComparer Instance { get; } = new();

        public int Compare(string x, string y)
            => InspectorUtility.Compare(x, y, k_TopAspects);
    }
}
