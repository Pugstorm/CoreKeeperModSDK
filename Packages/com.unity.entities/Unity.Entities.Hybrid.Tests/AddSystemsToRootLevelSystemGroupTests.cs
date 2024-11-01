using System.Linq;
using NUnit.Framework;

namespace Unity.Entities.Tests
{
    public class AddSystemsToRootLevelSystemGroupTests
    {
        partial class GetOtherSystemA : SystemBase
        {
            public GetOtherSystemB Other;

            protected override void OnCreate()
            {
                Other = World.GetExistingSystemManaged<GetOtherSystemB>();
            }

            protected override void OnUpdate() {}
        }

        partial class GetOtherSystemB : SystemBase
        {
            public GetOtherSystemA Other;

            protected override void OnCreate()
            {
                Other = World.GetExistingSystemManaged<GetOtherSystemA>();
            }

            protected override void OnUpdate() {}
        }

        [Test]
        public void CrossReferenceSystem()
        {
            using var world = new World("TestWorld");
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, typeof(GetOtherSystemA), typeof(GetOtherSystemB));

            var systemA = world.GetExistingSystemManaged<GetOtherSystemA>();
            var systemB = world.GetExistingSystemManaged<GetOtherSystemB>();

            Assert.AreEqual(systemB, systemA.Other);
            Assert.AreEqual(systemA, systemB.Other);
        }
    }
}
