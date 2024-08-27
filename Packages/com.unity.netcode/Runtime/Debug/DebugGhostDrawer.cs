#if (UNITY_EDITOR || NETCODE_DEBUG)
using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// Allows game code to write its own custom ghost drawers and hook them up in the `MultiplayerPlayModeWindow`.
    /// Implement your own <see cref="CustomDrawer"/> to add a custom debug drawer.
    /// See `BoundingBoxDebugGhostDrawer` for reference.
    /// </summary>
    public class DebugGhostDrawer
    {
        static DebugGhostDrawer s_Instance;

        public static List<CustomDrawer> CustomDrawers = new List<CustomDrawer>(2);
        
        [Obsolete("Use ClientServerBootstrap.ServerWorld instead. RemoveAfter Entities 1.x")]
        public static World FirstServerWorld => ClientServerBootstrap.ServerWorld;

        [Obsolete("Use ClientServerBootstrap.ClientWorld instead. RemoveAfter Entities 1.x")]
        public static World FirstClientWorld => ClientServerBootstrap.ClientWorld;

        static ulong s_LastNextSequenceNumber;

        /// <summary>
        ///     Replaces the existing DrawAction with the same name, if it already exists.
        /// </summary>
        public static void RegisterDrawAction(CustomDrawer newDrawAction)
        {
            if (newDrawAction?.Name == null) throw new ArgumentNullException(nameof(newDrawAction));

            CustomDrawers.RemoveAll(x => string.Equals(x.Name, newDrawAction.Name, StringComparison.OrdinalIgnoreCase));
            CustomDrawers.Add(newDrawAction);
            CustomDrawers.Sort();
        }
        
        [Obsolete("This functionality is obsolete, worlds are no longer cached here. RemoveAfter Entities 1.x")]
        public static void RefreshWorldCaches() {}

        public static bool HasRequiredWorlds => ClientServerBootstrap.ServerWorld != default && ClientServerBootstrap.ClientWorld != default;

        /// <inheritdoc cref="DebugGhostDrawer"/>>
        public class CustomDrawer : IComparer<CustomDrawer>, IComparable<CustomDrawer>
        {
            public readonly string Name;

            public readonly int SortOrder;

            public bool Enabled;
            public bool DetailsVisible;

            [CanBeNull]
            public readonly Action OnGuiAction;

            [CanBeNull]
            readonly Action m_EditorSaveAction;

            public CustomDrawer(string name, int sortOrder, [CanBeNull] Action onGuiAction, [CanBeNull] Action editorSaveAction)
            {
                Name = name;

                SortOrder = sortOrder;
                OnGuiAction = onGuiAction;
                m_EditorSaveAction = editorSaveAction;

#if UNITY_EDITOR
                EditorLoad();
#endif
            }

#if UNITY_EDITOR
            string Key => "DrawAction_" + Name;
            string EnabledKey => Key + "_Enabled";
            string DetailsVisibleKey => Key + "_DetailsVisible";

            void EditorLoad()
            {
                Enabled = UnityEditor.EditorPrefs.GetInt(EnabledKey, 0) != 0;
                DetailsVisible = UnityEditor.EditorPrefs.GetInt(DetailsVisibleKey, 0) != 0;
            }

            public void EditorSave()
            {
                if (!Enabled)
                    DetailsVisible = false;

                UnityEditor.EditorPrefs.SetInt(EnabledKey, Enabled ? 1 : 0);
                UnityEditor.EditorPrefs.SetInt(DetailsVisibleKey, DetailsVisible ? 1 : 0);
                m_EditorSaveAction?.Invoke();
            }
#endif

            public int Compare(CustomDrawer x, CustomDrawer y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (ReferenceEquals(null, y)) return 1;
                if (ReferenceEquals(null, x)) return -1;
                var diff = x.SortOrder - y.SortOrder;
                return diff != 0 ? diff : string.Compare(x.Name, y.Name, StringComparison.Ordinal);
            }

            public int CompareTo(CustomDrawer other) => Compare(this, other);
        }
    }
}
#endif
