using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    static class BindingUtilities
    {
        static readonly MethodInfo BaseBinderMethod;

        static readonly Dictionary<TypePairKey, MethodInfo> s_RegistrationMethods =
            new Dictionary<TypePairKey, MethodInfo>();

        static BindingUtilities()
        {
            BaseBinderMethod = typeof(BindingUtilities)
                .GetMethod(nameof(SetCallbacks), BindingFlags.Static | BindingFlags.NonPublic);
            if (null == BaseBinderMethod)
                throw new InvalidOperationException($"Could not find private static method `{nameof(SetCallbacks)}<,>` in the {nameof(BindingUtilities)} class.");
        }

        public static void Bind<TValue>(VisualElement element, ref TValue value, PropertyPath path, BindingContextElement root)
        {
            switch (element)
            {
                case BindingContextElement propertyElement:
                    GuiFactory.SetCallbacks(ref value, path, root, propertyElement);
                    break;
                case BaseField<TValue> field:
                    GuiFactory.SetCallbacks(ref value, path, root, field);
                    break;
                case Label label:
                    GuiFactory.SetCallbacks(ref value, path, root, label);
                    break;
                default:
                    // Use reflection to figure out if we can map it.
                    TrySetCallbacksThroughReflection(element, ref value, path, root);
                    break;
            }
        }

        static void TrySetCallbacksThroughReflection<TValue>(VisualElement element, ref TValue value, PropertyPath path, BindingContextElement root)
        {
            var type = element.GetType();
            var baseFieldType = GetBaseFieldType(type);

            if (null == baseFieldType)
                return;

            var fieldType = baseFieldType.GenericTypeArguments[0];
            var key = new TypePairKey(fieldType, typeof(TValue));
            if (!s_RegistrationMethods.TryGetValue(key, out var method))
            {
                s_RegistrationMethods[key] = method = BaseBinderMethod.MakeGenericMethod(fieldType, typeof(TValue));
            }

            method.Invoke(null, new object[] {value, element, path, root});
        }

        static void SetCallbacks<TFieldType, TValue>(ref TValue value, BaseField<TFieldType> field, PropertyPath path, BindingContextElement root)
        {
            GuiFactory.SetCallbacks(ref value, path, root, field);
        }

        static Type GetBaseFieldType(Type type)
        {
            var generic = typeof(BaseField<>);
            while (type != null && type != typeof(object))
            {
                var current = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                if (generic == current)
                {
                    return type;
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
