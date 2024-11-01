using System;
using Unity.Entities;
using Unity.Properties;
using UnityEditor;

namespace Unity.Editor.Legacy
{
    sealed partial class RuntimeComponentsDrawer : IVisitPropertyAdapter
    {
        public void Visit<TContainer, TValue>(in VisitContext<TContainer, TValue> context, ref TContainer container, ref TValue value)
        {
            if (typeof(TValue).IsEnum)
            {
                // @TODO Handle mixed values.

                var options = Enum.GetNames(typeof(TValue));
                var local = value;

                var index = Array.FindIndex(options, name => name == local.ToString());

                EditorGUILayout.Popup
                    (
                        GetDisplayName(context.Property),
                        index,
                        options
                    );

                return;
            }

            if (null == value)
            {
                PropertyField(context.Property, value);
                return;
            }

            if (typeof(TValue).IsGenericType && typeof(TValue).GetGenericTypeDefinition() == typeof(BlobAssetReference<>))
            {
                return;
            }

            context.ContinueVisitation(ref container, ref value);
        }
    }
}
