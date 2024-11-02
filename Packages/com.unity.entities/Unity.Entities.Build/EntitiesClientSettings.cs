using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Build
{
    /// <summary>
    /// The baking settings for the default entities setup.
    /// </summary>
    /// <remarks>The com.unity.netcode package will add more settings for different build targets.</remarks>
    [FilePath("ProjectSettings/EntitiesClientSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class EntitiesClientSettings : ScriptableSingleton<EntitiesClientSettings>, IEntitiesPlayerSettings
    {
        [SerializeField]
        public BakingSystemFilterSettings FilterSettings;

        static Hash128 s_Guid;
        public Hash128 GUID
        {
            get
            {
                if (!s_Guid.IsValid)
                    s_Guid = UnityEngine.Hash128.Compute(GetFilePath());
                return s_Guid;
            }
        }

        public string CustomDependency => GetFilePath();
        void IEntitiesPlayerSettings.RegisterCustomDependency()
        {
            if (!AssetDatabase.IsAssetImportWorkerProcess())
            {
                var hash = GetHash();
                AssetDatabase.RegisterCustomDependency(CustomDependency, hash);
            }
        }

        public UnityEngine.Hash128 GetHash()
        {
            // initialize the hash with the file GUID to have a non-zero hash when there are no filter settings
            var hash = (UnityEngine.Hash128)GUID;
            if (FilterSettings?.ExcludedBakingSystemAssemblies != null)
                foreach (var assembly in FilterSettings.ExcludedBakingSystemAssemblies)
                {
                    var guid = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(assembly.asset));
                    hash.Append(ref guid);
                }

            return hash;
        }

        public BakingSystemFilterSettings GetFilterSettings()
        {
            return FilterSettings;
        }

        public string[] GetAdditionalScriptingDefines()
        {
            return Array.Empty<string>();
        }

        ScriptableObject IEntitiesPlayerSettings.AsScriptableObject() => instance;

        internal void Save()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
                return;

            if (!EditorApplication.isUpdating)
            {
                ((IEntitiesPlayerSettings) this).RegisterCustomDependency();
            }

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
            //We can't call the RegisterCustomDependency here. We still save the asset (because it must be)
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

    internal class ClientSettings : DotsPlayerSettingsProvider
    {
        VisualElement m_BuildSettingsContainer;

        public override int Importance
        {
            get{ return 0; }
        }

        public override DotsGlobalSettings.PlayerType GetPlayerType()
        {
            return DotsGlobalSettings.PlayerType.Client;
        }

        protected override IEntitiesPlayerSettings DoGetSettingAsset()
        {
            return EntitiesClientSettings.instance;
        }

        public override void OnActivate(DotsGlobalSettings.PlayerType type, VisualElement rootElement)
        {
            rootElement.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            rootElement.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            var so = new SerializedObject(EntitiesClientSettings.instance);

            m_BuildSettingsContainer = new VisualElement();
            m_BuildSettingsContainer.AddToClassList("target");

            var label = new Label("Baking");
            m_BuildSettingsContainer.Add(label);

            var targetS = new VisualElement();
            targetS.AddToClassList("target-Settings");

            var prop = so.FindProperty("FilterSettings.ExcludedBakingSystemAssemblies");
            var propField = new PropertyField(prop);
            propField.BindProperty(prop);
            propField.RegisterCallback<ChangeEvent<string>>(
                evt =>
                {
                    EntitiesClientSettings.instance.FilterSettings.SetDirty();
                });
            targetS.Add(propField);

            m_BuildSettingsContainer.Add(targetS);
            m_BuildSettingsContainer.Bind(so);

            rootElement.Add(m_BuildSettingsContainer);
        }

        static void OnAttachToPanel(AttachToPanelEvent evt)
        {
            // The ScriptableSingleton<T> is not directly editable by default.
            // Change the hideFlags to make the SerializedObject editable.
            EntitiesClientSettings.instance.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        }

        static void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            // Restore the original flags
            EntitiesClientSettings.instance.hideFlags = HideFlags.HideAndDontSave;
            EntitiesClientSettings.instance.Save();
        }

        public override string[] GetExtraScriptingDefines()
        {
            return Array.Empty<string>();
        }
    }
}
