using System;

namespace Unity.Entities
{
    /// <summary>
    /// Attribute that informs the baking system that bakers of types derived from the current authoring component must be executed.
    /// </summary>
    /// <remarks>
    /// Use this attribute on bakers which handle authoring components that can be specialized.
    /// In the example below, both the BaseBaker and the DerivedBaker are invoked when baking the DerivedAuthoring component.
    /// </remarks>
    /// <example><code>
    /// class BaseAuthoring : MonoBehaviour { public int BaseValue; }
    /// class DerivedAuthoring : BaseAuthoring { public float DerivedValue; }
    ///
    /// [BakeDerivedTypes]
    /// class BaseBaker : Baker&lt;BaseAuthoring> { public override void Bake(BaseAuthoring authoring) { }
    ///
    /// class DerivedBaker : Baker&lt;DerivedAuthoring> { public override void Bake(DerivedAuthoring authoring) { }
    /// </code></example>
    /// <seealso cref="Baker{TAuthoringType}"/>
    public class BakeDerivedTypesAttribute : Attribute { }
}
