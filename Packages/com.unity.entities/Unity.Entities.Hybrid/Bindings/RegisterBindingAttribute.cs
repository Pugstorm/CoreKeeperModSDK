using System;

namespace Unity.Entities
{

    /// <summary>
    /// Creates a table association between the Type specified and the runtime field of an IComponentData,
    /// accessible via the BindingRegistry
    /// </summary>
    /// <remarks>
    /// Only primitive types of int, bool, and float, in addition to Unity.Mathematics variants of these primitives
    /// (e.g. int2, float4) will be added to the BindingRegistry. Other non-compatible types will be silently ignored
    /// if this attribute is applied to it.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public class RegisterBindingAttribute : Attribute
    {
        //order of constructors matter. Will break ILPP if changed.

        /// <summary>
        /// Establish a binding between the tagged authoring type and a runtime component field.
        /// </summary>
        /// <param name="runtimeComponent">The target component type</param>
        /// <param name="runtimeField">The target component field</param>
        public RegisterBindingAttribute(Type runtimeComponent, string runtimeField)
        {
            ComponentType = runtimeComponent;
            ComponentField = runtimeField;
            Generated = false;
            AuthoringField = null;
        }

        /// <summary>
        /// Establish a binding between the tagged authoring type and a runtime component field.
        /// </summary>
        /// <param name="runtimeComponent">The target component type</param>
        /// <param name="runtimeField">The target component field</param>
        /// <param name="generated">If true, the type is auto-generated</param>
        [Obsolete("You can now use base RegisterBindingAttribute(Type, string) to register vector based properties. (RemovedAfter 2023-02-14)")]
        public  RegisterBindingAttribute(Type runtimeComponent, string runtimeField, bool generated)
        {
            ComponentType = runtimeComponent;
            ComponentField = runtimeField;
            Generated = generated;
            AuthoringField = null;
        }

        /// <summary>
        /// Establish a binding between the tagged authoring type and a runtime component field.
        /// </summary>
        /// <param name="authoringField">The nested authoring field. Uses tagged field if null or empty.</param>
        /// <param name="runtimeComponent">The target component type</param>
        /// <param name="runtimeField">The target component field</param>
        public RegisterBindingAttribute(string authoringField, Type runtimeComponent, string runtimeField)
        {
            ComponentType = runtimeComponent;
            ComponentField = runtimeField;
            Generated = false;
            AuthoringField = authoringField;
        }

        internal bool Generated { get;  }
        /// <summary>
        /// The target component type
        /// </summary>
        public Type ComponentType { get; }

        /// <summary>
        /// The name of the target component field
        /// </summary>
        public string ComponentField { get; }

        /// <summary>
        /// Name of the nested authoring field. The base field if null or empty.
        /// </summary>
        public string AuthoringField { get;  }
    }
}
