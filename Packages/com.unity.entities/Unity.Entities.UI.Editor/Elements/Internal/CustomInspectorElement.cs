using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    class CustomInspectorElement : VisualElement, IBindable, IBinding
    {
        internal class DefaultInspectorElement : VisualElement{}

        PropertyPath m_BasePath;
        readonly BindingContextElement m_Root;
        PropertyPath m_RelativePath = new PropertyPath();
        PropertyPath m_AbsolutePath = new PropertyPath();
        VisualElement m_Content;

        public IInspector Inspector { get; }
        public IBinding binding { get; set; }

        public string bindingPath { get; set; }
        bool HasInspector { get; }
        public bool IsRootInspector => HasInspector && Inspector is IRootInspector;

        public CustomInspectorElement(PropertyPath basePath, IInspector inspector, BindingContextElement root)
        {
            m_Root = root;
            binding = this;
            m_BasePath = basePath;
            name = TypeUtility.GetTypeDisplayName(inspector.Type);
            Inspector = inspector;
            try
            {
                m_Content = Inspector.Build();

                if (null == m_Content)
                    return;

                HasInspector = true;

                // If `IInspector.Build` was not overridden, it returns this element as its content.
                if (this != m_Content)
                {
                    Add(m_Content);
                    RegisterBindings(m_Content);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        void IBinding.PreUpdate()
        {
            // Nothing to do.
        }

        void IBinding.Update()
        {
            if (!HasInspector || !m_Root.IsPathValid(m_BasePath))
                return;

            try
            {
                Inspector.Update();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        void IBinding.Release()
        {
            // Nothing to do.
        }

        void RegisterBindings(VisualElement content)
        {
            if (content is CustomInspectorElement && content != this)
                return;

            var popRelativePartCount = 0;
            if (content is BindableElement b && !string.IsNullOrEmpty(b.bindingPath))
            {
                if (b.bindingPath != ".")
                {
                    var previousCount = m_RelativePath.Length;
                    m_RelativePath = PropertyPath.Combine(m_RelativePath, b.bindingPath);
                    m_AbsolutePath = PropertyPath.Combine(m_AbsolutePath, b.bindingPath);
                    popRelativePartCount = m_RelativePath.Length - previousCount;
                }

                if (Inspector.IsPathValid(m_RelativePath))
                    RegisterBindings(Inspector, m_RelativePath, content, m_Root);
                else if (Inspector.IsPathValid(m_AbsolutePath))
                    RegisterBindings(Inspector, m_AbsolutePath, content, m_Root);
                m_AbsolutePath = default;
            }

            if (!(content is BindingContextElement) && !(content is DefaultInspectorElement))
                foreach (var child in content.Children())
                    RegisterBindings(child);

            for(var i = 0; i < popRelativePartCount; ++i)
            {
                m_RelativePath = PropertyPath.Pop(m_RelativePath);
            }
        }

        static void RegisterBindings(IInspector inspector, PropertyPath pathToValue, VisualElement toBind, BindingContextElement root)
        {
            var fullPath = PropertyPath.Combine(inspector.PropertyPath, pathToValue);
            root.RegisterBindings(fullPath, toBind);
        }
    }
}
