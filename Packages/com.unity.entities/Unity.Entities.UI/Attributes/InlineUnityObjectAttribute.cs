using System;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Attribute used to inform visitation that the <see cref="UnityEngine.Object"/> should be considered as a regular
    /// type rather than a <see cref="UnityEngine.Object"/>.
    /// </summary>
    /// <remarks>Has no effect when tagged on a field or property whose type is not assignable to <see cref="UnityEngine.Object"/></remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class InlineUnityObjectAttribute : Attribute
    {
    }
}
