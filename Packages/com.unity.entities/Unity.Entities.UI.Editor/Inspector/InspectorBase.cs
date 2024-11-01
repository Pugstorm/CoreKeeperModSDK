using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Base class for defining a custom inspector for values of type <see cref="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value to inspect.</typeparam>
    internal abstract class InspectorBase<T> : IInspector<T>
    {
        internal InspectorBase()
        {
        }

        InspectorContext<T> IInspector<T>.Context { get; set; }

        IInspector<T> Internal => this;

        /// <summary>
        /// Accessor to the value being inspected.
        /// </summary>
        protected T Target
        {
            get
            {
                EnsureValidContext();
                return Internal.Context.Data;
            }
            set
            {
                EnsureValidContext();
                var context = Internal.Context;
                context.Data = value;
            }
        }

        /// <summary>
        /// Returns the property name of the current value.
        /// </summary>
        protected string Name
        {
            get
            {
                EnsureValidContext();
                return Internal.Context.Name;
            }
        }

        /// <summary>
        /// Returns the property path of the current value.
        /// </summary>
        public PropertyPathPart Part
        {
            get
            {
                EnsureValidContext();
                return Internal.Context.Part;
            }
        }

        /// <summary>
        /// Returns the display name of the current value.
        /// </summary>
        protected string DisplayName
        {
            get
            {
                EnsureValidContext();
                return Internal.Context.DisplayName;
            }
        }

        /// <summary>
        /// Returns the tooltip of the current value.
        /// </summary>
        protected string Tooltip
        {
            get
            {
                EnsureValidContext();
                return Internal.Context.Tooltip;
            }
        }

        /// <summary>
        /// Returns <see langword="true"/> if the value field was tagged with the <see cref="UnityEngine.DelayedAttribute"/>.
        /// </summary>
        protected bool IsDelayed
        {
            get
            {
                EnsureValidContext();
                return Internal.Context.IsDelayed;
            }
        }

        /// <summary>
        /// Returns <see langword="true"/> if the value field is read-only.
        /// </summary>
        protected bool IsReadOnly
        {
            get
            {
                EnsureValidContext();
                return Internal.Context.IsReadOnly;
            }
        }

        /// <summary>
        /// Returns the full property path of the current target.
        /// </summary>
        public PropertyPath PropertyPath
        {
            get
            {
                EnsureValidContext();
                return Internal.Context.PropertyPath;
            }
        }

        PropertyPath BasePath
        {
            get
            {
                EnsureValidContext();
                return Internal.Context.BasePath;
            }
        }

        List<Attribute> Attributes
        {
            get
            {
                EnsureValidContext();
                return Internal.Context.Attributes;
            }
        }

        BindingContextElement Root
        {
            get
            {
                EnsureValidContext();
                return Internal.Context.Root;
            }
        }

        /// <inheritdoc/>
        public bool HasAttribute<TAttribute>()
            where TAttribute : Attribute
        {
            EnsureValidContext();
            for (var i = 0; i < Attributes?.Count; i++)
            {
                if (Attributes[i] is TAttribute)
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public TAttribute GetAttribute<TAttribute>()
            where TAttribute : Attribute
        {
            EnsureValidContext();
            for (var i = 0; i < Attributes?.Count; i++)
            {
                if (Attributes[i] is TAttribute typed)
                {
                    return typed;
                }
            }

            return default;
        }

        /// <inheritdoc/>
        public IEnumerable<TAttribute> GetAttributes<TAttribute>()
            where TAttribute : Attribute
        {
            EnsureValidContext();
            for (var i = 0; i < Attributes?.Count; i++)
            {
                if (Attributes[i] is TAttribute typed)
                {
                    yield return typed;
                }
            }
        }

        /// <inheritdoc/>
        public TInspectionContext GetContext<TInspectionContext>(string contextName = null)
            where TInspectionContext : InspectionContext
        {
            EnsureValidContext();
            return Root.GetContext<TInspectionContext>(contextName);
        }

        /// <inheritdoc/>
        public bool HasContext<TInspectionContext>(string contextName = null)
            where TInspectionContext : InspectionContext
        {
            EnsureValidContext();
            return Root.HasContext<TInspectionContext>(contextName);
        }

        /// <inheritdoc/>
        public virtual VisualElement Build()
        {
            EnsureValidContext();
            return DoDefaultGui();
        }

        /// <inheritdoc/>
        public virtual void Update()
        {
        }

        /// <inheritdoc/>
        public bool IsPathValid(PropertyPath path)
        {
            EnsureValidContext();
            if (path.IsEmpty && PropertyPath.IsEmpty)
                return true;

            var p = PropertyPath.Combine(PropertyPath, path);
            return Root.IsPathValid(p);
        }

        /// <inheritdoc/>
        public Type Type
        {
            get
            {
                EnsureValidContext();
                return typeof(T);
            }
        }

        /// <summary>
        /// Allows to revert to the default drawing handler for a specific field.
        /// </summary>
        /// <param name="parent">The parent element.</param>
        /// <param name="propertyPath">The property path to the field that needs to be drawn.</param>
        public void DoDefaultGui(VisualElement parent, string propertyPath)
            => DoDefaultGui(parent, new PropertyPath(propertyPath));

        /// <summary>
        /// Allows to revert to the default drawing handler for a specific property path.
        /// </summary>
        /// <param name="parent">The parent element.</param>
        /// <param name="propertyPath">The property path to the field that needs to be drawn.</param>
        public void DoDefaultGui(VisualElement parent, PropertyPath propertyPath)
        {
            EnsureValidContext();
            var path = PropertyPath.Combine(PropertyPath, propertyPath);
            Root.VisitAtPath(path, parent);
        }

        /// <summary>
        /// Allows to revert to the default drawing handler for a specific field.
        /// </summary>
        /// <param name="parent">The parent element.</param>
        /// <param name="index">The index of the field that needs to be drawn.</param>
        public void DoDefaultGuiAtIndex(VisualElement parent, int index)
        {
            EnsureValidContext();
            var path = PropertyPath.AppendIndex(PropertyPath, index);
            Root.VisitAtPath(path, parent);
        }

        /// <summary>
        /// Allows to revert to the default drawing handler for a specific field.
        /// </summary>
        /// <param name="parent">The parent element.</param>
        /// <param name="key">The key of the field that needs to be drawn.</param>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        public void DoDefaultGuiAtKey<TKey>(VisualElement parent, TKey key)
        {
            EnsureValidContext();
            var path = PropertyPath.AppendKey(PropertyPath, key);
            Root.VisitAtPath(path, parent);
        }

        /// <summary>
        /// Generates the default inspector.
        /// </summary>
        /// <returns>The parent <see cref="VisualElement"/> containing the generated inspector.</returns>
        protected VisualElement DoDefaultGui()
        {
            var visitor = new InspectorVisitor(Root);
            var root = new CustomInspectorElement.DefaultInspectorElement();
            using (visitor.Context.MakeIgnoreInspectorScope())
            {
                using (visitor.Context.MakeParentScope(root))
                {
                    if (PropertyPath.IsEmpty)
                    {
                        var wrapper = new PropertyWrapper<T>(Target);
                        PropertyContainer.Accept(visitor, ref wrapper);
                    }
                    else
                    {
                        Root.VisitAtPath(PropertyPath, root, visitor);
                    }
                }
            }

            return root;
        }

        /// <summary>
        /// Notifies the root element that a change occured on this value. This must be called when doing manual
        /// data binding.
        /// </summary>
        /// <remarks>
        /// This is called automatically when the "binding=path" is set to a valid value/field combination.
        /// </remarks>
        protected void NotifyChanged()
        {
            EnsureValidContext();
            Root.NotifyChanged(PropertyPath);
        }

        void EnsureValidContext([CallerMemberName] string caller = "")
        {
            if (EqualityComparer<InspectorContext<T>>.Default.Equals(Internal.Context, default))
                throw new InvalidOperationException($"{TypeUtility.GetTypeDisplayName(typeof(InspectorBase<T>))}: Cannot call `{caller}` before the `{nameof(Build)}` method has been called.");
        }
    }
}
