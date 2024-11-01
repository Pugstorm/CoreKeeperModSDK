#if UNITY_EDITOR
using System;
using Unity.Entities.Conversion;
using UnityEngine;

namespace Unity.Scenes.Editor.Tests
{
    [Serializable]
    public struct TestLiveConversionSettings
    {
        [SerializeField] bool _wasLiveConversionEnabled;
        [SerializeField] LiveConversionSettings.ConversionMode _previousConversionMode;
        [SerializeField] private bool _previousLiveConversionSceneViewShowRuntime;

        public void Setup(bool isBakingEnabled = false)
        {
            _wasLiveConversionEnabled = LiveConversionEditorSettings.LiveConversionEnabled;
            LiveConversionEditorSettings.LiveConversionEnabled = true;
            _previousConversionMode = LiveConversionSettings.Mode;
            LiveConversionSettings.TreatIncrementalConversionFailureAsError = true;
            LiveConversionSettings.EnableInternalDebugValidation = true;
            LiveConversionSettings.Mode = LiveConversionSettings.ConversionMode.IncrementalConversionWithDebug;

            _previousLiveConversionSceneViewShowRuntime = LiveConversionEditorSettings.LiveConversionSceneViewShowRuntime;
            LiveConversionEditorSettings.LiveConversionSceneViewShowRuntime = true;
        }

        public void TearDown()
        {
            LiveConversionEditorSettings.LiveConversionEnabled = _wasLiveConversionEnabled;
            LiveConversionSettings.TreatIncrementalConversionFailureAsError = false;
            LiveConversionSettings.EnableInternalDebugValidation = false;
            LiveConversionSettings.Mode = _previousConversionMode;

            LiveConversionEditorSettings.LiveConversionSceneViewShowRuntime = _previousLiveConversionSceneViewShowRuntime;
        }
    }
}
#endif
