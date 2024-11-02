using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Editor.Bridge;
using UnityEngine;
using UnityEngine.SceneManagement;
using Step = Unity.Entities.Editor.HierarchyNodeStore.IntegrateGameObjectChangesEnumerator.Step;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    sealed class IntegrateGameObjectChangesEnumeratorTests
    {
        HierarchyNodeStore m_HierarchyNodeStore;
        SubSceneMap m_Mapping;
        HierarchyGameObjectChanges m_Changes;

        [SetUp]
        public void SetUp()
        {
            m_Mapping = new SubSceneMap();
            m_HierarchyNodeStore = new HierarchyNodeStore(Allocator.Persistent);
            m_Changes = new HierarchyGameObjectChanges(Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            m_HierarchyNodeStore.Dispose();
            m_Mapping.Dispose();
            m_Changes.Dispose();
        }

        [Test]
        public void EnsureStepSequenceIsInCorrectOrder()
        {
            var testScene = new Scene();
            var testGO = new GameObject();
            m_Changes.LoadedScenes.Add(testScene);
            m_Changes.UnloadedScenes.Add(testScene);
            m_Changes.GameObjectChangeTrackerEvents.Add(new GameObjectChangeTrackerEvent(testGO.GetInstanceID(), GameObjectChangeTrackerEventType.CreatedOrChanged));

            var iterator = m_HierarchyNodeStore.CreateIntegrateGameObjectChangesEnumerator(m_Changes, m_Mapping, 10);

            Assert.That(iterator.CurrentStep, Is.EqualTo(Step.HandleUnloadedScenes));
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(iterator.CurrentStep, Is.EqualTo(Step.HandleLoadedScenes));
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(iterator.CurrentStep, Is.EqualTo(Step.IntegrateChanges));
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(iterator.CurrentStep, Is.EqualTo(Step.Complete));
            Assert.That(iterator.MoveNext(), Is.False);
        }

        [Test]
        public void CanSkipSteps([Values] bool hasUnloadedScene, [Values] bool hasLoadedScene, [Values] bool hasGameObjectChanges)
        {
            var testScene = new Scene();
            var testGO = new GameObject();

            var expectedSteps = new List<Step>();
            if (hasUnloadedScene)
            {
                m_Changes.UnloadedScenes.Add(testScene);
                expectedSteps.Add(Step.HandleUnloadedScenes);
            }

            if (hasLoadedScene)
            {
                m_Changes.LoadedScenes.Add(testScene);
                expectedSteps.Add(Step.HandleLoadedScenes);
            }

            if (hasGameObjectChanges)
            {
                m_Changes.GameObjectChangeTrackerEvents.Add(new GameObjectChangeTrackerEvent(testGO.GetInstanceID(), GameObjectChangeTrackerEventType.CreatedOrChanged));
                expectedSteps.Add(Step.IntegrateChanges);
            }

            expectedSteps.Add(Step.Complete);

            var iterator = m_HierarchyNodeStore.CreateIntegrateGameObjectChangesEnumerator(m_Changes, m_Mapping, 10);
            var steps = new List<Step>();
            while (true)
            {
                steps.Add(iterator.CurrentStep);
                if (!iterator.MoveNext())
                    break;
            }

            Assert.That(steps, Is.EqualTo(expectedSteps));
        }

        [Test]
        public void IntegrateGameObjectChangesOverMultipleIterations()
        {
            for (var i = 0; i < 30; i++)
            {
                var gameObject = new GameObject();
                m_Changes.GameObjectChangeTrackerEvents.Add(new GameObjectChangeTrackerEvent(gameObject.GetInstanceID(), GameObjectChangeTrackerEventType.CreatedOrChanged));
            }

            var iterator = m_HierarchyNodeStore.CreateIntegrateGameObjectChangesEnumerator(m_Changes, m_Mapping,10);
            Assert.That(iterator.CurrentStep, Is.EqualTo(Step.IntegrateChanges));
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(iterator.CurrentStep, Is.EqualTo(Step.IntegrateChanges));
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(iterator.CurrentStep, Is.EqualTo(Step.IntegrateChanges));
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(iterator.CurrentStep, Is.EqualTo(Step.Complete));
            Assert.That(iterator.MoveNext(), Is.False);
        }
    }
}
