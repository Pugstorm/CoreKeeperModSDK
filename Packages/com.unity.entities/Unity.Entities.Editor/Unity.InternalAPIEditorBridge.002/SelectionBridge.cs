using System;
using System.Collections.Generic;
using UnityEditor;
using UnityObject = UnityEngine.Object;

namespace Unity.Editor.Bridge
{
    class SelectionBridge
    {
        public static event Action PostProcessSelectionMetaData;

        public static event Action<UnityObject, UnityObject, HashSet<DataMode>> DeclareDataModeSupport;

        [DeclareDataModeSupport]
        static void AddDataModeSupport(
            UnityObject activeSelection,
            UnityObject activeContext,
            HashSet<DataMode> supportedModes)
            => DeclareDataModeSupport?.Invoke(activeSelection, activeContext, supportedModes);

        [InitializeOnLoadMethod]
        static void Init()
        {
            Selection.postProcessSelectionMetadata += () => PostProcessSelectionMetaData?.Invoke();
        }

        public static DataMode DataModeHint => Selection.dataModeHint;

        public static void SetSelection(
            UnityObject activeObject,
            UnityObject activeContext = null,
            DataMode dataModeHint = default)
            => Selection.SetSelection(activeObject, activeContext, dataModeHint);

        public static void SetSelection(
            UnityObject[] selection,
            UnityObject activeObject = null,
            UnityObject activeContext = null,
            DataMode dataModeHint = default)
            => Selection.SetSelection(selection, activeObject, activeContext, dataModeHint);

        public static void UpdateSelectionMetaData(UnityObject newContext, DataMode newDataModeHint)
            => Selection.UpdateSelectionMetaData(newContext, newDataModeHint);
    }
}
