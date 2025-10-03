using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build
{
    /// <summary>
    /// Data builder context object for Addressables.
    /// </summary>
    public class AddressablesDataBuilderInput
    {
        /// <summary>
        /// The main addressables settings object.
        /// </summary>
        public AddressableAssetSettings AddressableSettings { get; private set; }

        /// <summary>
        /// Build target group.
        /// </summary>
        public BuildTargetGroup TargetGroup { get; private set; }

        /// <summary>
        /// Build target.
        /// </summary>
        public BuildTarget Target { get; private set; }

        /// <summary>
        /// Player build version.
        /// </summary>
        public string PlayerVersion { get; set; }

        /// <summary>
        /// Bool to signify if profiler events should be broadcast.
        /// </summary>
        public bool ProfilerEventsEnabled { get; private set; }

        /// <summary>
        /// Registry of files created during the build
        /// </summary>
        public FileRegistry Registry { get; private set; }

        /// <summary>
        /// can be used in testing to append a suffix to file paths
        /// </summary>
        public string PathSuffix = string.Empty;

        /// <summary>
        /// The name of the default Runtime Settings file.
        /// </summary>
        public string RuntimeSettingsFilename = "settings.json";

        /// <summary>
        /// The name of the default Runtime Catalog file.
        /// </summary>
        public string RuntimeCatalogFilename =
#if ENABLE_BINARY_CATALOG
            "catalog.bin";
#else
            "catalog.json";
#endif
        /// <summary>
        /// The asset content state of a previous build.  This allows detection of deltas with the current build content state.  This will be
        /// null in standard builds.  This is only set during content update builds.
        /// </summary>
        public AddressablesContentState PreviousContentState { get; set; }


        /// <summary>
        /// Creates a default context object with values taken from the AddressableAssetSettings parameter.
        /// </summary>
        /// <param name="settings">The settings object to pull values from.</param>
        public AddressablesDataBuilderInput(AddressableAssetSettings settings)
        {
            string version = string.Empty;
            if (settings == null)
            {
                Debug.LogError("Attempting to set up AddressablesDataBuilderInput with null settings.");
            }
            else
                version = settings.PlayerBuildVersion;

            SetAllValues(settings,
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                EditorUserBuildSettings.activeBuildTarget,
                version);
        }

        /// <summary>
        /// Creates a default context object with values taken from the AddressableAssetSettings parameter.
        /// </summary>
        /// <param name="settings">The settings object to pull values from.</param>
        /// <param name="playerBuildVersion">The player build version.</param>
        public AddressablesDataBuilderInput(AddressableAssetSettings settings, string playerBuildVersion)
        {
            if (settings == null)
            {
                Debug.LogError("Attempting to set up AddressablesDataBuilderInput with null settings.");
            }

            SetAllValues(settings,
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                EditorUserBuildSettings.activeBuildTarget,
                playerBuildVersion);
        }

        internal void SetAllValues(AddressableAssetSettings settings, BuildTargetGroup buildTargetGroup, BuildTarget buildTarget, string playerBuildVersion)
        {
            AddressableSettings = settings;

            TargetGroup = buildTargetGroup;
            Target = buildTarget;
            PlayerVersion = playerBuildVersion;
            ProfilerEventsEnabled = ProjectConfigData.PostProfilerEvents;
            Registry = new FileRegistry();
            PreviousContentState = null;
        }

        internal bool IsBuildAndRelease = false;
        internal bool IsContentUpdateBuild = false;

        internal IBuildLogger Logger { get; set; }
    }
}
