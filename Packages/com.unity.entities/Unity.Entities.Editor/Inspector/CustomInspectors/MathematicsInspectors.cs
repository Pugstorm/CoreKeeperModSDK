using JetBrains.Annotations;
using Unity.Mathematics;
using Unity.Properties;
using UnityEngine;

using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Inspectors
{
    [UsedImplicitly]
    class Float2Inspector : BaseFieldInspector<Vector2Field, Vector2, float2>
    {
        static Float2Inspector()
        {
            TypeConversion.Register((ref float2 v) => (Vector2) v);
            TypeConversion.Register((ref Vector2 v) => (float2) v);
        }
    }

    [UsedImplicitly]
    class Float3Inspector : BaseFieldInspector<Vector3Field, Vector3, float3>
    {
        static Float3Inspector()
        {
            TypeConversion.Register((ref float3 v) => (Vector3) v);
            TypeConversion.Register((ref Vector3 v) => (float3) v);
        }
    }

    [UsedImplicitly]
    class Float4Inspector : BaseFieldInspector<Vector4Field, Vector4, float4>
    {
        static Float4Inspector()
        {
            TypeConversion.Register((ref float4 v) => (Vector4) v);
            TypeConversion.Register((ref Vector4 v) => (float4) v);
        }
    }

    [UsedImplicitly]
    class QuaternionInspector : BaseFieldInspector<Vector4Field, Vector4, quaternion>
    {
        static QuaternionInspector()
        {
            TypeConversion.Register((ref quaternion v) => (Vector4) v.value);
            TypeConversion.Register((ref Vector4 v) => new quaternion { value = v });
        }
    }
}
