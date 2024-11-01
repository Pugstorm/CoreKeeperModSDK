using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class QueryWithEntitiesView : FoldoutWithActionButton
    {
        readonly QueryWithEntitiesViewData m_Data;
        readonly VisualElement m_EntitiesContainer;
        readonly VisualElement m_SeeAll;
        static readonly string k_Query = L10n.Tr("Query");

        public QueryWithEntitiesView(in QueryWithEntitiesViewData data)
        {
            m_Data = data;
            Resources.Templates.QueryWithEntities.AddStyles(this);
            this.Q(className: "unity-foldout__content").AddToClassList(UssClasses.QueryWithEntities.ToggleContent);

            HeaderName.text = $"{k_Query} #{data.QueryOrder}";
            HeaderIcon.AddToClassList(UssClasses.QueryWithEntities.Icon);
            MatchingCount.text = "0";

            ActionButton.AddToClassList(UssClasses.QueryWithEntities.OpenQueryWindowButton);
            ActionButton.RegisterCallback<MouseDownEvent, QueryWithEntitiesView>((evt, @this) =>
            {
                evt.StopPropagation();
#if !UNITY_2023_2_OR_NEWER
                evt.PreventDefault();
#endif
                OpenQueryWindow(@this.m_Data, EntityQueryContentTab.Entities);
            }, this);

            m_EntitiesContainer = new VisualElement();
            Add(m_EntitiesContainer);

            m_SeeAll = new VisualElement();
            m_SeeAll.AddToClassList(UssClasses.QueryWithEntities.SeeAllContainer);
            var btn = new Button(() => OpenQueryWindow(m_Data, EntityQueryContentTab.Entities)) { text = L10n.Tr("See All...") };
            btn.AddToClassList(UssClasses.QueryWithEntities.SeeAllButton);
            m_SeeAll.Add(btn);
            m_SeeAll.Hide();
            Add(m_SeeAll);

            SetValueWithoutNotify(true);
        }

        static void OpenQueryWindow(QueryWithEntitiesViewData data, EntityQueryContentTab tab)
        {
            QueryWindowHelper.OpenNewWindow(data.World, data.Query, data.SystemProxy, data.QueryOrder, tab);
        }

        public void Update()
        {
            if (!m_Data.Update())
                return;

            MatchingCount.text = m_Data.TotalEntityCount.ToString();
            m_EntitiesContainer.Clear();
            foreach (var entity in m_Data.Entities)
            {
                m_EntitiesContainer.Add(new EntityView(entity));
            }

            m_SeeAll.SetVisibility(m_Data.TotalEntityCount > m_Data.Entities.Count);
        }
    }
}
