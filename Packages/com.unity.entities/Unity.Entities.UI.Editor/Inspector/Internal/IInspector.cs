using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    interface IInspector
    {
        /// <summary>
        /// Called whenever the UI needs to be rebuilt.
        /// </summary>
        /// <returns>The root visual element to use for the inspection.</returns>
        VisualElement Build();

        /// <summary>
        /// Called whenever the underlying data changed, so the custom inspector can update it's data.
        /// </summary>
        void Update();

        /// <summary>
        /// Allows to know if a property exists at the given path.
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <returns><see langword="true"/> if a property exists at the given path.</returns>
        bool IsPathValid(PropertyPath path);

        /// <summary>
        /// The property path from the root to this value.
        /// </summary>
        PropertyPath PropertyPath { get; }

        PropertyPathPart Part { get; }

        /// <summary>
        /// The type of the declared value type.
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Returns true if the field has any attributes of the given type.
        /// </summary>
        /// <typeparam name="TAttribute">The attribute type to check for.</typeparam>
        /// <returns><see langword="true"/> if the field has the given attribute type; otherwise, <see langword="false"/>.</returns>
        bool HasAttribute<TAttribute>()
            where TAttribute : Attribute;

        /// <summary>
        /// Returns the first attribute of the given type.
        /// </summary>
        /// <typeparam name="TAttribute">The attribute type to get.</typeparam>
        /// <returns>The attribute of the given type for this field.</returns>
        TAttribute GetAttribute<TAttribute>()
            where TAttribute : Attribute;

        /// <summary>
        /// Returns all attribute of the given type.
        /// </summary>
        /// <typeparam name="TAttribute">The attribute type to get.</typeparam>
        /// <returns>An <see cref="IEnumerable{TAttribute}"/> for all attributes of the given type.</returns>
        IEnumerable<TAttribute> GetAttributes<TAttribute>()
            where TAttribute : Attribute;

        /// <summary>
        /// Returns the first inspection context of the given type.
        /// </summary>
        /// <param name="contextName">The name of the inspection context.</param>
        /// <typeparam name="TInspectionContext">The inspection context type to get.</typeparam>
        /// <returns>The inspection content of the given type, if it exists.</returns>
        TInspectionContext GetContext<TInspectionContext>(string contextName = null)
            where TInspectionContext : InspectionContext;

        /// <summary>
        /// Returns <see langword="true"/> if an inspection context of the given type exists.
        /// </summary>
        /// <param name="contextName">The name of the inspection context.</param>
        /// <typeparam name="TInspectionContext">The inspection context type to get.</typeparam>
        /// <returns><see langword="true"/>, if it exists.</returns>
        bool HasContext<TInspectionContext>(string contextName = null)
            where TInspectionContext : InspectionContext;
    }

    /// <summary>
    /// Allows to declare a type as custom inspector for the specified type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    interface IInspector<T> : IInspector
    {
        InspectorContext<T> Context { get; set; }
    }

    interface IExperimentalInspector{}
}
