namespace Unity.Entities.Tests
{
    public struct IntTestData : IComponentData
    {
        public IntTestData(int val)
        {
            Value = val;
        }

        public int Value;
    }
}
