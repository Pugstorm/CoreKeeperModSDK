#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities.Build;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.NetCode.Hybrid
{
    public enum NetCodeClientTarget
    {
        [Tooltip("Build a client-only player.")]
        Client = 0,
        [Tooltip("Build a client-server player.")]
        ClientAndServer = 1
    }

    /// <summary>
    /// The <see cref="IEntitiesPlayerSettings"/> baking settings to use for client only builds. You can assign the <see cref="GUID"/>
    /// to the <see cref="Unity.Scenes.SceneSystemData.BuildConfigurationGUID"/> to instrument the asset import worker to bake the
    /// scene using this setting.
    /// </summary>
    [FilePath("ProjectSettings/NetCodeClientSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class NetCodeClientSettings : ScriptableSingleton<NetCodeClientSettings>, IEntitiesPlayerSettings, INetCodeConversionTarget
    {
        NetcodeConversionTarget INetCodeConversionTarget.NetcodeTarget => NetcodeConversionTarget.Client;

        [SerializeField]
        private BakingSystemFilterSettings FilterSettings;

        [SerializeField]
        private string[] AdditionalScriptingDefines = Array.Empty<string>();

        [SerializeField]
        public NetCodeClientTarget ClientTarget = NetCodeClientTarget.ClientAndServer;

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
            //But the depedency is going to be update when the scriptable is re-enabled.
            if (AssetDatabase.IsAssetImportWorkerProcess())
                return;
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
        private VisualElement m_rootElement;

        public override int Importance
        {
            get { return 1; }
        }

        public override DotsGlobalSettings.PlayerType GetPlayerType()
        {
            return DotsGlobalSettings.PlayerType.Client;
        }

        protected override void DoReloadAsset()
        {
            ReloadAsset(NetCodeClientSettings.instance);
            ReloadAsset(NetCodeClientAndServerSettings.instance);
        }

        public override void OnActivate(DotsGlobalSettings.PlayerType type, VisualElement rootElement)
        {
            rootElement.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            rootElement.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            m_rootElement = new VisualElement();
            m_rootElement.SetEnabled(type == DotsGlobalSettings.PlayerType.Client);

            var targetElement = UpdateUI();

            m_rootElement.Add(targetElement);
            rootElement.Add(m_rootElement);
        }

        static void OnAttachToPanel(AttachToPanelEvent evt)
        {
            // The ScriptableSingleton<T> is not directly editable by default.
            // Change the hideFlags to make the SerializedObject editable.
            NetCodeClientSettings.instance.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            NetCodeClientAndServerSettings.instance.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        }

        static void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            NetCodeClientSettings.instance.hideFlags = HideFlags.HideAndDontSave;
            NetCodeClientAndServerSettings.instance.hideFlags = HideFlags.HideAndDontSave;
            NetCodeClientSettings.instance.Save();
            NetCodeClientAndServerSettings.instance.Save();
        }

        VisualElement UpdateUI()
        {
            var targetElement = new VisualElement();
            targetElement.name = "target";
            targetElement.AddToClassList("target");

            var so = new SerializedObject(GetSettingAsset().AsScriptableObject());
            targetElement.Bind(so);
            so.Update();

            var label = new Label("Client");
            targetElement.Add(label);

            var targetS = new VisualElement();
            targetS.AddToClassList("target-Settings");

            // PropertyField didn't seem to work here.
            var field = new EnumField("NetCode Client Target", NetCodeClientSettings.instance.ClientTarget);
            field.tooltip = "Denotes whether or not Server data and logic is included in a client build (when making a client build). Doing so allows the client executable to self-host (i.e. \"Client Host\") a multiplayer game.";
            targetS.Add(field);

            var prop = so.FindProperty("FilterSettings.ExcludedBakingSystemAssemblies");
            var propField = new PropertyField(prop);
            propField.BindProperty(prop);
            propField.RegisterCallback<ChangeEvent<string>>(
                evt =>
                {
                    NetCodeClientSettings.instance.GetFilterSettings().SetDirty();
                });
            targetS.Add(propField);

            var propExtraDefines = so.FindProperty("AdditionalScriptingDefines");
            var propExtraDefinesField = new PropertyField(propExtraDefines);
            propExtraDefinesField.name = "Client Extra Defines";
            targetS.Add(propExtraDefinesField);

            field.RegisterCallback<ChangeEvent<Enum>>(evt =>
            {
                m_rootElement.Remove(targetElement);
                var oldFlags = NetCodeClientSettings.instance.hideFlags;
                var serializedObject = new SerializedObject(NetCodeClientSettings.instance);
                var serializedProperty = serializedObject.FindProperty("ClientTarget");
                serializedProperty.enumValueIndex = (int)(NetCodeClientTarget)evt.newValue;
                var hideFlags = serializedObject.FindProperty("m_ObjectHideFlags");
                hideFlags.intValue = (int)HideFlags.HideAndDontSave;
                if (serializedObject.ApplyModifiedProperties())
                    NetCodeClientSettings.instance.Save();
                NetCodeClientSettings.instance.hideFlags = oldFlags;
                var newTargetElement = UpdateUI();
                m_rootElement.Add(newTargetElement);
            });
            targetElement.Add(targetS);
            so.ApplyModifiedProperties();

            return targetElement;
        }

        public override string[] GetExtraScriptingDefines()
        {
            IEnumerable<string> extraDefines = GetSettingAsset().GetAdditionalScriptingDefines();
            var netCodeClientTarget = NetCodeClientSettings.instance.ClientTarget;
#if !NETCODE_NDEBUG
            if (EditorUserBuildSettings.development)
                extraDefines = extraDefines.Append("NETCODE_DEBUG");
#endif
            if (netCodeClientTarget == NetCodeClientTarget.ClientAndServer)
                return extraDefines.ToArray();
            if (netCodeClientTarget == NetCodeClientTarget.Client)
                return extraDefines.Append("UNITY_CLIENT").ToArray();
            return Array.Empty<string>();
        }

        protected override IEntitiesPlayerSettings DoGetSettingAsset()
        {
            var netCodeClientSettings = NetCodeClientSettings.instance;
            if (netCodeClientSettings.ClientTarget == NetCodeClientTarget.Client)
                return netCodeClientSettings;
            else
                return NetCodeClientAndServerSettings.instance;
        }
    }
}
#endif
