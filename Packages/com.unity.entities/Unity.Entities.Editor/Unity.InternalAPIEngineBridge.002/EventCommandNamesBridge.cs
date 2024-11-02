using UnityEngine;

namespace Unity.Editor.Bridge
{
    static class EventCommandNamesBridge
    {
        public const string Delete = EventCommandNames.Delete;
        public const string SoftDelete = EventCommandNames.SoftDelete;
        public const string Duplicate = EventCommandNames.Duplicate;
        public const string Rename = EventCommandNames.Rename;
        public const string Cut = EventCommandNames.Cut;
        public const string Copy = EventCommandNames.Copy;
        public const string Paste = EventCommandNames.Paste;
        public const string SelectAll = EventCommandNames.SelectAll;
        public const string DeselectAll = EventCommandNames.DeselectAll;
        public const string InvertSelection = EventCommandNames.InvertSelection;
        public const string SelectChildren = EventCommandNames.SelectChildren;
        public const string SelectPrefabRoot = EventCommandNames.SelectPrefabRoot;
        public const string Find = EventCommandNames.Find;
    }
}
