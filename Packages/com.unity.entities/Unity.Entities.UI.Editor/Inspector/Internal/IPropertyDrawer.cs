using JetBrains.Annotations;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Base interface to declare a property drawer. This is an internal interface.
    /// </summary>
    interface IPropertyDrawer
    {
    }

    /// <summary>
    /// Allows to tag a <see cref="IInspector"/> to override the drawing of that field. This is an internal interface.
    /// </summary>
    /// <typeparam name="TDrawerAttribute">The <see cref="UnityEngine.PropertyAttribute"/> attribute for which this drawer is for.</typeparam>
    interface IPropertyDrawer<[UsedImplicitly] TDrawerAttribute>
        where TDrawerAttribute : UnityEngine.PropertyAttribute
    {
    }
}
