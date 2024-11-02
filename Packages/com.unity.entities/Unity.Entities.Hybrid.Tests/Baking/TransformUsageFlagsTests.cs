using NUnit.Framework;

namespace Unity.Entities.Hybrid.Tests.Baking
{
    public class TransformUsageFlagsTests
    {
        [Test]
        public void UsageFlagAddWorks()
        {
            var usageFlags = new TransformUsageFlagCounters();
            usageFlags.Add(TransformUsageFlags.Dynamic);
            usageFlags.Add(TransformUsageFlags.NonUniformScale);
            Assert.AreEqual(TransformUsageFlags.Dynamic | TransformUsageFlags.NonUniformScale, usageFlags.Flags);
        }

        [Test]
        public void UsageFlagRemoveWorks()
        {
            var usageFlags = new TransformUsageFlagCounters();
            usageFlags.Add(TransformUsageFlags.Dynamic | TransformUsageFlags.NonUniformScale);
            usageFlags.Add(TransformUsageFlags.Dynamic);
            usageFlags.Remove(TransformUsageFlags.Dynamic);
            Assert.AreEqual(TransformUsageFlags.Dynamic | TransformUsageFlags.NonUniformScale, usageFlags.Flags);
            usageFlags.Remove(TransformUsageFlags.Dynamic);
            Assert.AreEqual(TransformUsageFlags.NonUniformScale, usageFlags.Flags);
        }

        [Test]
        public void UsageFlagUnusedWorks()
        {
            var usageFlags = new TransformUsageFlagCounters();
            Assert.IsTrue(usageFlags.IsUnused);
            usageFlags.Add(TransformUsageFlags.None);
            Assert.IsFalse(usageFlags.IsUnused);
            usageFlags.Remove(TransformUsageFlags.None);
            Assert.IsTrue(usageFlags.IsUnused);
        }
    }
}
