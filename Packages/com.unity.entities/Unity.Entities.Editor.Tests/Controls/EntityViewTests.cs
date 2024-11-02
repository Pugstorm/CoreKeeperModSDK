#if !DOTS_DISABLE_DEBUG_NAMES
using NUnit.Framework;
using Unity.Collections;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    class EntityViewTests
    {
        [Test]
        public void EntityView_GeneratesCorrectVisualHierarchy()
        {
            using var world = new World("EntityViewTestWorld");
            var archetype = world.EntityManager.CreateArchetype(typeof(EntityGuid));
            using var entities = world.EntityManager.CreateEntity(archetype, 2, world.UpdateAllocator.ToAllocator);
            for (var i = 0; i < entities.Length; i++)
            {
                world.EntityManager.SetName(entities[i], $"EntityViewTest_Entity{i}");
            }

            var entityView0 = new EntityView(new EntityViewData(world, entities[0]));
            var entityView1 = new EntityView(new EntityViewData(world, entities[1]));
            Assert.That(entityView0.Q<VisualElement>(className: UssClasses.EntityView.GoTo), Is.Not.Null);
            Assert.That(entityView1.Q<VisualElement>(className: UssClasses.EntityView.GoTo), Is.Not.Null);
            Assert.That(entityView0.Q<Label>(className: UssClasses.EntityView.EntityName).text, Is.EqualTo("EntityViewTest_Entity0"));
            Assert.That(entityView1.Q<Label>(className: UssClasses.EntityView.EntityName).text, Is.EqualTo("EntityViewTest_Entity1"));
        }
    }
}
#endif
