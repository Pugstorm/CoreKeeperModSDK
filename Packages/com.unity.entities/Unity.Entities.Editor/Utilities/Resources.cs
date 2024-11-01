using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    static class Resources
    {
        public static void AddCommonVariables(VisualElement rootElement)
        {
            Templates.Variables.AddStyles(rootElement);
            rootElement.AddToClassList("variables");
        }

        public const string PackageId = "com.unity.entities";

        public static class Templates
        {
            public static readonly VisualElementTemplate Variables = new(PackageId, "Common/variables");

            public static readonly VisualElementTemplate AutoComplete = new(PackageId, "Common/autocomplete");
            public static readonly VisualElementTemplate TabView = new(PackageId, "Controls/TabView/tab-view");

            public static readonly VisualElementTemplate SystemSchedule = new(PackageId, "SystemSchedule/system-schedule");
            public static readonly VisualElementTemplate SystemScheduleTreeViewHeader = new(PackageId, "SystemSchedule/system-schedule-header");
            public static readonly VisualElementTemplate SystemScheduleItem = new(PackageId, "SystemSchedule/system-schedule-item");
            public static readonly VisualElementTemplate SystemScheduleToolbar = new(PackageId, "SystemSchedule/system-schedule-toolbar");
            public static readonly VisualElementTemplate DotsEditorCommon = new(PackageId, "Common/dots-editor-common");
            public static readonly VisualElementTemplate CenteredMessageElement = new(PackageId, "Common/centered-message-element");

            public static readonly VisualElementTemplate DebugWindow = new(PackageId, "DebugWindows/debug-window");
            public static readonly VisualElementTemplate SystemsDebugWindow = new(PackageId, "DebugWindows/system-debug-window");

            public static readonly VisualElementTemplate ComponentView = new(PackageId, "Common/component-view");
            public static readonly VisualElementTemplate QueryView = new(PackageId, "Common/query-view");
            public static readonly VisualElementTemplate SystemDependencyView = new(PackageId, "Common/system-dependency-view");
            public static readonly VisualElementTemplate SystemListView = new(PackageId, "Common/system-list-view");
            public static readonly VisualElementTemplate SystemQueriesView = new(PackageId, "Common/system-queries-view");
            public static readonly VisualElementTemplate QueryWithEntities = new(PackageId, "Common/query-with-entities");
            public static readonly VisualElementTemplate EntityView = new(PackageId, "Common/entity-view");
            public static readonly VisualElementTemplate FoldoutWithActionButton = new(PackageId, "Common/foldout-with-action-button");
            public static readonly VisualElementTemplate FoldoutWithoutActionButton = new(PackageId, "Common/foldout-without-action-button");

            public static readonly VisualElementTemplate ComponentTypeView = new(PackageId, "Components/component-type-view");

            public static class Inspector
            {
                public static readonly VisualElementTemplate EntityInspector = new(PackageId, "Inspector/entity-inspector");
                public static readonly VisualElementTemplate EntityHeader = new(PackageId, "Inspector/entity-header");
                public static readonly VisualElementTemplate InspectorStyle = new(PackageId, "Inspector/inspector");
                public static readonly VisualElementTemplate ComponentHeader = new(PackageId, "Inspector/component-header");
                public static readonly VisualElementTemplate TagComponentElement = new(PackageId, "Inspector/tag-component-element");
                public static readonly VisualElementTemplate EntityField = new(PackageId, "Inspector/entity-field");
                public static readonly VisualElementTemplate ComponentsTab = new(PackageId, "Inspector/entity-inspector-components-tab");
                public static readonly VisualElementTemplate AspectsTab = new(PackageId, "Inspector/entity-inspector-aspects-tab");
                public static readonly VisualElementTemplate UnsupportedInspectorStyle = new(PackageId, "Inspector/unsupported-inspector");

                public static class RelationshipsTab
                {
                    public static readonly VisualElementTemplate Root = new(PackageId, "Inspector/entity-inspector-relationships-tab");
                }
            }

            public static class ContentProvider
            {
                public static readonly VisualElementTemplate Header = new(PackageId, "Content/header");
                public static readonly VisualElementTemplate EntityInfo = new(PackageId, "Content/entity-info");
                public static readonly VisualElementTemplate EntityQuery = new(PackageId, "Content/entity-query");
                public static readonly VisualElementTemplate EntityQueryHeader = new(PackageId, "Content/entity-query-header");
                public static readonly VisualElementTemplate System = new(PackageId, "Content/system");
                public static readonly VisualElementTemplate Component = new(PackageId, "Content/component");

                public static readonly VisualElementTemplate ComponentsWindow = new(PackageId, "Components/components-window");
                public static readonly VisualElementTemplate ComponentAttribute = new(PackageId, "Content/component-attribute");
            }

            public static class Hierarchy
            {
                public static readonly VisualElementTemplate Root = new(PackageId, "Hierarchy/hierarchy");
                public static readonly VisualElementTemplate Item = new(PackageId, "Hierarchy/hierarchy-item");
                public static readonly VisualElementTemplate Toolbar = new(PackageId, "Hierarchy/hierarchy-toolbar");
                public static readonly VisualElementTemplate Loading = new(PackageId, "Hierarchy/hierarchy-loading");
                public static readonly VisualElementTemplate PrefabStage = new(PackageId, "Hierarchy/hierarchy-prefab-stage");
            }

            public static readonly VisualElementTemplate SearchElement = new(PackageId, "Search/search-element");
            public static readonly VisualElementTemplate SearchElementFilterPopup = new(PackageId, "Search/search-element-filter-popup");
        }
    }
}
