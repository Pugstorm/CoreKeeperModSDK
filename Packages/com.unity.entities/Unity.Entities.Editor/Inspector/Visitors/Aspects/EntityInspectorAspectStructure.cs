using System;
using System.Collections.Generic;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Helper type to contain the aspect order of a given entity.
    /// </summary>
    class EntityInspectorAspectStructure
    {
        public readonly List<string> Aspects = new List<string>();

        public void Reset()
        {
            Aspects.Clear();
        }

        public void Sort()
        {
            Aspects.Sort(EntityInspectorAspectsComparer.Instance);
        }

        public void CopyFrom(EntityInspectorAspectStructure other)
        {
            Reset();

            Aspects.AddRange(other.Aspects);
        }
    }
}
