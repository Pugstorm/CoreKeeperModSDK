using System.Collections.Generic;
using Unity.Properties;
using Unity.Entities.UI;
using Unity.Scenes;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class AspectInspectionContext : InspectionContext
    {
        public PropertyElement Root;
    }

    abstract class AspectElementBase : BindableElement
    {
        public string DisplayName { get; protected set; }
        [CreateProperty] public string Path { get; protected set; }
    }

    sealed class AspectElement<TAspect> : AspectElementBase, IBinding
    {
        static readonly Dictionary<string, string> s_DisplayNames = new Dictionary<string, string>();
        EntityInspectorContext Context { get; }
        EntityAspectsCollectionContainer Container { get; }
        readonly PropertyElement m_Content;

        public AspectElement(IProperty property, EntityInspectorContext context, ref TAspect value)
        {
            binding = this;
            name = property.Name;
            bindingPath = property.Name;
            DisplayName = GetDisplayName(property.Name);
            Path = property.Name;
            Context = context;
            Container = Context.AspectsCollectionContainer;
            m_Content = CreateContent(property, ref value);
        }

        void OnAspectChanged(BindingContextElement element, PropertyPath path)
        {
            var aspect = element.GetTarget<TAspect>();
            var container = Container;

            if (container.IsReadOnly)
                return;

            PropertyContainer.SetValue(ref container, Path, aspect);
            EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
        }

        PropertyElement CreateContent<TValue>(IProperty property, ref TValue value)
        {
            Resources.Templates.Inspector.InspectorStyle.AddStyles(this);
            Resources.Templates.DotsEditorCommon.AddStyles(this);

            var aspectType = property.GetType().GetGenericArguments()[0];
            InspectorUtility.CreateAspectHeader(this, aspectType, DisplayName);

            var foldout = this.Q<Foldout>(className: UssClasses.Inspector.Component.Header);
            var content = new PropertyElement();
            foldout.contentContainer.Add(content);
            content.AddContext(new AspectInspectionContext {Root = content});
            content.SetTarget(value);
            content.OnChanged += OnAspectChanged;

            foldout.contentContainer.AddToClassList(UssClasses.Inspector.Component.Container);
            if (Container.IsReadOnly)
                InspectorUtility.SetUnityBaseFieldInputsEnabled(content, false);

            return content;
        }

        void IBinding.PreUpdate() { }

        void IBinding.Update()
        {
            var container = Container;
            if (!Context.World.IsCreated || !Context.EntityManager.SafeExists(Container.Entity))
            {
                RemoveFromHierarchy();
                return;
            }

            if (PropertyContainer.TryGetValue<EntityAspectsCollectionContainer, TAspect>(ref container, Path, out var aspect))
                m_Content.SetTarget(aspect);
        }

        void IBinding.Release() { }

        static string GetDisplayName(string propertyName)
        {
            if (!s_DisplayNames.TryGetValue(propertyName, out var displayName))
            {
                s_DisplayNames[propertyName] = displayName = ContentUtilities.NicifyTypeName(propertyName);
            }

            return displayName;
        }
    }
}
