using Unity.Properties;
using UnityEditor;
using UnityEngine;

namespace Unity.Editor.Legacy
{
    sealed partial class RuntimeComponentsDrawer : IVisitContravariantPropertyAdapter<Object>
    {
        public void Visit<TContainer>(in VisitContext<TContainer> context, ref TContainer container, Object value)
        {
            var type = value ? value.GetType() : typeof(Object);
            GUI.enabled = false;
            EditorGUILayout.ObjectField(GetDisplayName(context.Property), value, type, true);
            GUI.enabled = true;
        }
    }
}
