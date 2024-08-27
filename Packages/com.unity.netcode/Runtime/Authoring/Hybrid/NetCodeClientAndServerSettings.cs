#if UNITY_EDITOR
using System;
using Unity.Entities.Build;
using UnityEditor;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UIElements;

namespace Unity.NetCode.Hybrid
{
    /// <summary>
    /// The <see cref="IEntitiesPlayerSettings"/> baking settings to use for server builds. You can assign the <see cref="GUID"/>
    /// to the <see cref="Unity.Scenes.SceneSystemData.BuildConfigurationGUID"/> to instrument the asset import worker to bake the
    /// scene using this setting.
    /// </summary>
    [FilePath("ProjectSettings/NetCodeClientAndServerSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class NetCodeClientAndServerSettings : ScriptableSingleton<NetCodeClientAndServerSettings>, IEntitiesPlayerSettings, INetCodeConversionTarget
    {
        NetcodeConversionTarget INetCodeConversionTarget.NetcodeTarget => NetcodeConversionTarget.ClientAndServer;

        [SerializeField] private BakingSystemFilterSettings FilterSettings;

        [SerializeField] private string[] AdditionalScriptingDefines = Array.Empty<string>();

        static Entities.Hash128 s_Guid;
        /// <inheritdoc/>
        public Entities.Hash128 GUID
        {
            get
            {
                if (!s_Guid.IsValid)
                    s_Guid = UnityEngine.Hash128.Compute(GetFilePath());
                return s_Guid;
            }
        }

        /// <inheritdoc/>
        public string CustomDependency => GetFilePath();
        /// <inheritdoc/>
        void IEntitiesPlayerSettings.RegisterCustomDependency()
        {
            if (!AssetDatabase.IsAssetImportWorkerProcess())
            {
                var hash = GetHash();
                AssetDatabase.RegisterCustomDependency(CustomDependency, hash);
            }
        }
        /// <inheritdoc/>
        public UnityEngine.Hash128 GetHash()
        {
            var hash = (UnityEngine.Hash128)GUID;
            if (FilterSettings?.ExcludedBakingSystemAssemblies != null)
                foreach (var assembly in FilterSettings.ExcludedBakingSystemAssemblies)
                {
                    var guid = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(assembly.asset));
                    hash.Append(ref guid);
                }
            foreach (var define in AdditionalScriptingDefines)
                hash.Append(define);
            return hash;
        }
        /// <inheritdoc/>
        public BakingSystemFilterSettings GetFilterSettings()
        {
            return FilterSettings;
        }
        /// <inheritdoc/>
        public string[] GetAdditionalScriptingDefines()
        {
            return AdditionalScriptingDefines;
        }
        /// <inheritdoc/>
        ScriptableObject IEntitiesPlayerSettings.AsScriptableObject() => instance;

        internal void Save()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
                return;
            ((IEntitiesPlayerSettings)this).RegisterCustomDependency();
            Save(true);
            AssetDatabase.Refresh();
        }

#if UNITY_2023_2_OR_NEWER
        private void OnEnable()
        {
            if (!AssetDatabase.IsAssetImportWorkerProcess())
            {
                ((IEntitiesPlayerSettings)this).RegisterCustomDependency();
            }
        }
#endif
        private void OnDisable()
        {
#if !UNITY_2023_2_OR_NEWER
            Save();
#else
            //But the depedency is going to be update when the scriptable is re-enabled.
            if (AssetDatabase.IsAssetImportWorkerProcess())
                return;
            Save(true);
            //This safeguard is necessary because the RegisterCustomDependency throw exceptions
            //if this is called when the editor is refreshing the database.
            if(!EditorApplication.isUpdating)
            {
                ((IEntitiesPlayerSettings)this).RegisterCustomDependency();
                AssetDatabase.Refresh();
            }
#endif
        }
    }
}
#endif
