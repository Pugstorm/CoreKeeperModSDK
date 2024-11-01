using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class SystemQueriesView : FoldoutWithActionButton
    {
        SystemQueriesViewData m_QueriesViewData;

        public SystemQueriesView(in SystemQueriesViewData data)
        {
            Resources.Templates.SystemQueriesView.AddStyles(this);
            HeaderIcon.AddToClassList(UssClasses.SystemQueriesView.Icon);
            MatchingCount.Hide();
            ActionButton.AddToClassList(UssClasses.SystemQueriesView.GoTo);

            ActionButton.RegisterCallback<MouseDownEvent, SystemQueriesView>((evt, @this) =>
            {
                evt.StopPropagation();
#if !UNITY_2023_2_OR_NEWER
                evt.PreventDefault();
#endif
                Analytics.SendEditorEvent(Analytics.Window.Inspector, Analytics.EventType.RelationshipGoTo, Analytics.GoToSystemDestination);
                SystemScheduleWindow.HighlightSystem(@this.Data.SystemProxy);
                ContentUtilities.ShowSystemInspectorContent(@this.Data.SystemProxy);
            }, this);

            Update(data);
        }

        public SystemQueriesViewData Data => m_QueriesViewData;

        public void Update(in SystemQueriesViewData data)
        {
            if (!m_QueriesViewData.SystemProxy.Valid || !m_QueriesViewData.Equals(data))
            {
                UpdateIcon(m_QueriesViewData.Kind, data.Kind);
                m_QueriesViewData = data;
                HeaderName.text = m_QueriesViewData.SystemName;

                SetValueWithoutNotify(false);
            }

            var ui = this.Query<QueryView>().ToList();

            var i = 0;
            for (; i < ui.Count && i < data.Queries.Length; i++)
            {
                ui[i].Update(data.Queries[i]);
            }

            for (; i < data.Queries.Length; i++)
            {
                var queryView = new QueryView(data.Queries[i]);
                queryView.HeaderName.style.unityFontStyleAndWeight = FontStyle.Normal;
                Add(queryView);
            }

            for (; i < ui.Count; i++)
            {
                ui[i].RemoveFromHierarchy();
            }
        }

        internal static string GetClassForKind(SystemQueriesViewData.SystemKind kind) => kind switch
        {
            SystemQueriesViewData.SystemKind.Unmanaged => "unmanaged-system",
            SystemQueriesViewData.SystemKind.CommandBufferBegin => "begin-command-buffer",
            SystemQueriesViewData.SystemKind.CommandBufferEnd => "end-command-buffer",
            _ => string.Empty
        };

        void UpdateIcon(SystemQueriesViewData.SystemKind previousKind, SystemQueriesViewData.SystemKind newKind)
        {
            if (previousKind == newKind)
                return;

            HeaderIcon.RemoveFromClassList(GetClassForKind(previousKind));
            HeaderIcon.AddToClassList(GetClassForKind(newKind));
        }
    }
}
