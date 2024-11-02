using UnityEditor;
using UnityEngine;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using System;
using System.Collections;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    [CustomPropertyDrawer(typeof(UnityObjectRef<>), true)]
    class UnityObjectRefPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();

            var targetObjectType = DetermineTargetType(fieldInfo.FieldType);
            var instanceIdName = $"{property.propertyPath}.Id.instanceId";
            var instanceIdProperty = property.serializedObject.FindProperty($"{instanceIdName}");
            var unityObjectRef = new UntypedUnityObjectRef { instanceId = (int)instanceIdProperty.intValue };

            var currObject = UnityEngine.Resources.InstanceIDToObject(unityObjectRef.instanceId);
            var objectField = new ObjectField
            {
                objectType = targetObjectType,
                allowSceneObjects = false,
                label = property.name
            };

            objectField.SetValueWithoutNotify(currObject);
            objectField.RegisterCallback((ChangeEvent<UnityEngine.Object> e) =>
            {
                instanceIdProperty.intValue = e.newValue.GetInstanceID();
                property.serializedObject.ApplyModifiedProperties();
            });
            container.Add(objectField);

            return container;
        }

        private static Type GetElementType(Type t)
        {
            if (t.IsGenericType)
                return t.GetGenericArguments()[0];
            return t;
        }

        internal static Type DetermineTargetType(Type t)
        {
            if (typeof(IEnumerable).IsAssignableFrom(t) && t.IsGenericType)
            {
                return GetElementType(t.GetGenericArguments()[0]);
            }
            else if (t.IsArray)
            {
                return GetElementType(t.GetElementType());
            }
            return GetElementType(t);
        }
    }
}
