using Unity.Editor.Bridge;
using UnityEditor;

namespace Unity.Entities.Editor
{
    partial class HierarchyWindow
    {
        static readonly DataMode[] k_EditorDataModes = {DataMode.Authoring, DataMode.Mixed, DataMode.Runtime};
        static readonly DataMode[] k_RuntimeDataModes = {DataMode.Authoring, DataMode.Mixed, DataMode.Runtime};

        // Internal for test purpose.
        internal static DataMode[] GetSupportedDataModes()
            => EditorApplication.isPlaying
                ? k_RuntimeDataModes
                : k_EditorDataModes;

        static DataMode GetPreferredDataMode()
            => EditorApplication.isPlaying
                ? DataMode.Mixed
                : DataMode.Authoring;

        void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange is not (PlayModeStateChange.EnteredEditMode or PlayModeStateChange.EnteredPlayMode))
                return;

            var preferredDataMode = GetPreferredDataMode();
            dataModeController.UpdateSupportedDataModes(GetSupportedDataModes(), preferredDataMode);
        }

        void OnDataModeChanged(DataModeChangeEventArgs arg)
        {
            var newDataMode = arg.nextDataMode;
            RequestGlobalSelectionRestoration();
            m_Hierarchy.SetDataMode(newDataMode);
            SelectionBridge.SetSelection(Selection.activeObject, Selection.activeContext, newDataMode);

            Analytics.SendEditorEvent(
                Analytics.Window.Hierarchy,
                arg.changedThroughUI
                    ? Analytics.EventType.DataModeManualSwitch
                    : Analytics.EventType.DataModeSwitch,
                newDataMode.ToString());
        }
    }
}
