using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    partial class DisplayContent
    {
        public ContentProvider Provider => Content.Provider;
        public readonly SerializableContent Content;
        public readonly DynamicInspectionContext InspectionContext = new DynamicInspectionContext();

        [NonSerialized] ContentStatus m_PreviousState;
        [NonSerialized] bool m_RequestQuit;

        [NonSerialized] VisualElement m_Root;
        [NonSerialized] PropertyElement m_ContentRoot;
        [NonSerialized] PropertyElement m_ContentNotReadyRoot;

        public bool IsValid
            => !m_RequestQuit && null != Provider && Provider.IsValid();

        public DisplayContent(SerializableContent content)
        {
            Content = content;
        }

        public VisualElement CreateGUI()
        {
            if (null == m_Root)
                m_Root = new VisualElement();

            m_Root.style.flexGrow = 1;
            m_ContentRoot = new PropertyElement();
            m_ContentRoot.OnChanged += (element, path) =>
            {
                Provider.OnContentChanged(new ContentProvider.ChangeContext(element));
            };
            m_ContentRoot.AddContext(InspectionContext);
            if (InspectionContext.ApplyInspectorStyling)
                m_ContentRoot.RegisterCallback<GeometryChangedEvent, VisualElement>((evt, element) => StylingUtility.AlignInspectorLabelWidth(element), m_ContentRoot);
            m_ContentNotReadyRoot = new PropertyElement();
            m_Root.contentContainer.Add(m_ContentRoot);
            m_Root.contentContainer.Add(m_ContentNotReadyRoot);
            m_ContentRoot.style.flexGrow = 1;
            return m_Root;
        }

        public void Update()
        {
            if (!IsValid)
                return;

            if (null != m_ContentRoot && InspectionContext.ApplyInspectorStyling)
                StylingUtility.AlignInspectorLabelWidth(m_ContentRoot);

            var state = Provider.MoveNext();
            if (m_PreviousState != state)
            {
                m_ContentRoot?.ClearTarget();
                switch (state)
                {
                    case ContentStatus.ContentUnavailable:
                        return;
                    case ContentStatus.ContentNotReady:
                        SetNotReadyContent();
                        break;
                    case ContentStatus.ContentReady:
                        SetTarget();
                        break;
                    case ContentStatus.ReloadContent:
                        SetNotReadyContent();
                        Content.Load();
                        var value = Provider?.GetContent();
                        if (null == value)
                        {
                            SetNotReadyContent();
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            m_PreviousState = state;
        }

        void SetNotReadyContent()
        {
            // Removing from the hierarchy here for consistency with the SetTarget() above.
            m_ContentRoot.RemoveFromHierarchy();
            m_ContentNotReadyRoot.RemoveFromHierarchy();
            m_ContentNotReadyRoot.SetTarget(new ContentNotReady(Provider));
            m_Root.contentContainer.Add(m_ContentNotReadyRoot);
        }

        void SetTarget()
        {
            try
            {
                var value = Provider.GetContent();
                if (null == value)
                {
                    Debug.LogError($"{TypeUtility.GetTypeDisplayName(Provider.GetType())}: Releasing content named '{Provider.Name}' because it returned null value.");
                    m_RequestQuit = true;
                    return;
                }

                // Removing from the hierarchy here because Unity will try to bind the elements to a serializedObject and
                // we want to use our own bindings. This will be fixed in UIToolkit directly.
                m_ContentRoot.RemoveFromHierarchy();
                m_ContentNotReadyRoot.RemoveFromHierarchy();

                var visitor = new SetTargetVisitor {Content = Content, InspectionContext = InspectionContext, Inspector = m_ContentRoot};
                PropertyContainer.Accept(visitor, ref value);
                m_Root.contentContainer.Add(m_ContentRoot);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{TypeUtility.GetTypeDisplayName(Provider.GetType())}: Releasing content named '{Provider.Name}' because it threw an exception.");
                m_RequestQuit = true;
                Debug.LogException(ex);
            }
        }
    }
}
