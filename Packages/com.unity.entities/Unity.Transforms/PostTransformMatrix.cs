using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Transforms
{
    /// <summary>
    /// An optional transformation matrix used to implement non-affine
    /// transformation effects such as non-uniform scale.
    /// </summary>
    /// <remarks>
    /// If this component is present, it is applied to the entity's <see cref="LocalToWorld"/> matrix
    /// by the <see cref="LocalToWorldSystem"/>.
    ///
    /// If a system writes to an entity's <see cref="LocalToWorld"/> using a <see cref="WriteGroupAttribute"/>,
    /// it is also responsible for applying this matrix if it is present.
    /// </remarks>
    public struct PostTransformMatrix : IComponentData
    {
        /// <summary>
        /// The post-transform scale matrix
        /// </summary>
        public float4x4 Value;
    }
}
