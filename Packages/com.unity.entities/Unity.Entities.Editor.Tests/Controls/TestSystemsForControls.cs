namespace Unity.Entities.Editor.Tests
{
    partial class TestSystemsForControls
    {
        public partial class SystemA : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.WithoutBurst().WithAll<EntityGuid>().ForEach((in EntityGuid g) => { }).Run();
            }
        }

        [UpdateBefore(typeof(SystemA))]
        public partial class SystemB : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.WithoutBurst().WithAll<EntityGuid>().ForEach((in EntityGuid g) => { }).Run();
            }
        }

        public partial class SystemC : SystemBase
        {
            protected override void OnUpdate()
            {
            }
        }
    }
}
