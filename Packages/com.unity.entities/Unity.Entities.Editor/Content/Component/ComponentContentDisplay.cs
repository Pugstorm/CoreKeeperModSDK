using JetBrains.Annotations;
using Unity.Properties;

namespace Unity.Entities.Editor
{
    readonly struct ComponentContentDisplay
    {
        [CreateProperty, UsedImplicitly]
        Header Header { get; }

        [CreateProperty, TabView("ComponentInspector"), UsedImplicitly]
        ITabContent[] Tabs { get; }

        public ComponentContentDisplay(ComponentContent content)
        {
            Header = new Header(ComponentsUtility.GetComponentDisplayName(TypeUtility.GetTypeDisplayName(content.Type)), "component-type__icon");

            Tabs = new ITabContent[]
            {
                new ComponentAttributes(content.Type),
                new ComponentRelationships(content.Type)
            };
        }
    }
}
