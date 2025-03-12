using System;
using NUnit.Framework;

namespace Unity.Physics.Tests.Collision.Filter
{
    class FilterTests
    {
        //CollisionFilter expected behavior:
        //  uint BelongsTo       A bit mask describing which layers this object belongs to.
        //  uint CollidesWith    A bit mask describing which layers this object can collide with.
        //  int  GroupIndex      An optional override for the bit mask checks.
        //                         If the value in both objects is equal and positive, the objects always collide.
        //                         If the value in both objects is equal and negative, the objects never collide.

        [Test]
        public void CollisionFilterTestLayerSelfCollision()
        {
            var filter0 = new CollisionFilter();

            var filter1 = new CollisionFilter
            {
                BelongsTo = 1,
                CollidesWith = 1
            };

            var filter2 = new CollisionFilter
            {
                BelongsTo = 0xffffffff,
                CollidesWith = 0xffffffff
            };

            Assert.IsFalse(CollisionFilter.IsCollisionEnabled(filter0, filter0));
            Assert.IsFalse(CollisionFilter.IsCollisionEnabled(CollisionFilter.Zero, CollisionFilter.Zero));
            Assert.IsTrue(filter0.Equals(CollisionFilter.Zero));

            Assert.IsTrue(CollisionFilter.IsCollisionEnabled(filter1, filter1));
            Assert.IsTrue(CollisionFilter.IsCollisionEnabled(filter2, filter2));
            Assert.IsTrue(CollisionFilter.IsCollisionEnabled(CollisionFilter.Default, CollisionFilter.Default));
            Assert.IsTrue(filter2.Equals(CollisionFilter.Default));
        }

        [Test]
        public void CollisionFilterTestBelongsToAndCollidesWithBits()
        {
            var filterA = new CollisionFilter
            {
                BelongsTo = 1,
                CollidesWith = 2
            };

            var filterB = new CollisionFilter
            {
                BelongsTo = 2,
                CollidesWith = 1
            };

            Assert.IsFalse(CollisionFilter.IsCollisionEnabled(filterA, filterA));
            Assert.IsFalse(CollisionFilter.IsCollisionEnabled(filterB, filterB));

            Assert.IsTrue(CollisionFilter.IsCollisionEnabled(filterA, filterB));
            Assert.IsTrue(CollisionFilter.IsCollisionEnabled(filterB, filterA));

            Assert.IsTrue(CollisionFilter.IsCollisionEnabled(filterA, CollisionFilter.Default));
            Assert.IsTrue(CollisionFilter.IsCollisionEnabled(filterB, CollisionFilter.Default));
        }

        [Test]
        public void CollisionFilterTestGroupIndexSimple()
        {
            var filter0 = new CollisionFilter
            {
                GroupIndex = 1
            };

            var filter1 = new CollisionFilter
            {
                BelongsTo = 1,
                CollidesWith = 1,
                GroupIndex = -1
            };

            Assert.IsFalse(CollisionFilter.IsCollisionEnabled(filter0, CollisionFilter.Zero));
            Assert.IsFalse(CollisionFilter.IsCollisionEnabled(filter0, CollisionFilter.Default));

            Assert.IsFalse(CollisionFilter.IsCollisionEnabled(filter1, CollisionFilter.Zero));
            Assert.IsTrue(CollisionFilter.IsCollisionEnabled(filter1, CollisionFilter.Default));

            Assert.IsTrue(CollisionFilter.IsCollisionEnabled(filter0, filter0));
            Assert.IsFalse(CollisionFilter.IsCollisionEnabled(filter1, filter1));
        }

        [Test]
        public void CollisionFilterTestGroupIndex()
        {
            var filter0 = new CollisionFilter
            {
                GroupIndex = 1
            };

            var filter1 = new CollisionFilter
            {
                BelongsTo = 1,
                CollidesWith = 1,
                GroupIndex = -1
            };

            var filter2 = new CollisionFilter
            {
                BelongsTo = 1,
                CollidesWith = 1,
                GroupIndex = 1
            };

            var filter3 = new CollisionFilter
            {
                BelongsTo = 0,
                CollidesWith = 0,
                GroupIndex = -1
            };

            Assert.IsTrue(CollisionFilter.IsCollisionEnabled(filter0, filter2));
            Assert.IsTrue(CollisionFilter.IsCollisionEnabled(filter2, filter0));

            Assert.IsTrue(CollisionFilter.IsCollisionEnabled(filter1, filter2));
            Assert.IsTrue(CollisionFilter.IsCollisionEnabled(filter2, filter1));

            Assert.IsFalse(CollisionFilter.IsCollisionEnabled(filter3, filter1));
            Assert.IsFalse(CollisionFilter.IsCollisionEnabled(filter1, filter3));
        }

        [Test]
        public void CollisionFilterTestCreateUnion()
        {
            //Union: GroupIndex will only be not 0 if both operands have the same GroupIndex

            var filter0 = new CollisionFilter
            {
                GroupIndex = 1
            };

            var filter1 = new CollisionFilter
            {
                BelongsTo = 1,
                CollidesWith = 1,
                GroupIndex = -1
            };

            var filter2 = new CollisionFilter
            {
                BelongsTo = 1,
                CollidesWith = 1,
                GroupIndex = 1
            };

            var filter3 = new CollisionFilter
            {
                BelongsTo = 0,
                CollidesWith = 0,
                GroupIndex = -1
            };

            var filter4 = new CollisionFilter
            {
                BelongsTo = 1,
                CollidesWith = 1,
                GroupIndex = 0
            };

            Assert.IsTrue(CollisionFilter.CreateUnion(CollisionFilter.Zero, CollisionFilter.Default)
                .Equals(CollisionFilter.Default));

            Assert.IsTrue(CollisionFilter.CreateUnion(filter0, CollisionFilter.Zero).Equals(CollisionFilter.Zero));
            Assert.IsTrue(CollisionFilter.CreateUnion(filter1, CollisionFilter.Zero).Equals(filter4));
            Assert.IsTrue(CollisionFilter.CreateUnion(filter2, CollisionFilter.Zero).Equals(filter4));
            Assert.IsTrue(CollisionFilter.CreateUnion(filter3, CollisionFilter.Zero).Equals(CollisionFilter.Zero));
            Assert.IsTrue(CollisionFilter.CreateUnion(filter4, CollisionFilter.Zero).Equals(filter4));

            Assert.IsTrue(CollisionFilter.CreateUnion(filter0, filter1).Equals(filter4));
            Assert.IsTrue(CollisionFilter.CreateUnion(filter1, filter3).Equals(filter1));
            Assert.IsTrue(CollisionFilter.CreateUnion(filter1, filter2).Equals(filter4));
            Assert.IsTrue(CollisionFilter.CreateUnion(filter2, filter3).Equals(filter4));
            Assert.IsTrue(CollisionFilter.CreateUnion(filter3, filter4).Equals(filter4));
        }
    }
}
