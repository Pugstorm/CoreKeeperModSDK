using Unity.Mathematics;
using UnityEngine;

namespace Unity.Entities.Tests
{
    public struct ManualConversionComponentTest : IComponentData
    {
        public float BindFloat;
        public int BindInt;
        public bool BindBool;
        public quaternion BindQuaternion;
        public float3 BindVector3;
    }

    public class ManualConversionTestAuthoring : MonoBehaviour
    {
        [RegisterBinding(typeof(ManualConversionComponentTest),
            nameof(ManualConversionComponentTest.BindFloat))]
        public float FloatField = 10.0f;

        [RegisterBinding(typeof(ManualConversionComponentTest),
            nameof(ManualConversionComponentTest.BindInt))]
        public int IntField = 5;

        [RegisterBinding(typeof(ManualConversionComponentTest),
            nameof(ManualConversionComponentTest.BindBool))]
        public bool BoolField = true;

        [RegisterBinding(typeof(ManualConversionComponentTest),
            nameof(ManualConversionComponentTest.BindQuaternion))]
        public Quaternion QuaternionField = new Quaternion(1.0f,1.0f,1.0f,1.0f);

        [RegisterBinding(typeof(ManualConversionComponentTest),
            nameof(ManualConversionComponentTest.BindVector3))]
        public Vector3 Vector3Field = new Vector3(1.0f, 1.0f,1.0f);

        class Baker : Baker<ManualConversionTestAuthoring>
        {
            public override void Bake(ManualConversionTestAuthoring authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ManualConversionComponentTest
                    {BindFloat = authoring.FloatField, BindInt = authoring.IntField, BindBool = authoring.BoolField, BindQuaternion = authoring.QuaternionField, BindVector3 = authoring.Vector3Field});
            }
        }
    }
}
