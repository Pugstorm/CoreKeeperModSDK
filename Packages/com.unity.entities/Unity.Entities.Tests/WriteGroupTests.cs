using System;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Tests
{
    [TestFixture]
    class WriteGroupTests : ECSTestsFixture
    {
        //
        //     +-----------+        +----------+
        //     |TestOutputA<--------+TestInputB|
        //     +-----------+    ^   +----------+
        //          ^           |   +----------+
        //          |           +---+TestInputC|
        //     +-----------+    ^   +----------+
        //     |TestOutputB<----+   +----------+
        //     +-----------+    +---+TestInputD|
        //                          +----------+

        struct TestOutputA : IComponentData
        {
        }

        [WriteGroup(typeof(TestOutputA))]
        struct TestOutputB : IComponentData
        {
        }

        [WriteGroup(typeof(TestOutputA))]
        [WriteGroup(typeof(TestOutputB))]
        struct TestInputB : IComponentData
        {
        }

        [WriteGroup(typeof(TestOutputA))]
        [WriteGroup(typeof(TestOutputB))]
        struct TestInputC : IComponentData
        {
        }

        [WriteGroup(typeof(TestOutputA))]
        [WriteGroup(typeof(TestOutputB))]
        struct TestInputD : IComponentData
        {
        }


        [Test]
        public void WG_AllOnlyMatchesExplicit()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestInputB), typeof(TestInputC));
            var archetype1 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestInputB), typeof(TestInputC),
                typeof(TestInputD));
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<TestOutputA>()
                .WithAll<TestInputB, TestInputC>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(m_Manager);

            m_Manager.CreateEntity(archetype0);
            m_Manager.CreateEntity(archetype1);

            var results0 = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, results0.Length);
        }

        [Test]
        public void WG_AllOnlyMatchesExplicitLateDefinition()
        {
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<TestOutputA>()
                .WithAll<TestInputB, TestInputC>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(m_Manager);

            var archetype0 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestInputB), typeof(TestInputC));
            var archetype1 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestInputB), typeof(TestInputC),
                typeof(TestInputD));

            m_Manager.CreateEntity(archetype0);
            m_Manager.CreateEntity(archetype1);

            var results0 = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, results0.Length);
        }

        [Test]
        public void WG_AllOnlyMatchesExtended()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestInputB), typeof(TestInputC));
            var archetype1 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestInputB), typeof(TestInputC),
                typeof(TestInputD));
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<TestOutputA>()
                .WithAll<TestInputB, TestInputC, TestInputD>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(m_Manager);

            m_Manager.CreateEntity(archetype0);
            m_Manager.CreateEntity(archetype1);

            var results0 = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, results0.Length);
        }

        [Test]
        public void WG_AnyOnlyMatchesExplicit()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestInputB), typeof(TestInputC));
            var archetype1 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestInputB), typeof(TestInputC),
                typeof(TestInputD));
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAnyRW<TestOutputA>()
                .WithAny<TestInputB, TestInputC>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(m_Manager);

            m_Manager.CreateEntity(archetype0);
            m_Manager.CreateEntity(archetype1);

            var results0 = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, results0.Length);
        }

        [Test]
        public void WG_AnyMatchesAll()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestInputB), typeof(TestInputC));
            var archetype1 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestInputB), typeof(TestInputC),
                typeof(TestInputD));
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAnyRW<TestOutputA>()
                .WithAny<TestInputB, TestInputC, TestInputD>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(m_Manager);

            m_Manager.CreateEntity(archetype0);
            m_Manager.CreateEntity(archetype1);

            var results0 = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(2, results0.Length);
        }

        [Test]
        public void WG_AnyExplicitlyExcludesExtension()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestInputB), typeof(TestInputC));
            var archetype1 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestInputB), typeof(TestInputC),
                typeof(TestInputD));
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAnyRW<TestOutputA>()
                .WithAny<TestInputB, TestInputC>()
                .WithNone<TestInputD>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(m_Manager);

            m_Manager.CreateEntity(archetype0);
            m_Manager.CreateEntity(archetype1);

            var results0 = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, results0.Length);
        }

        [Test]
        public void WG_AllAllowsDependentWriteGroups()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestOutputB), typeof(TestInputB),
                typeof(TestInputC));
            var archetype1 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestOutputB), typeof(TestInputB),
                typeof(TestInputC), typeof(TestInputD));
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<TestOutputA>()
                .WithAll<TestOutputB>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(m_Manager);

            m_Manager.CreateEntity(archetype0);
            m_Manager.CreateEntity(archetype1);

            var results0 = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(2, results0.Length);
        }

        [Test]
        public void WG_AllExcludesFromDependentWriteGroup()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestOutputB), typeof(TestInputB),
                typeof(TestInputC));
            var archetype1 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestOutputB), typeof(TestInputB),
                typeof(TestInputC), typeof(TestInputD));
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<TestOutputA>()
                .WithAll<TestInputB>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(m_Manager);

            m_Manager.CreateEntity(archetype0);
            m_Manager.CreateEntity(archetype1);

            var results0 = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(0, results0.Length);
        }

        [Test]
        public void WG_NotExcludesWhenOverrideWriteGroup()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestOutputB), typeof(TestInputB),
                typeof(TestInputC));
            var archetype1 = m_Manager.CreateArchetype(typeof(TestOutputA), typeof(TestOutputB), typeof(TestInputB),
                typeof(TestInputC), typeof(TestInputD));
            // Not specified Options = EntityQueryOptions.FilterWriteGroup means that WriteGroup is being overridden (ignored)
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<TestOutputA>()
                .WithAll<TestInputB>()
                .Build(m_Manager);

            m_Manager.CreateEntity(archetype0);
            m_Manager.CreateEntity(archetype1);

            var results0 = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(2, results0.Length);
        }

        struct TestEnableableA : IComponentData, IEnableableComponent
        {
        }

        [WriteGroup(typeof(TestEnableableA))]
        struct TestEnableableB : IComponentData, IEnableableComponent
        {
        }

        [Test]
        public void WG_WithEnableableComponent_WithAll_MatchesExpectedEntities()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(TestEnableableA));
            var archetypeB = m_Manager.CreateArchetype(typeof(TestEnableableB));
            var archetypeAB = m_Manager.CreateArchetype(typeof(TestEnableableA), typeof(TestEnableableB));
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<TestEnableableA>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(m_Manager);

            Entity ent_A = m_Manager.CreateEntity(archetypeA);
            Entity ent_dA = m_Manager.CreateEntity(archetypeA);
            Entity ent_B = m_Manager.CreateEntity(archetypeB);
            Entity ent_dB = m_Manager.CreateEntity(archetypeB);
            Entity ent_A_B = m_Manager.CreateEntity(archetypeAB);
            Entity ent_dA_B = m_Manager.CreateEntity(archetypeAB);
            Entity ent_A_dB = m_Manager.CreateEntity(archetypeAB);
            Entity ent_dA_dB = m_Manager.CreateEntity(archetypeAB);
            m_Manager.SetComponentEnabled<TestEnableableA>(ent_dA, false);
            m_Manager.SetComponentEnabled<TestEnableableB>(ent_dB, false);
            m_Manager.SetComponentEnabled<TestEnableableA>(ent_dA_B, false);
            m_Manager.SetComponentEnabled<TestEnableableB>(ent_A_dB, false);
            m_Manager.SetComponentEnabled<TestEnableableA>(ent_dA_dB, false);
            m_Manager.SetComponentEnabled<TestEnableableB>(ent_dA_dB, false);

            var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            CollectionAssert.AreEquivalent(new[] { ent_A, ent_A_dB }, results);
        }

        [Test]
        public void WG_WithEnableableComponent_WithDisabled_MatchesExpectedEntities()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(TestEnableableA));
            var archetypeB = m_Manager.CreateArchetype(typeof(TestEnableableB));
            var archetypeAB = m_Manager.CreateArchetype(typeof(TestEnableableA), typeof(TestEnableableB));
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithDisabledRW<TestEnableableA>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(m_Manager);

            Entity ent_A = m_Manager.CreateEntity(archetypeA);
            Entity ent_dA = m_Manager.CreateEntity(archetypeA);
            Entity ent_B = m_Manager.CreateEntity(archetypeB);
            Entity ent_dB = m_Manager.CreateEntity(archetypeB);
            Entity ent_A_B = m_Manager.CreateEntity(archetypeAB);
            Entity ent_dA_B = m_Manager.CreateEntity(archetypeAB);
            Entity ent_A_dB = m_Manager.CreateEntity(archetypeAB);
            Entity ent_dA_dB = m_Manager.CreateEntity(archetypeAB);
            m_Manager.SetComponentEnabled<TestEnableableA>(ent_dA, false);
            m_Manager.SetComponentEnabled<TestEnableableB>(ent_dB, false);
            m_Manager.SetComponentEnabled<TestEnableableA>(ent_dA_B, false);
            m_Manager.SetComponentEnabled<TestEnableableB>(ent_A_dB, false);
            m_Manager.SetComponentEnabled<TestEnableableA>(ent_dA_dB, false);
            m_Manager.SetComponentEnabled<TestEnableableB>(ent_dA_dB, false);

            var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            CollectionAssert.AreEquivalent(new[] { ent_dA, ent_dA_dB }, results);
        }
    }
}
