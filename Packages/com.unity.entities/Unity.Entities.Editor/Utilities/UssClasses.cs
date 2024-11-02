namespace Unity.Entities.Editor
{
    static class UssClasses
    {
        public static class DotsEditorCommon
        {
            public const string CommonResources = "common-resources";
            public const string SettingsIcon = "settings-icon";
            public const string SearchIconContainer = "search-icon-container";
            public const string SearchIcon = "search-icon";

            public const string SearchFieldContainer = "search-field-container";
            public const string SearchField = "search-field";

            const string CenteredMessageElementBase = "centered-message-element";
            public const string CenteredMessageElementTitle = CenteredMessageElementBase + "__title";
            public const string CenteredMessageElementMessage = CenteredMessageElementBase + "__message";

            public const string UnityToolbarMenuArrow = "unity-toolbar-menu__arrow";
            public const string UnityBaseField = "unity-base-field";
        }

        public static class ComponentView
        {
            const string k_Base = "component-view";

            public const string Icon = k_Base + "__icon";
            public const string Name = k_Base + "__name";
            public const string AccessMode = k_Base + "__access-mode";
            public const string GoTo = k_Base + "__goto-icon";
        }

        public static class FoldoutWithActionButton
        {
            const string k_Base = "foldout-with-action-button";

            public const string ToggleHeaderHoverStyle = k_Base + "__toggle-header--hover";
            public const string Icon = k_Base + "__icon";
            public const string Name = k_Base + "__name";
            public const string ButtonContainer = k_Base + "__button-container";
            public const string Button = k_Base + "__button";
            public const string Count = k_Base + "__count";
            public const string Toggle = k_Base + "__toggle";
            public const string ToggleInput = k_Base + "__toggle-input";
        }

        public static class FoldoutWithoutActionButton
        {
            const string k_Base = "foldout-without-action-button";

            public const string Icon = k_Base + "__icon";
            public const string Name = k_Base + "__name";
            public const string Count = k_Base + "__count";
            public const string ToggleInput = k_Base + "__toggle-input";
            public const string ToggleNoBorder = k_Base + "__toggle-no-border";
        }

        public static class QueryView
        {
            const string k_Base = "query-view";
            public const string EmptyMessage = k_Base + "__empty";
            public const string Toggle = k_Base + "__toggle";
            public const string ToggleContent = k_Base + "__toggle-content";
            public const string HideActionIcon = k_Base + "__hide-action-icon";
            public const string FoldoutContentPadding = k_Base + "__foldout-content-padding";
            public const string HeaderBold = k_Base + "__header-bold";
        }

        public static class SystemDependencyView
        {
            const string k_Base = "system-dependency-view";

            public const string Name = k_Base + "__name";
            public const string GotoButtonContainer = k_Base + "__button-container";
        }

        public static class SystemListView
        {
            const string k_Base = "system-list-view";

            public const string ContentElement = k_Base + "__content";
            public const string MoreLabel = k_Base + "__more-label";
            public const string SystemIcon = k_Base + "__system-icon";
        }

        public static class SystemQueriesView
        {
            const string k_Base = "system-queries-view";

            public const string Icon = k_Base + "__icon";
            public const string GoTo = k_Base + "__goto-icon";
        }

        public static class QueryWithEntities
        {
            const string k_Base = "query-with-entities";
            public const string Icon = k_Base + "__icon";
            public const string OpenQueryWindowButton = k_Base + "__open-query-window-button";
            public const string SeeAllContainer = k_Base + "__see-all-container";
            public const string SeeAllButton = k_Base + "__see-all";
            public const string ToggleContent = k_Base + "__toggle-content";
            public const string EntityIcon = k_Base + "__entity-icon";
        }

        public static class EntityView
        {
            const string k_Base = "entity-view";
            public const string EntityName = k_Base + "__name";
            public const string GoTo = k_Base + "__goto";
        }

        public static class ComponentAttribute
        {
            const string k_Base = "component-attribute";
            public const string Name = k_Base + "__name";
            public const string Value = k_Base + "__value";
        }

        public static class SystemScheduleWindow
        {
            const string SystemSchedule = "system-schedule";
            public const string WindowRoot = SystemSchedule + "__root";

            public static class Toolbar
            {
                const string k_Base = SystemSchedule + "-toolbar";
                public const string LeftSide = k_Base + "__left";
                public const string RightSide = k_Base + "__right";
            }

            public static class TreeViewHeader
            {
                const string Header = SystemSchedule + "-header";
                public const string System = Header + "__system-label";
                public const string World = Header + "__world-label";
                public const string Namespace = Header + "__namespace-label";
                public const string EntityCount = Header + "__entity-count-label";
                public const string Time = Header + "__time-label";
            }

            public static class Items
            {
                const string Base = SystemSchedule + "-item";
                public const string Icon = Base + "__icon";
                public const string EnabledContainer = Base + "__state-toggle-container";
                public const string StateToggle = Base + "__state-toggle";
                public const string SystemName = Base + "__name-label";
                public const string WorldName = Base + "__world-label";
                public const string Namespace = Base + "__namespace-label";
                public const string Matches = Base + "__entity-count-label";
                public const string Time = Base + "__time-label";

                public const string SystemNameColumn = Base + "__column-system-name";
                public const string WorldNameColumn = Base + "__column-world-name";
                public const string NamespaceColumn = Base + "__column-namespace";
                public const string EntityCountColumn = Base + "__column-entity-count";
                public const string TimeColumn = Base + "__column-time";

                public const string SystemToggleEnabled = Base + "__toggle-enabled";
                public const string SystemToggleMixed = Base + "__toggle-mixed";

                public const string SystemIcon = Icon + "--system";
                public const string SystemGroupIcon = Icon + "--system-group";
                public const string BeginCommandBufferIcon = Icon + "--begin-command-buffer";
                public const string EndCommandBufferIcon = Icon + "--end-command-buffer";
                public const string UnmanagedSystemIcon = Icon + "--unmanaged-system";

                public const string SystemNameNormal = Base + "__name-label-normal";
                public const string SystemNameBold = Base + "__name-label-bold";
            }
        }

        public static class Hierarchy
        {
            const string k_Hierarchy = "hierarchy";
            public const string Root = k_Hierarchy;
            public const string Loading = k_Hierarchy + "-loading";
            public const string PrefabStage = k_Hierarchy + "-prefab-stage";

            public static class Toolbar
            {
                const string k_Toolbar = k_Hierarchy + "-toolbar";
                public const string LeftSide = k_Toolbar + "__left";
                public const string RightSide = k_Toolbar + "__right";
            }

            public static class Item
            {
                const string k_Item = k_Hierarchy + "-item";
                public const string SceneNode = k_Item + "__scene-node";
                public const string SubSceneNode = k_Item + "__subscene-node";
                public const string Icon = k_Item + "__icon";
                public const string IconScene = Icon + "--scene";
                public const string IconEntity = Icon + "--entity";
                public const string IconGameObject = Icon + "--gameobject";
                public const string Name = k_Item + "__name";
                public const string SubSceneState = k_Item + "__subscene-state-label";
                public const string NameScene = Name + "--scene";
                public const string SystemButton = k_Item + "__system-button";
                public const string PingGameObjectButton = k_Item + "__ping-gameobject-button";
                public const string PrefabStageButton = k_Item + "__prefab-stage-button";
                public const string SubSceneButton = k_Item + "__subscene-button";
                public const string VisibleOnHover = k_Item + "__visible-on-hover";
                public const string Prefab = k_Item + "--prefab";
                public const string PrefabRoot = k_Item + "--prefab-root";
                public const string RuntimeModeIndent = "runtime";
                public const string PrefabOverrideIndent = "prefab";
            }
        }

        public static class Inspector
        {
            public const string EntityInspector = "entity-inspector";
            public const string EmptyMessage = "empty-inspector-label";

            public static class EntityHeader
            {
                public const string OriginatingGameObject = "originating-game-object";
            }

            public static class Icons
            {
                const string k_Base = "inspector-icon";
                public const string Small = k_Base + "--small";
            }

            public static class RuntimeBar
            {
                public const string RuntimeYellowBar = EntityInspector + "__runtime-bar";
                public const string RuntimeYellowBarAdded = EntityInspector + "__runtime-bar-added";
            }

            public static class Component
            {
                const string k_Base = "component";
                public const string Container = k_Base + "-container";
                public const string Header = k_Base + "-header";
                public const string Name = k_Base + "-name";
                public const string Icon = k_Base + "-icon";
                public const string Enabled = k_Base + "-enabled";
                public const string Category = k_Base + "-category";
                public const string Menu = k_Base + "-menu";
                public const string AspectIcon = "aspect-icon";
                public const string Shrink = "shrink";
            }

            public static class ComponentTypes
            {
                const string k_PostFix = "-data";
                public const string Component = "component" + k_PostFix;
                public const string Tag = "tag-component" + k_PostFix;
                public const string SharedComponent = "shared-component" + k_PostFix;
                public const string ChunkComponent = "chunk-component" + k_PostFix;
                public const string ManagedComponent = "managed-component" + k_PostFix;
                public const string BufferComponent = "buffer-component" + k_PostFix;
            }

            public static class AspectsTab
            {
                const string k_TabBase = EntityInspector + "-aspects-tab";
                public const string Content = k_TabBase + "__content";
            }

            public static class RelationshipsTab
            {
                const string k_TabBase = EntityInspector + "-relationships-tab";
                public const string Container = k_TabBase + "__container";
                public const string SearchField = k_TabBase + "__search-field";
            }

            public static class ComponentsTab
            {
                const string k_TabBase = EntityInspector + "-components-tab";
                public const string SearchField = k_TabBase + "__search-field";
            }

            public static class UnsupportedInspector
            {
                public static class Names
                {
                    const string k_RootName = "UnsupportedInspector";
                    public const string Icon = k_RootName + "-ItemIcon";
                    public const string Name = k_RootName + "-ItemName";
                    public const string BodyText = k_RootName + "-BodyText";
                }

                public static class Classes
                {
                    const string k_RootClass = "unsupported-inspector";
                    const string k_RootIcon = k_RootClass + "__item-icon";
                    public const string EntityIcon = k_RootIcon + "--entity";
                    public const string GameObjectIcon = k_RootIcon + "--game-object";
                    public const string PrefabEntityIcon = k_RootIcon + "--prefab-entity";
                    public const string Button = k_RootClass + "__button";
                }

            }
        }

        public static class Content
        {
            public static class Query
            {
                public static class EntityQuery
                {
                    const string k_EntityQuery = "entity-query";

                    public const string Container = k_EntityQuery + "__container";
                    public const string HeaderMainTitle = k_EntityQuery + "__header__main-title__label";
                    public const string HeaderSubTitle = k_EntityQuery + "__header__subtitle";
                    public const string HeaderGoTo = k_EntityQuery + "__header__subtitle__goto";
                    public const string ListView = k_EntityQuery + "__list-view";
                    public const string SearchContainer = k_EntityQuery + "__search-element-container";

                    public const string SystemQuery = "system-query";
                    public const string ComponentQuery = "component-query";

                }

                public static class EntityInfo
                {
                    const string k_EntityInfo = "entity-info";
                    public const string Container = k_EntityInfo + "__container";
                    public const string Icon = k_EntityInfo + "__icon";
                    public const string Name = k_EntityInfo + "__name";
                }
            }

            public static class SystemInspector
            {
                public const string SystemContainer = "system__container";
                public const string SystemQueriesEmpty = "system-queries__empty";

                public static class SystemIcons
                {
                    public const string SystemIconBig = "system__icon--big";
                    public const string EcbBeginIconBig = "ecb-begin__icon--big";
                    public const string EcbEndIconBig = "ecb-end__icon--big";
                    public const string GroupIconBig = "group__icon--big";
                }
            }
        }

        public static class UIToolkit
        {
            public const string Disabled = "unity-disabled";

            public static class BaseField
            {
                const string k_Base = "unity-base-field";
                public const string Input = k_Base + "__input";
            }

            public static class ObjectField
            {
                public const string ObjectSelector = "unity-object-field__selector";
                public const string Display = "unity-object-field-display";
            }

            public static class Toggle
            {
                const string k_Base = "unity-toggle";
                public const string Text = k_Base + "__text";
                public const string Input = k_Base + "__input";
                public const string Checkmark = k_Base + "__checkmark";
            }
        }

        public static class SearchElement
        {
            const string k_Base = "unity-entities";

            public const string Root = k_Base + "__search-element";
        }

        public static class SearchElementFilterPopup
        {
            const string k_Base = "unity-entities";

            public const string Root = k_Base + "__search-element-filter-popup";
            public const string ChoiceButton = Root + "__choice-button";
            public const string ChoiceName = Root + "__choice-name";
            public const string ChoiceToken = Root + "__choice-token";
        }
    }
}
