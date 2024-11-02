namespace Unity.Entities.Editor.Tests
{
    struct SystemScheduleTestData1 : IComponentData
    {
#pragma warning disable 649
        public int value;
#pragma warning restore 649
    }

    struct SystemScheduleTestData2 : IComponentData
    {
#pragma warning disable 649
        public bool value;
#pragma warning restore 649
    }
}
