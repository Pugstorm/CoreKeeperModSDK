using JetBrains.Annotations;
using Unity.Properties;
using Unity.Entities.UI;
using Unity.Serialization;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    readonly struct EntityViewData
    {
        static readonly string k_InvalidEntity = L10n.Tr("Invalid Entity");

        public EntityViewData(World world, Entity entity)
        {
            this.World = world;
            Entity = entity;
            EntityGuid = this.World.EntityManager.HasComponent<EntityGuid>(Entity)
                ? this.World.EntityManager.GetComponentData<EntityGuid>(Entity)
                : EntityGuid.Null;
        }

        [DontCreateProperty] public readonly World World;
        [DontCreateProperty] public readonly Entity Entity;
        [DontCreateProperty] public readonly EntityGuid EntityGuid;

        [CreateProperty, UsedImplicitly, DontSerialize] public string EntityName
        {
            get
            {
                if (!World.IsCreated)
                    return k_InvalidEntity;
                var name = World.EntityManager.GetName(Entity);
                return string.IsNullOrEmpty(name)
                    ? Entity.ToString()
                    : name;
            }
        }

        [CreateProperty, UsedImplicitly, DontSerialize] public int InstanceId => EntityGuid.OriginatingId;
        [CreateProperty, UsedImplicitly, DontSerialize] public int Index => Entity.Index;
        [CreateProperty, UsedImplicitly, DontSerialize] public int ComponentCount => World.EntityManager.GetComponentCount(Entity);

        [UsedImplicitly]
        class Inspector : PropertyInspector<EntityViewData>
        {
            public override VisualElement Build()
            {
                var root = new VisualElement();
                Resources.Templates.ContentProvider.EntityInfo.AddStyles(root);

                root.AddToClassList(UssClasses.Content.Query.EntityInfo.Container);
                root.RegisterCallback<ClickEvent, Inspector>((evt, inspector) =>
                {
                    if (evt.clickCount >= 1)
                        EntitySelectionProxy.SelectEntity(inspector.Target.World, inspector.Target.Entity);
                }, this);

                var icon = new VisualElement();
                icon.AddToClassList(UssClasses.Content.Query.EntityInfo.Icon);
                root.Add(icon);

                var entity = new Label { bindingPath = "EntityName" };
                entity.AddToClassList(UssClasses.Content.Query.EntityInfo.Name);
                root.Add(entity);
                return root;
            }
        }
    }
}
