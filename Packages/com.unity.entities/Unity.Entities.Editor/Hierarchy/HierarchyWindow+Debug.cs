using JetBrains.Annotations;
using Unity.Editor.Bridge;
using Unity.Properties;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    partial class HierarchyWindow : IHasCustomMenu
    {
        void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
        {
            if (Unsupported.IsDeveloperMode())
            {
                foreach (var value in System.Enum.GetValues(typeof(Hierarchy.OperationModeType)))
                {
                    var mode = (Hierarchy.OperationModeType) value;
                    menu.AddItem(new GUIContent(mode.ToString()), m_Hierarchy.OperationMode == mode, () => { m_Hierarchy.OperationMode = mode; });
                }

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Stats..."), false, () => SelectionUtility.ShowInWindow(new DebugContentProvider()));
            }

            if (!Unsupported.IsDeveloperMode())
            {
                menu.AddItem(EditorGUIUtility.TrTextContent("Reload Window"), false, userData => ((EditorWindow)userData).ReloadHostView(), this);
            }
        }

        /// <summary>
        /// The <see cref="ContentProvider"/> for the internal hierarchy debug properties.
        ///
        /// @FIXME There is an issue during a domain reload. The window must be closed and re-opened.
        /// </summary>
        class DebugContentProvider : ContentProvider
        {
            public override string Name => "DOTS Hierarchy Stats";
            public override object GetContent() => new DebugContent(HasOpenInstances<HierarchyWindow>() ? GetWindow<HierarchyWindow>() : null);

            protected override ContentStatus GetStatus()
                => !HasOpenInstances<HierarchyWindow>() ? ContentStatus.ReloadContent : ContentStatus.ContentReady;
        }

        /// <summary>
        /// The data being shown in the hierarchy properties window. Currently we show the serialized state and the stats.
        /// </summary>
        class DebugContent
        {
            readonly HierarchyWindow m_Context;

            [CreateProperty, UsedImplicitly] public HierarchyState State => m_Context.m_Hierarchy.State;
            [CreateProperty, UsedImplicitly] public HierarchyStats Stats => m_Context.m_Hierarchy.Stats;

            public DebugContent(HierarchyWindow context) => m_Context = context;

            [UsedImplicitly]
            class Inspector : PropertyInspector<DebugContent>
            {
            }
        }
    }
}
