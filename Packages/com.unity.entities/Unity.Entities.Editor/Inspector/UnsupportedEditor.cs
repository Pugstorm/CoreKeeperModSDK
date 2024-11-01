using JetBrains.Annotations;
using Unity.Editor.Bridge;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using UnityObject = UnityEngine.Object;

namespace Unity.Entities.Editor
{
    abstract class UnsupportedEditor : UnityEditor.Editor
    {
        protected bool m_Initialized;
        protected VisualElement m_Root;

        protected override void OnHeaderGUI()
        {
            // Intentionally overriden to avoid drawing the default header.
        }

        public override VisualElement CreateInspectorGUI()
        {
            if (m_Initialized)
                return m_Root;

            m_Root = new VisualElement();
            m_Initialized = true;

            Resources.AddCommonVariables(m_Root);
            Resources.Templates.Inspector.UnsupportedInspectorStyle.Clone(m_Root);

            var icon = m_Root.Q<VisualElement>(name: UssClasses.Inspector.UnsupportedInspector.Names.Icon);
            icon.AddToClassList(ItemIconClass);

            var itemName = m_Root.Q<Label>(name: UssClasses.Inspector.UnsupportedInspector.Names.Name);
            itemName.text = ItemName;

            var bodyText = m_Root.Q<Label>(name: UssClasses.Inspector.UnsupportedInspector.Names.BodyText);
            bodyText.text = BodyText;

            return m_Root;
        }

        protected abstract string ItemIconClass { get; }
        protected abstract string ItemName { get; }
        protected abstract string BodyText { get; }
    }

    class UnsupportedEntityEditor : UnsupportedEditor
    {
        static readonly string k_BodyText = L10n.Tr("This Entity only exists at runtime.");
        static readonly string k_InvalidEntity = L10n.Tr("Invalid Entity");
        EntitySelectionProxy m_SelectionContext;

        protected override string ItemIconClass => UssClasses.Inspector.UnsupportedInspector.Classes.EntityIcon;

        protected override string ItemName =>
            CachedProxy is { Exists : true } proxy
                ? proxy.World.EntityManager.GetName(proxy.Entity)
                : k_InvalidEntity;

        protected override string BodyText => k_BodyText;

        EntitySelectionProxy m_CachedProxy;
        protected EntitySelectionProxy CachedProxy
        {
            get
            {
                if (m_CachedProxy == null)
                {
                    m_CachedProxy = target is EntitySelectionProxy
                        ? target as EntitySelectionProxy
                        : m_SelectionContext;
                }

                return m_CachedProxy;
            }
        }

        [UsedImplicitly]
        internal void InternalSetContextObject(UnityObject context)
        {
            m_SelectionContext = context as EntitySelectionProxy;
        }
    }

    class UnsupportedPrefabEntityEditor : UnsupportedEntityEditor
    {
        static readonly string k_RootBodyText = L10n.Tr("This Entity is a Prefab instance and only exists at runtime.");
        static readonly string k_PartBodyText = L10n.Tr("This Entity is part of a Prefab instance hierarchy that only exists at runtime.");
        static readonly string k_CallToActionLabel = L10n.Tr("Edit Prefab Asset");

        string m_AssetPath;
        bool m_IsRoot;
        bool m_ValuesInitialized;

        protected override string ItemIconClass
            => m_IsRoot
                ? UssClasses.Inspector.UnsupportedInspector.Classes.PrefabEntityIcon
                : UssClasses.Inspector.UnsupportedInspector.Classes.EntityIcon;

        protected override string BodyText
            => m_IsRoot
                ? k_RootBodyText
                : k_PartBodyText;

        public override VisualElement CreateInspectorGUI()
        {
            if (m_Initialized)
                return m_Root;

            var hasValidTarget = false;

            if (CachedProxy is { Exists : true } proxy)
            {
                var authoringObject = proxy.World.EntityManager.Debug.GetAuthoringObjectForEntity(proxy.Entity);

                if (authoringObject != null)
                {
                    m_AssetPath = PrefabUtilityBridge.GetAssetPathOfSourcePrefab(authoringObject, out m_IsRoot);
                    hasValidTarget = true;
                }
            }

            var root = base.CreateInspectorGUI();

            if (hasValidTarget)
            {
                var button = new Button(() =>
                {
                    SelectionBridge.SetSelection(AssetDatabase.LoadAssetAtPath<UnityObject>(m_AssetPath));
                }) { text = k_CallToActionLabel };

                button.AddToClassList(UssClasses.Inspector.UnsupportedInspector.Classes.Button);

                root.Add(button);
            }

            return root;
        }
    }

    class UnsupportedGameObjectEditor : UnsupportedEditor
    {
        static readonly string k_BodyText = L10n.Tr("This GameObject only exists at runtime.");
        static readonly string k_InvalidGameObject = L10n.Tr("Invalid GameObject");

        protected override string ItemIconClass => UssClasses.Inspector.UnsupportedInspector.Classes.GameObjectIcon;

        protected override string ItemName
        {
            get
            {
                var go = target as GameObject;
                return go == null
                    ? k_InvalidGameObject
                    : go.name;
            }
        }

        protected override string BodyText => k_BodyText;
    }

    class InvalidSelectionEditor : UnsupportedEditor
    {
        static readonly string k_BodyText = L10n.Tr("The selection is not valid anymore, please re-select.");
        static readonly string k_InvalidSelection = L10n.Tr("Invalid Selection");
        protected override string ItemIconClass => UssClasses.Inspector.UnsupportedInspector.Classes.GameObjectIcon;
        protected override string ItemName => k_InvalidSelection;
        protected override string BodyText => k_BodyText;
    }

    class InvalidEntityEditor : UnsupportedEditor
    {
        static readonly string k_BodyText = L10n.Tr("The entity does not exist anymore, please unlock inspector if it is locked or re-select.");
        static readonly string k_InvalidSelection = L10n.Tr("Invalid Entity");
        protected override string ItemIconClass => UssClasses.Inspector.UnsupportedInspector.Classes.EntityIcon;
        protected override string ItemName => k_InvalidSelection;
        protected override string BodyText => k_BodyText;
    }
}
