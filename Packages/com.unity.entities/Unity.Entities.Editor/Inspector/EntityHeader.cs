using JetBrains.Annotations;
using Unity.Properties;
using Unity.Entities.UI;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Wrapper type to display the <see cref="Entity"/> inspector header.
    /// </summary>
    readonly struct EntityHeader
    {
        readonly EntityInspectorContext m_Context;

        public EntityHeader(EntityInspectorContext context)
        {
            m_Context = context;
        }

        [CreateProperty, UsedImplicitly]
        public string Name
        {
            get => m_Context.GetTargetName();
            set => m_Context.SetTargetName(value);
        }

        [CreateProperty, UsedImplicitly] public int Index => m_Context.Entity.Index;
        [CreateProperty, UsedImplicitly] public int Version => m_Context.Entity.Version;
        [CreateProperty, UsedImplicitly] public UnityEngine.Object ConvertedFrom => m_Context.GetSourceObject();

        [UsedImplicitly]
        class EntityHeaderInspector : PropertyInspector<EntityHeader>
        {
            const string k_EntityName = "Entity Name";
            const string k_IndexVersion = "Index/Version";

            public override VisualElement Build()
            {
                var root = Resources.Templates.Inspector.EntityHeader.Clone();

                root.Q<TextField>(k_EntityName).isDelayed = true;

                var originatingGO = root.Q<ObjectField>(className: UssClasses.Inspector.EntityHeader.OriginatingGameObject);

                // Prevent users from dragging a GameObject on the field.
                originatingGO.RegisterCallback<ChangeEvent<Object>, ObjectField>(ForceTargetGameObject, originatingGO);

                // Hide the button that allows to display the object selector window.
                originatingGO.Q(className: UssClasses.UIToolkit.ObjectField.ObjectSelector).Hide();

                var context = GetContext<EntityInspectorContext>();

                if (context?.IsReadOnly ?? true)
                {
                    originatingGO.AddToClassList(UssClasses.UIToolkit.Disabled);
                    root.Q(k_EntityName).SetEnabled(false);
                }

                root.Q(k_IndexVersion).SetEnabled(false);

                return root;
            }

            void ForceTargetGameObject(ChangeEvent<Object> evt, ObjectField field)
            {
                field.SetValueWithoutNotify(Target.ConvertedFrom);
            }
        }
    }
}
