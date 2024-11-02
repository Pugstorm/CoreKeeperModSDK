using Unity.Entities;
using Unity.Properties;

namespace Unity.Editor.Legacy
{
    sealed partial class RuntimeComponentsDrawer :
        IVisitPropertyAdapter<Entity>
    {
        public void Visit<TContainer>(in VisitContext<TContainer, Entity> context, ref TContainer container, ref Entity value)
        {
            LabelField(context.Property, $"Index: <b>{value.Index}</b> Version: <b>{value.Version}</b>", IsMixedValue(context.Property, value));
        }
    }
}
