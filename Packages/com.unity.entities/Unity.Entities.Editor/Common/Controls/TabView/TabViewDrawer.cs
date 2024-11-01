using System.Collections;
using JetBrains.Annotations;
using Unity.Entities.UI;
using Unity.Serialization.Editor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    [UsedImplicitly]
    class TabViewDrawer : PropertyInspector<ITabContent[], TabViewAttribute>
    {
        public override VisualElement Build()
        {
            var tabView = new TabView();
            var tabs = new TabContent[Target.Length];
            var initializedTabs = new BitArray(tabs.Length);
            var state = SessionState<State>.GetOrCreate(DrawerAttribute.Id);

            if ((uint)state.TabIndex >= Target.Length)
                state.TabIndex = 0;

            for (var i = 0; i < Target.Length; i++)
            {
                var tab = Target[i];
                if (tab is TabContent tc)
                {
                    tabs[i] = tc;
                    initializedTabs[i] = true;
                }
                else
                {
                    var tabContent = new TabContent { TabName = tab.TabName };
                    tabs[i] = tabContent;

                    if (state.TabIndex != i)
                        continue;

                    var element = new PropertyElement();
                    element.SetTarget(tab);
                    tabContent.Add(element);
                    initializedTabs[i] = true;
                }

                tab.OnTabVisibilityChanged(state.TabIndex == i);
            }

            tabView.Tabs = tabs;
            tabView.value = state.TabIndex;

            tabView.RegisterCallback((ChangeEvent<int> evt, (TabViewAttribute attribute, ITabContent[] tabs, TabContent[] tabContents, BitArray initializedTabs) args) =>
            {
                if (evt.target.GetType() != typeof(TabView))
                    return;

                var tabIndex = evt.newValue;
                SessionState<State>.GetOrCreate(args.attribute.Id).TabIndex = tabIndex;

                if (!args.initializedTabs[tabIndex])
                {
                    var element = new PropertyElement();
                    element.SetTarget(args.tabs[tabIndex]);
                    args.tabContents[tabIndex].Add(element);
                    args.initializedTabs[tabIndex] = true;
                }

                for (var i = 0; i < args.tabs.Length; i++)
                {
                    args.tabs[i].OnTabVisibilityChanged(i == tabIndex);
                }

            }, (DrawerAttribute, Target, tabs, initializedTabs));

            return tabView;
        }

        // internal for tests
        internal class State
        {
            public int TabIndex;
        }
    }
}
