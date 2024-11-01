using Unity.Entities.Editor.Inspectors;
using Unity.Entities.UI;
using Unity.Properties;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    abstract class ComponentElementBase : BindableElement
    {
        public int TypeIndex { get; private set; }
        public ComponentPropertyType Type { get; private set; }

        [CreateProperty] public string Path { get; private set; }
        protected string DisplayName { get; private set; }
        protected EntityInspectorContext Context { get; private set; }
        protected EntityContainer Container { get; private set; }

        protected ComponentElementBase(IComponentProperty property, EntityInspectorContext context)
        {
            TypeIndex = property.TypeIndex;
            Type = property.Type;
            Path = property.Name;
            DisplayName = ComponentsUtility.GetComponentDisplayName(property.Name);
            Context = context;
            Container = Context.EntityContainer;
        }

        protected PropertyElement CreateContent<TValue>(IComponentProperty property, ref TValue value)
        {
            Resources.Templates.Inspector.InspectorStyle.AddStyles(this);
            InspectorUtility.CreateComponentHeader(this, property.Type, DisplayName);

            var foldout = this.Q<Foldout>(className: UssClasses.Inspector.Component.Header);
            foldout.Q<Toggle>().AddManipulator(new ContextualMenuManipulator(evt => { OnPopulateMenu(evt.menu); }));

            if (!Context.IsReadOnly)
            {
                if (Type == ComponentPropertyType.ChunkComponent)
                    foldout.contentContainer.Add(new HelpBox("Chunk component data is shared between multiple entities", HelpBoxMessageType.Info));

                if (Type == ComponentPropertyType.SharedComponent)
                    foldout.contentContainer.Add(new HelpBox("Changing shared values will move entities between chunks", HelpBoxMessageType.Info));
            }

            var content = new PropertyElement();

            // We set user data to this root PropertyElement to indicate this is a live property displaying runtime data.
            // So that we can draw this property field with runtime bar being added.
            if (EditorApplication.isPlaying)
                content.userData = content;

            foldout.contentContainer.Add(content);
            content.AddContext(Context);
            content.SetTarget(value);
            content.OnChanged += OnComponentChanged;

            foldout.contentContainer.AddToClassList(UssClasses.Inspector.Component.Container);

            var container = Container;
            if (container.IsReadOnly)
            {
                SetReadonly(foldout);
                foldout.RegisterCallback<ClickEvent, EntityInspectorContext>(OnClicked, Context, TrickleDown.TrickleDown);
            }

            return content;
        }

        protected virtual void SetReadonly(VisualElement root)
        {
            root.contentContainer.SetEnabled(false);
            root.Q<Toggle>(className: UssClasses.Inspector.Component.Enabled).SetEnabled(false);
        }

        protected abstract void OnComponentChanged(BindingContextElement element, PropertyPath path);

        protected abstract void OnPopulateMenu(DropdownMenu menu);

        static void OnClicked(ClickEvent evt, EntityInspectorContext context)
        {
            var element = (VisualElement)evt.currentTarget;
            OnClicked(evt, context, element);
        }

        static void OnClicked(ClickEvent evt, EntityInspectorContext context, VisualElement current)
        {
            switch (current)
            {
                case Foldout foldout:
                    if (foldout.enabledInHierarchy || !foldout.Q<Toggle>().worldBound.Contains(evt.position))
                        break;
                    foldout.value = !foldout.value;
                    break;
                case ObjectField objectField:
                    var display = objectField.Q(className: UssClasses.UIToolkit.ObjectField.Display);
                    if (null == display)
                        break;
                    if (!display.worldBound.Contains(evt.position))
                        break;

                    if (evt.clickCount == 1)
                        EditorGUIUtility.PingObject(objectField.value);
                    else
                    {
                        var value = objectField.value;
                        if (null != value && value)
                            Selection.activeObject = value;
                    }
                    break;
                case EntityField entityField:
                    var input = entityField.Q(className: "unity-entity-field__input");
                    if (null == input)
                        break;
                    if (!input.worldBound.Contains(evt.position))
                        break;

                    if (evt.clickCount > 1)
                    {
                        var world = context.World;
                        if (null == world || !world.IsCreated)
                            break;
                        if (!context.EntityManager.SafeExists(entityField.value))
                            break;

                        EntitySelectionProxy.SelectEntity(context.World, entityField.value);
                    }
                    break;
            }

            for (var i = 0; i < current.childCount; ++i)
            {
                OnClicked(evt, context, current[i]);
            }
        }
    }
}
