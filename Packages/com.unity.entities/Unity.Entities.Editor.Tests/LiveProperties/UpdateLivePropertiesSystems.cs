using Unity.Entities.Tests;
using Unity.Mathematics;

namespace Unity.Entities.Editor.Tests
{
    partial class UpdateSingleLiveProperties : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref ManualConversionComponentTest comp) =>
            {
                comp.BindInt = 1;
                comp.BindFloat = 1.5f;
                comp.BindBool = false;
                comp.BindQuaternion.value = new float4(3.0f, 4.0f, 5.0f, 6.0f);
                comp.BindVector3 = new float3(3.0f, 4.0f, 5.0f);
            }).Schedule();
        }
    }

    partial class UpdateMultipleLiveProperties : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref BindingRegistryIntComponent comp) =>
            {
                comp.Int1  = 1;
                comp.Int2  = new int2(1, 2);
                comp.Int3 = new int3(1, 2, 3);
                comp.Int4 = new int4(1, 2, 3, 4);
            }).Schedule();
        }
    }
}
