using System;
using JetBrains.Annotations;
using Unity.Properties;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class ComponentContentProvider : ContentProvider
    {
        // System.Type is not serializable by default, so we use the assembly qualified name in order to reconstruct the type.
        // TODO: Support type migration through [FormallySerializedAs], [MovedFrom] and [FormerName]
        [CreateProperty, HideInInspector] string m_AssemblyQualifiedTypeName;

        Type m_ComponentType;

        public Type ComponentType
        {
            get => m_ComponentType;
            set
            {
                if (value == m_ComponentType)
                    return;

                try
                {
                    // Inside of a try/catch because this method will throw an exception if the type is not valid.
                    if (TypeManager.GetTypeIndex(value) != TypeIndex.Null)
                        m_ComponentType = value;

                    m_AssemblyQualifiedTypeName = m_ComponentType != null
                        ? m_ComponentType.AssemblyQualifiedName
                        : null;
                }
                catch (ArgumentException ex)
                {
                    // Gracefully recover
                    Debug.LogException(ex);
                    m_ComponentType = null;
                    m_AssemblyQualifiedTypeName = null;
                }
            }
        }

        protected override ContentStatus GetStatus()
        {
            if (ComponentType != null)
                return ContentStatus.ContentReady;

            // If either of those are null or empty, we won't be able to recover previous state.
            if (string.IsNullOrEmpty(m_AssemblyQualifiedTypeName))
                return ContentStatus.ContentUnavailable;

            if (ComponentType == null)
            {
                // Try to load the type from it's name. If it fails, we likely won't be able to recover previous state.
                ComponentType = Type.GetType(m_AssemblyQualifiedTypeName);
                if (ComponentType == null)
                    return ContentStatus.ContentUnavailable;
            }

            return ComponentType == null
                ? ContentStatus.ContentNotReady
                : ContentStatus.ContentReady;
        }

        public override string Name { get; } = L10n.Tr("Component");
        public override object GetContent()
        {
            return this;
        }
    }

    [UsedImplicitly]
    class ComponentContentInspector : PropertyInspector<ComponentContentProvider>
    {
        public override VisualElement Build()
        {
            var element = new PropertyElement();
            Resources.AddCommonVariables(element);
            Resources.Templates.ContentProvider.Component.AddStyles(element);

            var content = new ComponentContent(Target.ComponentType);
            element.SetTarget(new ComponentContentDisplay(content));
            return element;
        }
    }
}
