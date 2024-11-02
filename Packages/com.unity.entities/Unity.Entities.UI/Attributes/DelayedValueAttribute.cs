using System;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Attribute used to make a numeric or string value be delayed.
    ///
    /// When this attribute is used, the numeric or text field will not return a new value until the user has pressed enter or focus is moved away from the field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class DelayedValueAttribute : InspectorAttribute
    {
    }
}
