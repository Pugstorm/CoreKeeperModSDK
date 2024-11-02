using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Base class for elements that can generate a UI hierarchy for a given object.
    /// </summary>
    internal abstract class BindingContextElement : BindableElement
    {
        /// <summary>
        /// Handler to react to changes.
        /// </summary>
        /// <param name="element">The element under which the change is detected.</param>
        /// <param name="path">The property path of the change</param>
        /// <remarks>
        /// The property path can be empty for the root object or when the path to the change is not provided.
        /// </remarks>
        public delegate void ChangeHandler(BindingContextElement element, PropertyPath path);

        /// <summary>
        /// Handler to filter visitation based on a field's attribute.
        /// </summary>
        /// <param name="attributes">The attributes of the field.</param>
        /// <returns><see langword="true"/>if the field should be shown; <see langword="false"/> otherwise.</returns>
        public delegate bool AttributeFilterHandler(IEnumerable<Attribute> attributes);

        /// <summary>
        /// Register to this event to be notified when a change is detected.
        /// </summary>
        public event ChangeHandler OnChanged = delegate { };

        internal AttributeFilterHandler m_AttributeFilter;

        readonly List<InspectionContext> m_InspectionContexts = new List<InspectionContext>();

        IBindingTarget m_BindingTarget;
        BindingContextElement m_Root;

        /// <summary>
        /// Constructs an instance of <see cref="BindingContextElement"/>.
        /// </summary>
        internal BindingContextElement()
        {
            Resources.Templates.Common.AddStyles(this);
            AddToClassList(UssClasses.Variables);
        }

        /// <summary>
        /// Tries to get the target of the <see cref="BindingContextElement"/> as an instance of type <see cref="T"/>.
        /// </summary>
        /// <param name="target">The target instance.</param>
        /// <typeparam name="T">The type of the target.</typeparam>
        /// <returns><see langword="true"/> if target was of the correct type; <see langword="false"/> otherwise.</returns>
        public bool TryGetTarget<T>(out T target)
        {
            switch (m_BindingTarget)
            {
                case null:
                    target = default;
                    return false;
                case IBindingTarget<T> typedTarget:
                    target = typedTarget.Target;
                    return true;
                default:
                    return m_BindingTarget.TryGetTarget(out target);
            }
        }

        /// <summary>
        /// Gets the target of the <see cref="BindingContextElement"/> as <see cref="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the target.</typeparam>
        /// <returns>The instance of the target.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no target has been set.</exception>
        /// <exception cref="InvalidCastException">Thrown when the target is not of type <see cref="T"/>.</exception>
        public T GetTarget<T>()
        {
            if (TryGetTarget(out T target))
            {
                return target;
            }

            if (null == m_BindingTarget)
            {
                throw new InvalidOperationException(
                    $"{nameof(BindingContextElement)}: Could not get value of type `{typeof(T).Name}`, as no target has been set.");
            }

            throw new InvalidCastException($"{nameof(BindingContextElement)}: Could not get value of type `{typeof(T).Name}`. Actual value is of type`{m_BindingTarget.TargetType.Name}`");
        }

        /// <summary>
        /// Sets the current target.
        /// </summary>
        /// <remarks>
        /// This will clear current hierarchy and regenerate a new one.
        /// </remarks>
        /// <param name="target">The target to set.</param>
        /// <typeparam name="T">The type of the target.</typeparam>
        public void SetTarget<T>(T target)
        {
            if (TryGetTarget(out T current) && EqualityComparer<T>.Default.Equals(target, current))
            {
                // Nothing to do..
                return;
            }

            if (null != m_BindingTarget)
            {
                var declaredType = typeof(T);

                var targetType = declaredType;
                if (typeof(T).IsClass && EqualityComparer<T>.Default.Equals(target, default))
                {
                    targetType = target.GetType();
                }

                if (declaredType == m_BindingTarget.DeclaredType && targetType == m_BindingTarget.TargetType)
                {
                    ((BindingTarget<T>) m_BindingTarget).Target = target;
                    return;
                }
            }

            m_BindingTarget?.Release();

            if (TypeTraits<T>.CanBeNull && EqualityComparer<T>.Default.Equals(target, default))
            {
                m_BindingTarget = null;
                return;
            }

            m_BindingTarget = new BindingTarget<T>(this, target);
            name = TypeUtility.GetTypeDisplayName(target.GetType());
            m_BindingTarget.GenerateHierarchy();
        }

        /// <summary>
        /// Clears the current target and removes all child elements from this element's contentContainer.
        /// </summary>
        public void ClearTarget()
        {
            m_BindingTarget?.Release();
            m_BindingTarget = null;
        }

        /// <summary>
        /// Clears the current children and re-generates them.
        /// </summary>
        public void ForceReload()
        {
            m_BindingTarget?.Release();
            m_BindingTarget?.GenerateHierarchy();
        }

        /// <summary>
        /// Allows to filter the hierarchy generation based on the field's attributes.
        /// </summary>
        /// <param name="filter">The filter method to apply.</param>
        public void SetAttributeFilter(AttributeFilterHandler filter)
        {
            if (m_AttributeFilter == filter)
                return;
            m_AttributeFilter = filter;
            ForceReload();
        }

        /// <summary>
        /// Adds an inspection context to this element.
        /// </summary>
        /// <param name="inspectionContext">The inspection context to add.</param>
        /// <exception cref="NullReferenceException">The inspection context is <see langword="null"/>.</exception>
        public void AddContext(InspectionContext inspectionContext)
        {
            if (null == inspectionContext)
                throw new NullReferenceException(nameof(inspectionContext));

            m_InspectionContexts.Add(inspectionContext);
            ForceReload();
        }

        /// <summary>
        /// Removes an inspection context from this element.
        /// </summary>
        /// <param name="inspectionContext">The inspection context to add.</param>
        /// <exception cref="NullReferenceException">The inspection context is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The inspection context was not previously added.</exception>
        public void RemoveContext(InspectionContext inspectionContext)
        {
            if (null == inspectionContext)
                throw new NullReferenceException(nameof(inspectionContext));

            if (!m_InspectionContexts.Remove(inspectionContext))
                throw new ArgumentException(nameof(inspectionContext));

            ForceReload();
        }

        /// <summary>
        /// Returns an inspection context of the given type.
        /// </summary>
        /// <param name="contextName">The name of the inspection context.</param>
        /// <typeparam name="T">The inspection context type.</typeparam>
        /// <returns>The inspection context, if it exists.</returns>
        public T GetContext<T>(string contextName = null)
            where T : InspectionContext
        {
            return m_InspectionContexts.OfType<T>().FirstOrDefault(c => string.IsNullOrEmpty(contextName) || c.Name == contextName)
                   ?? m_Root?.GetContext<T>()
                   ?? GetFirstAncestorOfType<BindingContextElement>()?.GetContext<T>(contextName);
        }

        /// <summary>
        /// Returns <see langword="true"/> if an inspection context of the given type exists.
        /// </summary>
        /// <param name="contextName">The name of the inspection context.</param>
        /// <typeparam name="T">The inspection context type.</typeparam>
        /// <returns><see langword="true"/>, if it exists.</returns>
        public bool HasContext<T>(string contextName = null)
            where T : InspectionContext
        {
            return m_InspectionContexts.OfType<T>().Any(c => string.IsNullOrEmpty(contextName) || c.Name == contextName)
                   || (GetFirstAncestorOfType<BindingContextElement>()?.HasContext<T>(contextName) ?? false);
        }

        /// <summary>
        /// Sets the value of type <see cref="TValue"/> at the given path.
        /// </summary>
        /// <param name="path">The property path to the value.</param>
        /// <param name="value">the value we want to set.</param>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <exception cref="InvalidOperationException">Thrown when no target has been set.</exception>
        public void SetValue<TValue>(PropertyPath path, TValue value)
        {
            if (null == m_BindingTarget)
            {
                throw new InvalidOperationException($"{nameof(BindingContextElement)}: Could not set value of type `{typeof(TValue).Name}`, as no target was set.");
            }
            m_BindingTarget.SetAtPath(value, path);
        }

        /// <summary>
        /// Tries to set the value of type <see cref="TValue"/> at the given path.
        /// </summary>
        /// <param name="path">The property path to the value.</param>
        /// <param name="value">the value we want to set.</param>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <returns>The value that was effectively set.</returns>
        public bool TrySetValue<TValue>(PropertyPath path, TValue value)
        {
            return m_BindingTarget?.TrySetAtPath(value, path) ?? false;
        }

        /// <summary>
        /// Gets the value of type <see cref="TValue"/> at the given path.
        /// </summary>
        /// <param name="path">The property path to the value.</param>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <returns>The value at path.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no target has been set.</exception>
        public TValue GetValue<TValue>(PropertyPath path)
        {
            if (null == m_BindingTarget)
            {
                throw new InvalidOperationException($"{nameof(BindingContextElement)}: Could not set value of type `{typeof(TValue).Name}`, as no target was set.");
            }
            return m_BindingTarget.GetAtPath<TValue>(path);
        }

        /// <summary>
        /// Tries to get the value of type <see cref="TValue"/> at the given path.
        /// </summary>
        /// <param name="path">The property path to the value.</param>
        /// <param name="value">The value at path.</param>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <returns><see langword="true"/> if the getting the value was successful.</returns>
        public bool TryGetValue<TValue>(PropertyPath path, out TValue value)
        {
            if (null == m_BindingTarget)
            {
                value = default;
                return false;
            }

            if (path.IsEmpty && m_BindingTarget.TryGetTarget(out value))
                return true;

            if (m_BindingTarget.TryGetAtPath(path, out value))
                return true;

            value = default;
            return false;
        }

        /// <summary>
        /// Returns <see langword="true"/> if the given path is valid from the target.
        /// </summary>
        /// <param name="path">the property path.</param>
        /// <returns><see langword="true"/> if the path is valid.</returns>
        public bool IsPathValid(PropertyPath path)
        {
            return m_BindingTarget?.IsPathValid(path) ?? false;
        }

        internal void ReloadAtPath(PropertyPath path, VisualElement element)
        {
            m_BindingTarget?.ReloadAtPath(path, element);
        }

        internal void NotifyChanged(PropertyPath path)
        {
            OnChanged(this, path);
        }

        internal void VisitAtPath(PropertyPath path, IPropertyVisitor visitor)
        {
            m_BindingTarget?.VisitAtPath(visitor, path);
        }

        internal void VisitAtPath(PropertyPath path, VisualElement localRoot)
        {
            m_BindingTarget?.VisitAtPath(path, localRoot);
        }

        internal void VisitAtPath(PropertyPath path, VisualElement localRoot, InspectorVisitor visitor)
        {
            m_BindingTarget?.VisitAtPath(visitor, path, localRoot);
        }

        internal void RegisterBindings(PropertyPath path, VisualElement element)
        {
            m_BindingTarget?.RegisterBindings(path, element);
        }

        internal bool TryGetProperty(PropertyPath path, out IProperty property)
        {
            if (path.IsEmpty)
            {
                property = null;
                return false;
            }

            return m_BindingTarget.TryGetProperty(path, out property);
        }

        /// <summary>
        /// Returns the type of the current target.
        /// </summary>
        /// <returns>The type of the target.</returns>
        public Type GetTargetType()
        {
            return m_BindingTarget?.TargetType;
        }

        internal InspectorVisitor GetVisitor()
        {
            return m_BindingTarget.Visitor;
        }

        internal void SwapWithInstance<TValue>(PropertyPath propertyPath, VisualElement current, TValue value)
        {
            var previousParent = current.parent;
            TrySetValue(propertyPath, value);
            NotifyChanged(propertyPath);
            var element = new VisualElement();
            VisitAtPath(propertyPath, element);
            var index = previousParent.IndexOf(current);
            for (var i = 0; i < element.childCount; ++i)
            {
                previousParent.Insert(index + i, element[i]);
            }

            if (typeof(TValue) ==typeof(string) && previousParent.childCount > index)
                previousParent[index]?.Q(className: UssClasses.Unity.BaseTextFieldInput)?.Focus();

            current.RemoveFromHierarchy();
            previousParent.GetFirstAncestorOfType<ICustomStyleApplier>()?.ApplyStyleAtPath(propertyPath);
        }

        internal void StartHighlightAtPath(PropertyPath path)
        {
            foreach (var element in this.ChildrenOfType<IContextElement>().Where(c => c.Path.Equals(path)).OfType<Foldout>())
            {
                element.Q<Toggle>().AddToClassList(UssClasses.Highlight);
            }
        }

        internal void StopHighlightAtPath(PropertyPath path)
        {
            foreach (var element in this.ChildrenOfType<IContextElement>().Where(c => c.Path.Equals(path)).OfType<Foldout>())
            {
                element.Q<Toggle>().RemoveFromClassList(UssClasses.Highlight);
            }
        }

        internal void SetRoot(BindingContextElement root)
        {
            m_Root = root;
        }
    }
}
