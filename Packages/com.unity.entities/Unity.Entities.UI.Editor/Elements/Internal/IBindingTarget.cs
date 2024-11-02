using System;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    interface IBindingTarget
    {
        Type DeclaredType { get; }
        Type TargetType { get; }
        BindingContextElement Root { get; }
        InspectorVisitor Visitor { get; }
        bool TryGetTarget<T>(out T t);
        bool IsPathValid(PropertyPath path);
        void ReloadAtPath(PropertyPath path, VisualElement current);
        void RegisterBindings(PropertyPath path, VisualElement element);
        void VisitAtPath(IPropertyVisitor visitor, PropertyPath path);
        void VisitAtPath(PropertyPath path, VisualElement parent);
        void VisitAtPath(InspectorVisitor visitor, PropertyPath path, VisualElement parent);
        void SetAtPath<TValue>(TValue value, PropertyPath path);
        bool TrySetAtPath<TValue>(TValue value, PropertyPath path);
        TValue GetAtPath<TValue>(PropertyPath path);
        bool TryGetAtPath<TValueType>(PropertyPath path, out TValueType value);
        bool TryGetProperty(PropertyPath path, out IProperty property);
        void GenerateHierarchy();
        void Release();
    }

    interface IBindingTarget<out TTarget> : IBindingTarget
    {
        TTarget Target { get; }
    }
}
