using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    static class Constants
    {
        public const string EditorDefaultResourcesPath = "Packages/" + Resources.PackageId + "/Editor Default Resources/";

        public static readonly Vector2 MinWindowSize = new Vector2(200, 200); // Matches SceneHierarchy's min size

        public static class MenuItems
        {
            const string k_WindowRoot = "Window/Entities/";

            // A note on Window priorities:
            // Since this is ECS tooling, we want to keep the E, C, and S windows together, in that order, at the top
            // Hierarchy = E here – it's where we display all the Entities
            // The rest doesn't specially matter, but we keep Archetypes next to the above because it is thematically close to those
            const int k_BaseWindowPriority = 3006;

            public const string HierarchyWindow = k_WindowRoot + "Hierarchy";
            public const int HierarchyWindowPriority = k_BaseWindowPriority;

            public const string ComponentsWindow = k_WindowRoot + "Components";
            public const int ComponentsWindowPriority = k_BaseWindowPriority + 1;

            public const string SystemScheduleWindow = k_WindowRoot + "Systems";
            public const int SystemScheduleWindowPriority = k_BaseWindowPriority + 2;

            public const string ArchetypesWindow = k_WindowRoot + "Archetypes";
            public const int ArchetypesWindowPriority = k_BaseWindowPriority + 3;

            public const string JournalingWindow = k_WindowRoot + "Journaling";
            public const int JournalingWindowPriority = k_BaseWindowPriority + 4;
        }

        public static class ListView
        {
            public const int ItemHeight = 16;
        }

        public static class Settings
        {
            public const string EditorSettingsRoot = "Entities";
            public const string Inspector = "Inspector";
            public const string Hierarchy = "Hierarchy Window";
            public const string SystemsWindow = "Systems Window";
            public const string Advanced = "Advanced";
            public const string Baking = "Baking";
            public const string Migration = "Migration";

#if !DISABLE_ENTITIES_JOURNALING
            public const string Journaling = "Journaling";
#else
            public const string Journaling = "Journaling (disabled via define)";
#endif
        }

        public static class SystemSchedule
        {
            public const string k_SystemDependencyToken = "sd:";
            public const string k_Dash = "-";
        }

        public static class ComponentSearch
        {
            public const string Token = "c";
            public const string All = "all";
            public const string None = "none";
            public const string Any = "any";
            public const string Op = "=";
            public const string TokenCaseInsensitive = "cC";
            public const string TokenOp = "c=";
            public const string SharedComponentPrefix = "#";
        }

        public static class Hierarchy
        {
            public const string EntityIndexToken = "ei";
            public const string EntityIndexTokenOpEqual = "ei=";
            public const string NodeKindOpEqual = "k=";
            public const string KindToken = "k";
        }

        public static class Inspector
        {
            public const int MaxVisibleSystemCount = 50;
            public const double CoolDownTime = 300;
            public static readonly string EmptyRelationshipMessage = L10n.Tr("No relationships.");
            public static readonly string EmptyAspectsMessage = L10n.Tr("No aspects.");
            public const string k_ComponentToken = "c:";
            public const string k_AspectToken = "aspect:";
        }
    }
}
