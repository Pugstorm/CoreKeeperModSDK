using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;
using UnityEngine;

namespace Unity.Entities.UI
{
    partial class InspectorVisitor
        : IPropertyVisitor
            , ICollectionPropertyVisitor
            , IListPropertyVisitor
            , ISetPropertyVisitor
            , IDictionaryPropertyVisitor
    {
        readonly List<IInspectorVisit> m_AdapterOverrides = new List<IInspectorVisit>();

        internal void AddExplicitAdapter(IInspectorVisit adapter)
        {
            m_AdapterOverrides.Add(adapter);
        }

        internal void RemoveExplicitAdapter(IInspectorVisit adapter)
        {
            m_AdapterOverrides.Remove(adapter);
        }

        void IPropertyVisitor.Visit<TContainer, TValue>(
            Property<TContainer, TValue> property,
            ref TContainer container)
        {
            if (IsExcluded(property))
                return;

            var value = property.GetValue(ref container);
            var isWrapper = container is PropertyWrapper<TValue>;
            Context.IsRootObject = isWrapper;

            // Null values
            if (TypeTraits<TValue>.CanBeNull
                && ShouldBeTreatedAsNull(property, ref container, ref value)
                && ShouldStayNull(property, ref container, ref value))
            {
                using (Context.MakePathScope(property))
                {
                    if (VisitNull(Context, property, ref value, Context.CopyCurrentPath()))
                        return;
                }
            }

            foreach (var adapter in m_AdapterOverrides)
            {
                if (!(adapter is IInspectorVisit<TValue> partiallyTyped))
                    continue;

                using (Context.MakePathScope(property))
                {
                    if (partiallyTyped.Visit(Context, property, ref value, Context.CopyCurrentPath()))
                        return;
                }
            }

            // Primitives
            if (this is IInspectorVisit<TValue> typed)
            {
                using (Context.MakePathScope(property))
                {
                    if (typed.Visit(Context, property, ref value, Context.CopyCurrentPath()))
                        return;
                }
            }

            // UnityEngine.Object
            if (this is IInspectorContravariantVisit<TValue> contravariant)
            {
                using (Context.MakePathScope(property))
                {
                    if (contravariant.Visit(Context, property, value, Context.CopyCurrentPath()))
                        return;
                }
            }

            // Enums
            if (TypeTraits<TValue>.IsEnum)
            {
                using (Context.MakePathScope(property))
                {
                    if (VisitEnum(Context, property, ref value, Context.CopyCurrentPath(), isWrapper))
                        return;
                }
            }

            try
            {
                // Regular visit
                if (!isWrapper)
                    Context.AddToPath(property);

                var path = Context.CopyCurrentPath();
                using (var references = Context.MakeVisitedReferencesScope(ref value, path))
                {
                    if (references.VisitedOnCurrentBranch)
                    {
                        Context.Parent.Add(new CircularReferenceElement<TValue>(Context.Root, property, value, path,
                            references.GetReferencePath()));
                        return;
                    }

                    var inspector = GetCustomInspector(property, ref value, path, isWrapper);
                    if (null != inspector)
                    {
                        var customInspector = new CustomInspectorElement(path, inspector, Context.Root);
                        Context.Parent.contentContainer.Add(customInspector);
                        return;
                    }

                    if (path.Length > 0)
                    {
                        if (string.IsNullOrEmpty(GuiFactory.GetDisplayName(property)))
                            PropertyContainer.TryAccept(this, ref value);
                        else
                        {
                            // Give a chance for different property bags to handle themselves
                            if (PropertyBag.TryGetPropertyBagForValue(ref value, out var valuePropertyBag))
                            {
                                switch (valuePropertyBag)
                                {
                                    case IDictionaryPropertyAccept<TValue> accept:
                                        accept.Accept(this, property, ref container, ref value);
                                        break;
                                    case IListPropertyAccept<TValue> accept:
                                        accept.Accept(this, property, ref container, ref value);
                                        break;
                                    case ISetPropertyAccept<TValue> accept:
                                        accept.Accept(this, property, ref container, ref value);
                                        break;
                                    case ICollectionPropertyAccept<TValue> accept:
                                        accept.Accept(this, property, ref container, ref value);
                                        break;
                                    default:
                                        var foldout = GuiFactory.Foldout<TValue>(property, path, Context);
                                        using (Context.MakeParentScope(foldout))
                                            PropertyContainer.TryAccept(this, ref value);
                                        break;
                                }
                            }
                        }
                    }
                    else
                    {
                        PropertyContainer.TryAccept(this, ref value);
                    }
                }
            }
            finally
            {
                if (!isWrapper)
                    Context.RemoveFromPath(property);
            }
        }

        void ICollectionPropertyVisitor.Visit<TContainer, TCollection, TElement>(
            Property<TContainer, TCollection> property,
            ref TContainer container,
            ref TCollection collection)
        {
            var path = Context.CopyCurrentPath();
            var foldout = GuiFactory.Foldout<TCollection>(property, path, Context);
            using (Context.MakeParentScope(foldout))
                PropertyContainer.TryAccept(this, ref collection);
        }

        void IListPropertyVisitor.Visit<TContainer, TList, TElement>(
            Property<TContainer, TList> property,
            ref TContainer container,
            ref TList list)
        {
            var path = Context.CopyCurrentPath();
            var foldout = GuiFactory.Foldout<TList, TElement>(property, path, Context);
            using (Context.MakeParentScope(foldout))
                foldout.Reload(property);
        }

        void ISetPropertyVisitor.Visit<TContainer, TSet, TValue>(
            Property<TContainer, TSet> property,
            ref TContainer container,
            ref TSet set)
        {
            var path = Context.CopyCurrentPath();
            var foldout = GuiFactory.SetFoldout<TSet, TValue>(property, path, Context);
            using (Context.MakeParentScope(foldout))
                foldout.Reload(property);
        }

        public void Visit<TContainer, TDictionary, TKey, TValue>(
            Property<TContainer, TDictionary> property,
            ref TContainer container,
            ref TDictionary dictionary) where TDictionary : IDictionary<TKey, TValue>
        {
            var path = Context.CopyCurrentPath();
            var foldout = GuiFactory.Foldout<TDictionary, TKey, TValue>(property, path, Context);
            using (Context.MakeParentScope(foldout))
                foldout.Reload(property);
        }

        bool IsExcluded<TContainer, TValue>(
            Property<TContainer, TValue> property)
        {
            var shouldShow = true;
            if (null != Context.Root.m_AttributeFilter && !(property is IPropertyWrapper))
            {
                shouldShow = Context.Root.m_AttributeFilter(property.GetAttributes());
            }

            return !shouldShow || !IsFieldTypeSupported<TContainer, TValue>() || !ShouldShowField(property);
        }

        bool ShouldBeTreatedAsNull<TContainer, TValue>(
            Property<TContainer, TValue> property,
            ref TContainer container,
            ref TValue value)
        {
            var isUnityObject = typeof(UnityEngine.Object).IsAssignableFrom(typeof(TValue));

            return (!isUnityObject || property.HasAttribute<InlineUnityObjectAttribute>() )&&
                   EqualityComparer<TValue>.Default.Equals(value, default);
        }

        static bool ShouldStayNull<TContainer, TValue>(
            Property<TContainer, TValue> property,
            ref TContainer container,
            ref TValue value)
        {
            if (property.IsReadOnly || !property.HasAttribute<CreateInstanceOnInspectionAttribute>() ||
                property is ICollectionElementProperty)
                return true;

            var attribute = property.GetAttribute<CreateInstanceOnInspectionAttribute>();
            if (null == attribute.Type)
            {
                if (TypeUtility.CanBeInstantiated<TValue>())
                {
                    value = TypeUtility.Instantiate<TValue>();
                    property.SetValue(ref container, value);
                    return false;
                }

                Debug.LogWarning(PropertyChecks.GetNotConstructableWarningMessage(typeof(TValue)));
            }
            else
            {
                var isAssignable = typeof(TValue).IsAssignableFrom(attribute.Type);
                var isConstructable = TypeConstructionUtility.GetAllConstructableTypes(typeof(TValue))
                    .Contains(attribute.Type);
                if (isAssignable && isConstructable)
                {
                    value = TypeUtility.Instantiate<TValue>(attribute.Type);
                    property.SetValue(ref container, value);
                    return false;
                }

                Debug.LogWarning(isAssignable
                    ? PropertyChecks.GetNotConstructableWarningMessage(attribute.Type)
                    : PropertyChecks.GetNotAssignableWarningMessage(attribute.Type, typeof(TValue)));
            }

            return true;
        }

        IInspector GetCustomInspector<TValue>(
            IProperty property,
            ref TValue value,
            PropertyPath path,
            bool root)
        {
            if (Context.NextInspectorIsIgnored())
            {
                return null;
            }

            if (root && Context.Root is InspectorElement)
            {
                return InspectorRegistry.GetInspector(Context, property, ref value, path);
            }
            return InspectorRegistry.GetAttributeInspector(Context, property, ref value, path)
                   ?? InspectorRegistry.GetPropertyInspector(Context, property, ref value, path);
        }

        IInspector GetAttributeDrawer<TValue>(
            IProperty property,
            ref TValue value,
            PropertyPath path)
        {
            return Context.NextInspectorIsIgnored()
                ? null
                : InspectorRegistry.GetAttributeInspector(Context, property, ref value, path);
        }

        static bool IsFieldTypeSupported<TContainer, TValue>()
        {
            var valueType = typeof(TValue);
            if (valueType == typeof(object) && typeof(TContainer) != typeof(PropertyWrapper<TValue>))
                return false;

            if (valueType.IsArray && valueType.GetArrayRank() != 1)
                return false;

            if (Nullable.GetUnderlyingType(valueType) != null)
                return false;

#if !UNITY_2020_2_OR_NEWER
            // 64-bits enums are not supported in UIToolkit right now.
            if (valueType.IsEnum && Enum.GetUnderlyingType(valueType) == typeof(long))
                return false;
#endif

            return true;
        }

        static bool ShouldShowField<TContainer, TValue>(
            Property<TContainer, TValue> property)
        {
            return !property.HasAttribute<HideInInspector>();
        }
    }
}
