using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Burst;
using Unity.Collections;
using Unity.Transforms;
using System.Text.RegularExpressions;
using System.Linq;

namespace Unity.Entities.Tests
{
    [BurstCompile]
    class ComponentSystemGroupTests : ECSTestsFixture
    {
        partial class TestGroup : ComponentSystemGroup
        {
        }

        private partial class TestSystemBase : SystemBase
        {
            protected override void OnUpdate() => throw new System.NotImplementedException();
        }

        [Test]
        public void SortEmptyParentSystem()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            Assert.DoesNotThrow(() => { parent.SortSystems(); });
        }

        [Test]
        public void SettingParent_ThenDeletingParentInSameFrame_DoesNotThrow()
        {
            var parentSystem = World.GetOrCreateSystem<ParentSystem>();
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddComponentData(child, new Parent { Value = parent });
            m_Manager.DestroyEntity(parent);
            Assert.DoesNotThrow(() =>
                parentSystem.Update(World.Unmanaged)
            );
            Assert.IsFalse(m_Manager.HasComponent<Parent>(child));
        }

        partial class TestSystem : TestSystemBase
        {
            protected override void OnUpdate() { Dependency = default; }
        }

        partial class TestSystem2 : TestSystemBase
        {
            protected override void OnUpdate() { Dependency = default; }
        }

        partial class TestSystem3 : TestSystemBase
        {
            protected override void OnUpdate() { Dependency = default; }
        }

        [Test]
        public void SortOneChildSystem()
        {
            var parent = World.CreateSystemManaged<TestGroup>();

            var child = World.CreateSystemManaged<TestSystem>();
            parent.AddSystemToUpdateList(child);
            parent.SortSystems();
            CollectionAssert.AreEqual(new[] {child}, parent.ManagedSystems);
        }

        [UpdateAfter(typeof(Sibling2System))]
        partial class Sibling1System : TestSystemBase
        {
            protected override void OnUpdate()  { Dependency = default; }
        }
        partial class Sibling2System : TestSystemBase
        {
            protected override void OnUpdate()  { Dependency = default; }
        }

        [Test]
        public void SortTwoChildSystems_CorrectOrder()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var child1 = World.CreateSystemManaged<Sibling1System>();
            var child2 = World.CreateSystemManaged<Sibling2System>();
            parent.AddSystemToUpdateList(child1);
            parent.AddSystemToUpdateList(child2);
            parent.SortSystems();
            CollectionAssert.AreEqual(new TestSystemBase[] {child2, child1}, parent.ManagedSystems);
        }

        [Test]
        public void SortThreeChildSystemsWithSameName_PreserveOriginalOrder()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var child1 = World.CreateSystemManaged<Sibling1System>();
            var child2 = World.CreateSystemManaged<Sibling1System>();
            var child3 = World.CreateSystemManaged<Sibling1System>();
            parent.AddSystemToUpdateList(child1);
            parent.AddSystemToUpdateList(child2);
            parent.AddSystemToUpdateList(child3);
            parent.SortSystems();
            CollectionAssert.AreEqual(new TestSystemBase[] {child1, child2, child3}, parent.ManagedSystems);
        }

        // This test constructs the following system dependency graph:
        // 1 -> 2 -> 3 -> 4 -v
        //           ^------ 5 -> 6
        // The expected results of topologically sorting this graph:
        // - systems 1 and 2 are properly sorted in the system update list.
        // - systems 3, 4, and 5 form a cycle (in that order, or equivalent).
        // - system 6 is not sorted AND is not part of the cycle.
        [UpdateBefore(typeof(Circle2System))]
        partial class Circle1System : TestSystemBase
        {
        }
        [UpdateBefore(typeof(Circle3System))]
        partial class Circle2System : TestSystemBase
        {
        }
        [UpdateAfter(typeof(Circle5System))]
        partial class Circle3System : TestSystemBase
        {
        }
        [UpdateAfter(typeof(Circle3System))]
        partial class Circle4System : TestSystemBase
        {
        }
        [UpdateAfter(typeof(Circle4System))]
        partial class Circle5System : TestSystemBase
        {
        }
        [UpdateAfter(typeof(Circle5System))]
        partial class Circle6System : TestSystemBase
        {
        }

        // https://unity3d.atlassian.net/browse/DOTSR-1432

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires system safety checks")]
        public void DetectCircularDependency_Throws()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var child1 = World.CreateSystemManaged<Circle1System>();
            var child2 = World.CreateSystemManaged<Circle2System>();
            var child3 = World.CreateSystemManaged<Circle3System>();
            var child4 = World.CreateSystemManaged<Circle4System>();
            var child5 = World.CreateSystemManaged<Circle5System>();
            var child6 = World.CreateSystemManaged<Circle6System>();
            parent.AddSystemToUpdateList(child3);
            parent.AddSystemToUpdateList(child6);
            parent.AddSystemToUpdateList(child2);
            parent.AddSystemToUpdateList(child4);
            parent.AddSystemToUpdateList(child1);
            parent.AddSystemToUpdateList(child5);
            
            var e = Assert.Throws<InvalidOperationException>(() =>  parent.SortSystems());
            // Make sure the cycle expressed in e.Chain is the one we expect, even though it could start at any node
            // in the cycle.

            Assert.AreEqual(@"The following systems form a circular dependency cycle (check their [*Before]/[*After] attributes):
- Unity.Entities.Tests.ComponentSystemGroupTests+Circle3System
- Unity.Entities.Tests.ComponentSystemGroupTests+Circle4System
- Unity.Entities.Tests.ComponentSystemGroupTests+Circle5System", e.Message);

        }

        partial class Unconstrained1System : TestSystemBase
        {
        }
        partial class Unconstrained2System : TestSystemBase
        {
        }
        partial class Unconstrained3System : TestSystemBase
        {
        }
        partial class Unconstrained4System : TestSystemBase
        {
        }

        [Test]
        public void SortUnconstrainedSystems_IsDeterministic()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var child1 = World.CreateSystemManaged<Unconstrained1System>();
            var child2 = World.CreateSystemManaged<Unconstrained2System>();
            var child3 = World.CreateSystemManaged<Unconstrained3System>();
            var child4 = World.CreateSystemManaged<Unconstrained4System>();
            parent.AddSystemToUpdateList(child2);
            parent.AddSystemToUpdateList(child4);
            parent.AddSystemToUpdateList(child3);
            parent.AddSystemToUpdateList(child1);
            parent.SortSystems();
            CollectionAssert.AreEqual(new TestSystemBase[] {child1,child2,  child4,  child3}, parent.ManagedSystems);
        }

        private partial class UpdateCountingSystemBase : SystemBase
        {
            public int CompleteUpdateCount = 0;
            protected override void OnUpdate()
            {
                ++CompleteUpdateCount;
            }
        }
        partial class NonThrowing1System : UpdateCountingSystemBase
        {
        }
        partial class NonThrowing2System : UpdateCountingSystemBase
        {
        }
        partial class ThrowingSystem : UpdateCountingSystemBase
        {
            public string ExceptionMessage = "I should always throw!";
            protected override void OnUpdate()
            {
                if (CompleteUpdateCount == 0)
                {
                    throw new InvalidOperationException(ExceptionMessage);
                }
                base.OnUpdate();
            }
        }

        [Test]
        public void SystemInGroupThrows_LaterSystemsRun()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var child1 = World.CreateSystemManaged<NonThrowing1System>();
            var child2 = World.CreateSystemManaged<ThrowingSystem>();
            var child3 = World.CreateSystemManaged<NonThrowing2System>();
            parent.AddSystemToUpdateList(child1);
            parent.AddSystemToUpdateList(child2);
            parent.AddSystemToUpdateList(child3);
            LogAssert.Expect(LogType.Exception, new Regex(child2.ExceptionMessage));
            parent.Update();
            LogAssert.NoUnexpectedReceived();
            Assert.AreEqual(1, child1.CompleteUpdateCount);
            Assert.AreEqual(0, child2.CompleteUpdateCount);
            Assert.AreEqual(1, child3.CompleteUpdateCount);
        }

        [Test]
        public void SystemThrows_SystemNotRemovedFromUpdate()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var child = World.CreateSystemManaged<ThrowingSystem>();
            parent.AddSystemToUpdateList(child);
            LogAssert.Expect(LogType.Exception, new Regex(child.ExceptionMessage));
            parent.Update();
            LogAssert.Expect(LogType.Exception, new Regex(child.ExceptionMessage));
            parent.Update();
            LogAssert.NoUnexpectedReceived();

            Assert.AreEqual(0, child.CompleteUpdateCount);
        }

        [UpdateAfter(typeof(NonSibling2System))]
        partial class NonSibling1System : TestSystemBase
        {
        }
        [UpdateBefore(typeof(NonSibling1System))]
        partial class NonSibling2System : TestSystemBase
        {
        }

        [Test]
        public void ComponentSystemGroup_UpdateAfterTargetIsNotSibling_LogsWarning()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var child = World.CreateSystemManaged<NonSibling1System>();
            LogAssert.Expect(LogType.Warning, new Regex(@"Ignoring invalid \[Unity.Entities.UpdateAfterAttribute\] attribute on .+NonSibling1System targeting.+NonSibling2System"));
            parent.AddSystemToUpdateList(child);
            parent.SortSystems();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ComponentSystemGroup_UpdateBeforeTargetIsNotSibling_LogsWarning()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var child = World.CreateSystemManaged<NonSibling2System>();
            LogAssert.Expect(LogType.Warning, new Regex(@"Ignoring invalid \[Unity.Entities.UpdateBeforeAttribute\] attribute on .+NonSibling2System targeting.+NonSibling1System"));
            parent.AddSystemToUpdateList(child);
            parent.SortSystems();
            LogAssert.NoUnexpectedReceived();
        }

        [UpdateAfter(typeof(NotEvenASystem))]
        partial class InvalidUpdateAfterSystem : TestSystemBase
        {
        }
        [UpdateBefore(typeof(NotEvenASystem))]
        partial class InvalidUpdateBeforeSystem : TestSystemBase
        {
        }
        class NotEvenASystem
        {
        }

        [Test]
        public void ComponentSystemGroup_UpdateAfterTargetIsNotSystem_LogsWarning()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var child = World.CreateSystemManaged<InvalidUpdateAfterSystem>();
            LogAssert.Expect(LogType.Warning, new Regex(@"Ignoring invalid \[Unity.Entities.UpdateAfterAttribute\].+InvalidUpdateAfterSystem.+NotEvenASystem is not a subclass of ComponentSystemBase and does not implement ISystem"));
            parent.AddSystemToUpdateList(child);
            parent.SortSystems();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ComponentSystemGroup_UpdateBeforeTargetIsNotSystem_LogsWarning()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var child = World.CreateSystemManaged<InvalidUpdateBeforeSystem>();
            LogAssert.Expect(LogType.Warning, new Regex(@"Ignoring invalid \[Unity.Entities.UpdateBeforeAttribute\].+InvalidUpdateBeforeSystem.+NotEvenASystem is not a subclass of ComponentSystemBase and does not implement ISystem"));
            parent.AddSystemToUpdateList(child);
            parent.SortSystems();
            LogAssert.NoUnexpectedReceived();
        }

        [UpdateAfter(typeof(UpdateAfterSelfSystem))]
        partial class UpdateAfterSelfSystem : TestSystemBase
        {
        }
        [UpdateBefore(typeof(UpdateBeforeSelfSystem))]
        partial class UpdateBeforeSelfSystem : TestSystemBase
        {
        }

        [Test]
        public void ComponentSystemGroup_UpdateAfterTargetIsSelf_LogsWarning()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var child = World.CreateSystemManaged<UpdateAfterSelfSystem>();
            LogAssert.Expect(LogType.Warning, new Regex(@"Ignoring invalid \[Unity.Entities.UpdateAfterAttribute\].+UpdateAfterSelfSystem.+cannot be updated or created after or before itself."));
            parent.AddSystemToUpdateList(child);
            parent.SortSystems();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ComponentSystemGroup_UpdateBeforeTargetIsSelf_LogsWarning()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var child = World.CreateSystemManaged<UpdateBeforeSelfSystem>();
            LogAssert.Expect(LogType.Warning, new Regex(@"Ignoring invalid \[Unity.Entities.UpdateBeforeAttribute\].+UpdateBeforeSelfSystem.+cannot be updated or created after or before itself."));
            parent.AddSystemToUpdateList(child);
            parent.SortSystems();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ComponentSystemGroup_AddNullToUpdateList_QuietNoOp()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            Assert.DoesNotThrow(() => { parent.AddSystemToUpdateList(null); });
            Assert.IsEmpty(parent.ManagedSystems);
        }

        [Test]
        public void ComponentSystemGroup_AddSelfToUpdateList_Throws()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            Assert.That(() => { parent.AddSystemToUpdateList(parent); },
                Throws.ArgumentException.With.Message.Contains("to its own update list"));
        }
        partial class StartAndStopSystemGroup : ComponentSystemGroup
        {
            public List<int> Operations;
            protected override void OnCreate()
            {
                base.OnCreate();
                Operations = new List<int>(6);
            }

            protected override void OnStartRunning()
            {
                Operations.Add(0);
                base.OnStartRunning();
            }

            protected override void OnUpdate()
            {
                Operations.Add(1);
                base.OnUpdate();
            }

            protected override void OnStopRunning()
            {
                Operations.Add(2);
                base.OnStopRunning();
            }
        }

        partial class StartAndStopSystemA : SystemBase
        {
            private StartAndStopSystemGroup Group;
            protected override void OnCreate()
            {
                base.OnCreate();
                Group = World.GetExistingSystemManaged<StartAndStopSystemGroup>();
            }

            protected override void OnStartRunning()
            {
                Group.Operations.Add(10);
                base.OnStartRunning();
            }

            protected override void OnUpdate()
            {
                Group.Operations.Add(11);
            }

            protected override void OnStopRunning()
            {
                Group.Operations.Add(12);
                base.OnStopRunning();
            }
        }
        partial class StartAndStopSystemB : SystemBase
        {
            private StartAndStopSystemGroup Group;
            protected override void OnCreate()
            {
                base.OnCreate();
                Group = World.GetExistingSystemManaged<StartAndStopSystemGroup>();
            }

            protected override void OnStartRunning()
            {
                Group.Operations.Add(20);
                base.OnStartRunning();
            }

            protected override void OnUpdate()
            {
                Group.Operations.Add(21);
            }

            protected override void OnStopRunning()
            {
                Group.Operations.Add(22);
                base.OnStopRunning();
            }
        }
        partial class StartAndStopSystemC : SystemBase
        {
            private StartAndStopSystemGroup Group;
            protected override void OnCreate()
            {
                base.OnCreate();
                Group = World.GetExistingSystemManaged<StartAndStopSystemGroup>();
            }

            protected override void OnStartRunning()
            {
                Group.Operations.Add(30);
                base.OnStartRunning();
            }

            protected override void OnUpdate()
            {
                Group.Operations.Add(31);
            }

            protected override void OnStopRunning()
            {
                Group.Operations.Add(32);
                base.OnStopRunning();
            }
        }

        [Test]
        public void ComponentSystemGroup_OnStartRunningOnStopRunning_Recurses()
        {
            var parent = World.CreateSystemManaged<StartAndStopSystemGroup>();
            var childA = World.CreateSystemManaged<StartAndStopSystemA>();
            var childB = World.CreateSystemManaged<StartAndStopSystemB>();
            var childC = World.CreateSystemManaged<StartAndStopSystemC>();
            parent.AddSystemToUpdateList(childA);
            parent.AddSystemToUpdateList(childB);
            parent.AddSystemToUpdateList(childC);
            // child C is always disabled; make sure enabling/disabling the parent doesn't change that
            childC.Enabled = false;

            // first update
            parent.Update();
            CollectionAssert.AreEqual(parent.Operations.ToArray(), new[] {0, 1, 20, 21, 10, 11});
            parent.Operations.Clear();

            // second update with no new enabled/disabled
            parent.Update();
            CollectionAssert.AreEqual(parent.Operations.ToArray(), new[] {1, 21, 11});
            parent.Operations.Clear();

            // parent is disabled
            parent.Enabled = false;
            parent.Update();
            CollectionAssert.AreEqual(parent.Operations.ToArray(), new[] {2, 22, 12});
            parent.Operations.Clear();

            // parent is re-enabled
            parent.Enabled = true;
            parent.Update();
            CollectionAssert.AreEqual(parent.Operations.ToArray(), new[] {0, 1, 20, 21, 10, 11});
            parent.Operations.Clear();
        }

        partial class TrackUpdatedSystem : SystemBase
        {
            public List<ComponentSystemBase> Updated;

            protected override void OnUpdate()
            {
                Updated.Add(this);
            }
        }

        [Test]
        public void AddAndRemoveTakesEffectBeforeUpdate()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var childa = World.CreateSystemManaged<TrackUpdatedSystem>();
            var childb = World.CreateSystemManaged<TrackUpdatedSystem>();

            var updates = new List<ComponentSystemBase>();
            childa.Updated = updates;
            childb.Updated = updates;

            // Add 2 systems & validate Update calls
            parent.AddSystemToUpdateList(childa);
            parent.AddSystemToUpdateList(childb);
            parent.Update();

            // Order is not guaranteed
            Assert.IsTrue(updates.Count == 2 && updates.Contains(childa) && updates.Contains(childb));

            // Remove system & validate Update calls
            updates.Clear();
            parent.RemoveSystemFromUpdateList(childa);
            parent.Update();
            Assert.AreEqual(new ComponentSystemBase[] {childb}, updates.ToArray());
        }

        [UpdateInGroup(typeof(int))]
        public partial class GroupIsntAComponentSystem : EmptySystem
        {
        }

        [Test]
        public void UpdateInGroup_TargetNotASystem_Throws()
        {
            using World w = new World("Test World");

            // In hybrid, IsSystemAGroup() returns false for non-system inputs
            Assert.That(() => DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(w, typeof(GroupIsntAComponentSystem)),
                Throws.InvalidOperationException.With.Message.Contains("must be derived from ComponentSystemGroup"));
        }

        [UpdateInGroup(typeof(TestSystem))]
        public partial class GroupIsntAComponentSystemGroup : EmptySystem
        {
        }

        [Test]
        public void UpdateInGroup_TargetNotAGroup_Throws()
        {
            using World w = new World("Test World");
            Assert.That(() => DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(w, typeof(GroupIsntAComponentSystemGroup)),
                Throws.InvalidOperationException.With.Message.Contains("must be derived from ComponentSystemGroup"));
        }

        [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true, OrderLast = true)]
        public partial class FirstAndLast : EmptySystem
        {
        }

        [Test]
        public void UpdateInGroup_OrderFirstAndOrderLast_Throws()
        {
            using World w = new World("Test World");
            var systemTypes = new[] {typeof(FirstAndLast), typeof(TestGroup)};
            Assert.That(() => DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(w, systemTypes),
                Throws.InvalidOperationException.With.Message.Contains("can not specify both OrderFirst=true and OrderLast=true"));
        }

        // All the ordering constraints below are valid (though some are redundant). All should sort correctly without warnings.
        [UpdateInGroup(typeof(TestGroup), OrderFirst = true)]
        [UpdateBefore(typeof(FirstSystem))]
        public partial class FirstBeforeFirstSystem : EmptySystem { }

        [UpdateInGroup(typeof(TestGroup), OrderFirst = true)]
        [UpdateBefore(typeof(MiddleSystem))] // redundant
        [UpdateBefore(typeof(LastSystem))] // redundant
        public partial class FirstSystem : EmptySystem { }

        [UpdateInGroup(typeof(TestGroup), OrderFirst = true)]
        [UpdateAfter(typeof(FirstSystem))]
        public partial class FirstAfterFirstSystem : EmptySystem { }

        [UpdateInGroup(typeof(TestGroup))]
        [UpdateAfter(typeof(FirstSystem))] // redundant
        [UpdateBefore(typeof(MiddleSystem))]
        public partial class MiddleAfterFirstSystem : EmptySystem { }

        [UpdateInGroup(typeof(TestGroup))]
        public partial class MiddleSystem : EmptySystem { }

        [UpdateInGroup(typeof(TestGroup))]
        [UpdateAfter(typeof(MiddleSystem))]
        [UpdateBefore(typeof(LastSystem))] // redundant
        public partial class MiddleBeforeLastSystem : EmptySystem { }

        [UpdateInGroup(typeof(TestGroup), OrderLast = true)]
        [UpdateBefore(typeof(LastSystem))]
        public partial class LastBeforeLastSystem : EmptySystem { }

        [UpdateInGroup(typeof(TestGroup), OrderLast = true)]
        [UpdateAfter(typeof(FirstSystem))] // redundant
        [UpdateAfter(typeof(MiddleSystem))] // redundant
        public partial class LastSystem : EmptySystem { }

        [UpdateInGroup(typeof(TestGroup), OrderLast = true)]
        [UpdateAfter(typeof(LastSystem))]
        public partial class LastAfterLastSystem : EmptySystem { }

        [Test]
        public void ComponentSystemSorter_ValidUpdateConstraints_SortCorrectlyWithNoWarnings()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var systems = new List<EmptySystem>
            {
                World.CreateSystemManaged<FirstBeforeFirstSystem>(),
                World.CreateSystemManaged<FirstSystem>(),
                World.CreateSystemManaged<FirstAfterFirstSystem>(),
                World.CreateSystemManaged<MiddleAfterFirstSystem>(),
                World.CreateSystemManaged<MiddleSystem>(),
                World.CreateSystemManaged<MiddleBeforeLastSystem>(),
                World.CreateSystemManaged<LastBeforeLastSystem>(),
                World.CreateSystemManaged<LastSystem>(),
                World.CreateSystemManaged<LastAfterLastSystem>(),
            };
            // Insert in reverse order
            for (int i = systems.Count - 1; i >= 0; --i)
            {
                parent.AddSystemToUpdateList(systems[i]);
            }

            parent.SortSystems();

            CollectionAssert.AreEqual(systems, parent.ManagedSystems);
            LogAssert.NoUnexpectedReceived();
        }

        // Invalid constraints
        [UpdateInGroup(typeof(TestGroup), OrderFirst = true)]
        public partial class DummyFirstSystem : EmptySystem { }

        [UpdateInGroup(typeof(TestGroup), OrderLast = true)]
        public partial class DummyLastSystem : EmptySystem { }

        [UpdateInGroup(typeof(TestGroup), OrderFirst = true)]
        [UpdateAfter(typeof(DummyLastSystem))] // can't update after an OrderLast without also being OrderLast
        public partial class FirstAfterLastSystem : EmptySystem { }

        [UpdateInGroup(typeof(TestGroup))]
        [UpdateBefore(typeof(DummyFirstSystem))] // can't update before an OrderFirst without also being OrderFirst
        public partial class MiddleBeforeFirstSystem : EmptySystem { }

        [UpdateInGroup(typeof(TestGroup))]
        [UpdateAfter(typeof(DummyLastSystem))] // can't update after an OrderLast without also being OrderLast
        public partial class MiddleAfterLastSystem : EmptySystem { }

        [UpdateInGroup(typeof(TestGroup), OrderLast = true)]
        [UpdateBefore(typeof(DummyFirstSystem))] // can't update before an OrderFirst without also being OrderFirst
        public partial class LastBeforeFirstSystem : EmptySystem { }

        [Test] // runtime string formatting
        public void ComponentSystemSorter_OrderFirstUpdateAfterOrderLast_WarnAndIgnoreConstraint()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var systems = new List<EmptySystem>
            {
                World.CreateSystemManaged<FirstAfterLastSystem>(),
                World.CreateSystemManaged<DummyLastSystem>(),
            };
            // Insert in reverse order
            for (int i = systems.Count - 1; i >= 0; --i)
            {
                parent.AddSystemToUpdateList(systems[i]);
            }

            LogAssert.Expect(LogType.Warning, "Ignoring invalid [Unity.Entities.UpdateAfterAttribute(Unity.Entities.Tests.ComponentSystemGroupTests+DummyLastSystem)] attribute on Unity.Entities.Tests.ComponentSystemGroupTests+FirstAfterLastSystem because OrderFirst/OrderLast has higher precedence.");
            parent.SortSystems();
            LogAssert.NoUnexpectedReceived();

            CollectionAssert.AreEqual(systems, parent.ManagedSystems);
        }

        [Test] // runtime string formatting
        public void ComponentSystemSorter_MiddleUpdateBeforeOrderFirst_WarnAndIgnoreConstraint()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var systems = new List<EmptySystem>
            {
                World.CreateSystemManaged<DummyFirstSystem>(),
                World.CreateSystemManaged<MiddleBeforeFirstSystem>(),
            };
            // Insert in reverse order
            for (int i = systems.Count - 1; i >= 0; --i)
            {
                parent.AddSystemToUpdateList(systems[i]);
            }

            LogAssert.Expect(LogType.Warning, "Ignoring invalid [Unity.Entities.UpdateBeforeAttribute(Unity.Entities.Tests.ComponentSystemGroupTests+DummyFirstSystem)] attribute on Unity.Entities.Tests.ComponentSystemGroupTests+MiddleBeforeFirstSystem because OrderFirst/OrderLast has higher precedence.");
            parent.SortSystems();
            LogAssert.NoUnexpectedReceived();
            CollectionAssert.AreEqual(systems, parent.ManagedSystems);
        }

        [Test] // runtime string formatting
        public void ComponentSystemSorter_MiddleUpdateAfterOrderLast_WarnAndIgnoreConstraint()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var systems = new List<EmptySystem>
            {
                World.CreateSystemManaged<MiddleAfterLastSystem>(),
                World.CreateSystemManaged<DummyLastSystem>(),
            };
            // Insert in reverse order
            for (int i = systems.Count - 1; i >= 0; --i)
            {
                parent.AddSystemToUpdateList(systems[i]);
            }

            LogAssert.Expect(LogType.Warning, "Ignoring invalid [Unity.Entities.UpdateAfterAttribute(Unity.Entities.Tests.ComponentSystemGroupTests+DummyLastSystem)] attribute on Unity.Entities.Tests.ComponentSystemGroupTests+MiddleAfterLastSystem because OrderFirst/OrderLast has higher precedence.");
            parent.SortSystems();
            LogAssert.NoUnexpectedReceived();
            CollectionAssert.AreEqual(systems, parent.ManagedSystems);
        }

        [Test] // runtime string formatting
        public void ComponentSystemSorter_OrderLastUpdateBeforeOrderFirst_WarnAndIgnoreConstraint()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var systems = new List<EmptySystem>
            {
                World.CreateSystemManaged<DummyFirstSystem>(),
                World.CreateSystemManaged<LastBeforeFirstSystem>(),
            };
            // Insert in reverse order
            for (int i = systems.Count - 1; i >= 0; --i)
            {
                parent.AddSystemToUpdateList(systems[i]);
            }

            LogAssert.Expect(LogType.Warning, "Ignoring invalid [Unity.Entities.UpdateBeforeAttribute(Unity.Entities.Tests.ComponentSystemGroupTests+DummyFirstSystem)] attribute on Unity.Entities.Tests.ComponentSystemGroupTests+LastBeforeFirstSystem because OrderFirst/OrderLast has higher precedence.");
            parent.SortSystems();
            LogAssert.NoUnexpectedReceived();
            CollectionAssert.AreEqual(systems, parent.ManagedSystems);
        }

        [UpdateInGroup(typeof(TestGroup), OrderFirst = true)]
        public partial class OFL_A : EmptySystem
        {
        }

        [UpdateInGroup(typeof(TestGroup), OrderFirst = true)]
        [UpdateAfter(typeof(OFL_A))]
        public partial class OFL_B : EmptySystem
        {
        }

        public partial class OFL_C : EmptySystem
        {
        }

        [UpdateInGroup(typeof(TestGroup), OrderLast = true)]
        public partial class OFL_D : EmptySystem
        {
        }

        [UpdateInGroup(typeof(TestGroup), OrderLast = true)]
        [UpdateAfter(typeof(OFL_D))]
        public partial class OFL_E : EmptySystem
        {
        }

        [Test]
        public void OrderFirstLastWorks([Values(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 30, 31)] int bits)
        {
            var parent = World.CreateSystemManaged<TestGroup>();

            // Add in reverse order
            if (0 != (bits & (1 << 4))) { parent.AddSystemToUpdateList(World.CreateSystemManaged<OFL_E>()); }
            if (0 != (bits & (1 << 3))) { parent.AddSystemToUpdateList(World.CreateSystemManaged<OFL_D>()); }
            if (0 != (bits & (1 << 2))) { parent.AddSystemToUpdateList(World.CreateSystemManaged<OFL_C>()); }
            if (0 != (bits & (1 << 1))) { parent.AddSystemToUpdateList(World.CreateSystemManaged<OFL_B>()); }
            if (0 != (bits & (1 << 0))) { parent.AddSystemToUpdateList(World.CreateSystemManaged<OFL_A>()); }

            parent.SortSystems();

            // Ensure they are always in alphabetical order
            NativeText.ReadOnly prev = default;
            foreach (var sys in parent.ManagedSystems)
            {
                var curr = TypeManager.GetSystemName(sys.GetType());
                // no string.CompareTo() in DOTS Runtime, but in this case we know only the last character will be different
                int len = curr.Length;
                Assert.IsTrue(prev.IsEmpty || (prev[len-1] < curr[len-1]));
                prev = curr;
            }
        }

        [UpdateAfter(typeof(TestSystem))]
        partial struct MyUnmanagedSystem : ISystem
        {
        }

        partial struct MyUnmanagedSystem2 : ISystem
        {
        }

        partial struct MyUnmanagedSystem3 : ISystem
        {
        }

        [Test]
        public void NewSortWorksWithBoth()
        {
            var parent = World.CreateSystemManaged<TestGroup>();
            var sys = World.CreateSystem<MyUnmanagedSystem>();
            var s1 = World.GetOrCreateSystemManaged<TestSystem>();

            parent.AddSystemToUpdateList(sys);
            parent.AddSystemToUpdateList(s1);

            parent.SortSystems();
        }

        [Test]
        public void ComponentSystemGroup_RemoveThenReAddManagedSystem_SystemIsInGroup()
        {
            var group = World.CreateSystemManaged<TestGroup>();
            var sys = World.CreateSystemManaged<TestSystem>();
            group.AddSystemToUpdateList(sys);

            group.RemoveSystemFromUpdateList(sys);
            group.AddSystemToUpdateList(sys);
            // This is where removals are processed
            group.SortSystems();
            var expectedSystems = new List<ComponentSystemBase> {sys};
            CollectionAssert.AreEqual(expectedSystems, group.ManagedSystems);
        }

        [Test]
        public void ComponentSystemGroup_RemoveSystemNotInGroup_Ignored()
        {
            var group = World.CreateSystemManaged<TestGroup>();
            var sys = World.CreateSystemManaged<TestSystem>();
            // group.AddSystemToUpdateList(sys); // the point here is to remove a system _not_ in the group
            group.RemoveSystemFromUpdateList(sys);
            Assert.AreEqual(0, group.m_managedSystemsToRemove.Count);
        }

        [Test]
        public void ComponentSystemGroup_DuplicateRemove_Ignored()
        {
            var group = World.CreateSystemManaged<TestGroup>();
            var sys = World.CreateSystemManaged<TestSystem>();
            group.AddSystemToUpdateList(sys);

            group.RemoveSystemFromUpdateList(sys);
            group.RemoveSystemFromUpdateList(sys);
            var expectedSystems = new List<ComponentSystemBase> {sys};
            CollectionAssert.AreEqual(expectedSystems, group.m_managedSystemsToRemove);
        }

        partial struct UnmanagedTestSystem : ISystem
        {
        }

        [Test]
        public void ComponentSystemGroup_RemoveThenReAddUnmanagedSystem_SystemIsInGroup()
        {
            var group = World.CreateSystemManaged<TestGroup>();
            var sys = World.CreateSystem<UnmanagedTestSystem>();
            group.AddSystemToUpdateList(sys);
            Assert.IsTrue(group.m_UnmanagedSystemsToUpdate.Contains(sys), "system not in group after initial add");

            group.RemoveSystemFromUpdateList(sys);
            group.AddSystemToUpdateList(sys);
            Assert.IsTrue(group.m_UnmanagedSystemsToUpdate.Contains(sys), "system not in group after remove-and-add");

            group.SortSystems();
            Assert.IsTrue(group.m_UnmanagedSystemsToUpdate.Contains(sys), "system not in group after re-sorting");
        }

        [Test]
        public void ComponentSystemGroup_RemoveUnmanagedSystemNotInGroup_Ignored()
        {
            var group = World.CreateSystemManaged<TestGroup>();
            var sys = World.CreateSystem<UnmanagedTestSystem>();
            // group.AddSystemToUpdateList(sys); // the point here is to remove a system _not_ in the group
            group.RemoveSystemFromUpdateList(sys);
            Assert.AreEqual(0, group.m_UnmanagedSystemsToRemove.Length);
        }

        [Test]
        public void ComponentSystemGroup_DuplicateRemoveUnmanaged_Ignored()
        {
            var group = World.CreateSystemManaged<TestGroup>();
            var sys = World.CreateSystem<UnmanagedTestSystem>();
            group.AddSystemToUpdateList(sys);

            group.RemoveSystemFromUpdateList(sys);
            group.RemoveSystemFromUpdateList(sys);
            var expectedSystems = new List<SystemHandle> {sys};
            Assert.AreEqual(1, group.m_UnmanagedSystemsToRemove.Length);
            Assert.AreEqual(sys, group.m_UnmanagedSystemsToRemove[0]);
        }

        [Test]
        public void ComponentSystemGroup_NullRateManager_DoesntThrow()
        {
            var group = World.CreateSystemManaged<TestGroup>();
            group.RateManager = null;
            Assert.DoesNotThrow(() => { group.Update(); });
        }

        partial class ParentSystemGroup : ComponentSystemGroup
        {
        }

        partial class ChildSystemGroup : ComponentSystemGroup
        {
        }

        [Test]
        public void ComponentSystemGroup_SortCleanParentWithDirtyChild_ChildIsSorted()
        {
            var parentGroup = World.CreateSystemManaged<ParentSystemGroup>();
            var childGroup = World.CreateSystemManaged<ChildSystemGroup>();
            parentGroup.AddSystemToUpdateList(childGroup); // parent group sort order is dirty
            parentGroup.SortSystems(); // parent group sort order is clean

            var child1 = World.CreateSystemManaged<Sibling1System>();
            var child2 = World.CreateSystemManaged<Sibling2System>();
            childGroup.AddSystemToUpdateList(child1); // child group sort order is dirty
            childGroup.AddSystemToUpdateList(child2);
            parentGroup.SortSystems(); // parent and child group sort orders should be clean

            // If the child group's systems aren't in the correct order, it wasn't recursively sorted by the parent group.
            CollectionAssert.AreEqual(new TestSystemBase[] {child2, child1}, childGroup.ManagedSystems);
        }

        internal partial class NoSortGroup : ComponentSystemGroup
        {
            public NoSortGroup()
            {
                EnableSystemSorting = false;
            }

            // Toggling sorting at runtime is NOT an expected use case; this is a hack for testing purposes
            public void SetSortingEnabled(bool enabled)
            {
                EnableSystemSorting = enabled;
            }
        }

        [Test]
        public void ComponentSystemGroup_SortManuallySortedParentWithDirtyChild_ChildIsSorted()
        {
            var parentGroup = World.CreateSystemManaged<NoSortGroup>();
            var childGroup = World.CreateSystemManaged<ChildSystemGroup>();
            parentGroup.AddSystemToUpdateList(childGroup);

            var child1 = World.CreateSystemManaged<Sibling1System>();
            var child2 = World.CreateSystemManaged<Sibling2System>();
            childGroup.AddSystemToUpdateList(child1); // child group sort order is dirty
            childGroup.AddSystemToUpdateList(child2);
            parentGroup.SortSystems(); // parent and child group sort orders should be clean

            // If the child group's systems aren't in the correct order, it wasn't recursively sorted by the parent group.
            CollectionAssert.AreEqual(new TestSystemBase[] {child2, child1}, childGroup.ManagedSystems);
        }

        [Test]
        public void ComponentSystemGroup_ReEnableSorting_SystemsAreSorted()
        {
            var group = World.CreateSystemManaged<NoSortGroup>();
            var sibling1 = World.CreateSystemManaged<Sibling1System>();
            var sibling2 = World.CreateSystemManaged<Sibling2System>();
            group.AddSystemToUpdateList(sibling1);
            group.AddSystemToUpdateList(sibling2);
            CollectionAssert.AreEqual(new TestSystemBase[]{sibling1, sibling2}, group.ManagedSystems);
            // With sorting disabled, the group's systems are updated in insertion order.
            group.Update();
            CollectionAssert.AreEqual(new TestSystemBase[]{sibling1, sibling2}, group.ManagedSystems);
            // sibling1 has [UpdateAfter(sibling2)], so if sorting has happened, they should update as [sibling2, sibling1]
            group.SetSortingEnabled(true);
            group.Update();
            CollectionAssert.AreEqual(new TestSystemBase[]{sibling2, sibling1}, group.ManagedSystems);
        }

        [Test]
        public unsafe void ComponentSystemGroup_RemoveManagedFromManuallySortedGroup()
        {
            var group = World.CreateSystemManaged<NoSortGroup>();
            var unmanaged1  = World.CreateSystem<MyUnmanagedSystem>();
            var unmanaged2 = World.CreateSystem<MyUnmanagedSystem2>();
            var unmanaged3 = World.CreateSystem<MyUnmanagedSystem3>();

            var managed1 = World.CreateSystemManaged<TestSystem>();
            var managed2 = World.CreateSystemManaged<TestSystem2>();
            var managed3 = World.CreateSystemManaged<TestSystem3>();

            group.AddSystemToUpdateList(unmanaged1);
            group.AddSystemToUpdateList(managed1);
            group.AddSystemToUpdateList(unmanaged2);
            group.AddSystemToUpdateList(managed2);
            group.AddSystemToUpdateList(unmanaged3);
            group.AddSystemToUpdateList(managed3);

            group.RemoveSystemFromUpdateList(managed2);

            group.Update();

            var expectedUpdateList = new[]
            {
                new UpdateIndex(0, false),
                new UpdateIndex(0, true),
                new UpdateIndex(1, false),
                new UpdateIndex(2, false),
                new UpdateIndex(1, true)
            };

            for (int i = 0; i < expectedUpdateList.Length; ++i)
            {
                Assert.AreEqual(expectedUpdateList[i], group.m_MasterUpdateList[i]);
            }

            Assert.AreEqual(unmanaged1,group.m_UnmanagedSystemsToUpdate[0]);
            Assert.AreEqual(managed1.CheckedState()->m_SystemID,group.m_managedSystemsToUpdate[0].CheckedState()->m_SystemID);
            Assert.AreEqual(unmanaged2,group.m_UnmanagedSystemsToUpdate[1]);
            Assert.AreEqual(unmanaged3,group.m_UnmanagedSystemsToUpdate[2]);
            Assert.AreEqual(managed3.CheckedState()->m_SystemID,group.m_managedSystemsToUpdate[1].CheckedState()->m_SystemID);

        }

        [Test]
        public unsafe void ComponentSystemGroup_RemoveUnmanagedFromManuallySortedGroup()
        {
            var group = World.CreateSystemManaged<NoSortGroup>();
            var unmanaged1  = World.CreateSystem<MyUnmanagedSystem>();
            var unmanaged2 = World.CreateSystem<MyUnmanagedSystem2>();
            var unmanaged3 = World.CreateSystem<MyUnmanagedSystem3>();

            var managed1 = World.CreateSystemManaged<TestSystem>();
            var managed2 = World.CreateSystemManaged<TestSystem2>();
            var managed3 = World.CreateSystemManaged<TestSystem3>();

            group.AddSystemToUpdateList(unmanaged1);
            group.AddSystemToUpdateList(managed1);
            group.AddSystemToUpdateList(unmanaged2);
            group.AddSystemToUpdateList(managed2);
            group.AddSystemToUpdateList(unmanaged3);
            group.AddSystemToUpdateList(managed3);

            group.RemoveSystemFromUpdateList(unmanaged2);

            group.Update();

            var expectedUpdateList = new[]
            {
                new UpdateIndex(0, false),
                new UpdateIndex(0, true),
                new UpdateIndex(1, true),
                new UpdateIndex(1, false),
                new UpdateIndex(2, true)
            };

            for (int i = 0; i < expectedUpdateList.Length; ++i)
            {
                Assert.AreEqual(expectedUpdateList[i], group.m_MasterUpdateList[i]);
            }

            Assert.AreEqual(unmanaged1,group.m_UnmanagedSystemsToUpdate[0]);
            Assert.AreEqual(managed1.CheckedState()->m_SystemID,group.m_managedSystemsToUpdate[0].CheckedState()->m_SystemID);
            Assert.AreEqual(managed2.CheckedState()->m_SystemID,group.m_managedSystemsToUpdate[1].CheckedState()->m_SystemID);
            Assert.AreEqual(unmanaged3,group.m_UnmanagedSystemsToUpdate[1]);
            Assert.AreEqual(managed3.CheckedState()->m_SystemID,group.m_managedSystemsToUpdate[2].CheckedState()->m_SystemID);

        }

        [Test]
        public void ComponentSystemGroup_DisableAutoSorting_UpdatesInInsertionOrder()
        {
            var noSortGroup = World.CreateSystemManaged<NoSortGroup>();
            var child1 = World.CreateSystemManaged<Sibling1System>();
            var child2 = World.CreateSystemManaged<Sibling2System>();
            var unmanagedChild = World.CreateSystem<MyUnmanagedSystem>();
            noSortGroup.AddSystemToUpdateList(child1);
            noSortGroup.AddSystemToUpdateList(unmanagedChild);
            noSortGroup.AddSystemToUpdateList(child2);

            // Just adding the systems should cause them to be updated in insertion order
            var expectedUpdateList = new[]
            {
                new UpdateIndex(0, true),
                new UpdateIndex(0, false),
                new UpdateIndex(1, true),
            };
            CollectionAssert.AreEqual(new TestSystemBase[] {child1, child2}, noSortGroup.ManagedSystems);
            Assert.AreEqual(1, noSortGroup.UnmanagedSystems.Length);
            Assert.AreEqual(unmanagedChild, noSortGroup.UnmanagedSystems[0]);
            for (int i = 0; i < expectedUpdateList.Length; ++i)
            {
                Assert.AreEqual(expectedUpdateList[i], noSortGroup.m_MasterUpdateList[i]);
            }
            // Sorting the system group should have no effect on the update order
            noSortGroup.SortSystems();
            CollectionAssert.AreEqual(new TestSystemBase[] {child1, child2}, noSortGroup.ManagedSystems);
            Assert.AreEqual(1, noSortGroup.UnmanagedSystems.Length);
            Assert.AreEqual(unmanagedChild, noSortGroup.UnmanagedSystems[0]);
            for (int i = 0; i < expectedUpdateList.Length; ++i)
            {
                Assert.AreEqual(expectedUpdateList[i], noSortGroup.m_MasterUpdateList[i]);
            }
        }

        [UpdateAfter(typeof(EmptySystem))]
        public partial class GenericSystemThatDoesNotExistAtCompileTime<T> : SystemBase
        {
            protected override void OnUpdate()
            {
            }
        }


        public static SystemBase MakeGenericSystem<T>(World world)
        {
            return world.CreateSystemManaged<GenericSystemThatDoesNotExistAtCompileTime<T>>();
        }

        [Test]
        public void ComponentSystemGroup_SortsCorrectly_WithGenericSystemType_ConstructedAtRuntime()
        {
            var parentGroup = World.CreateSystemManaged<NoSortGroup>();
            parentGroup.SetSortingEnabled(false);

            var genericSystem = MakeGenericSystem<float>(World);
            parentGroup.AddSystemToUpdateList(sys: genericSystem);
            var emptySystem = World.CreateSystemManaged<EmptySystem>();
            parentGroup.AddSystemToUpdateList(emptySystem);
            
            CollectionAssert.AreEqual(new [] {genericSystem, emptySystem}, parentGroup.ManagedSystems);

            parentGroup.SetSortingEnabled(true);
            parentGroup.SortSystems();

            parentGroup.Update();
            var expectedUpdateList = new[]
            {
                new UpdateIndex(0, true),
                new UpdateIndex(1, true)
            };
            CollectionAssert.AreEqual(new [] {emptySystem, genericSystem}, parentGroup.ManagedSystems);

            for (int i = 0; i < expectedUpdateList.Length; ++i)
            {
                Assert.AreEqual(expectedUpdateList[i], parentGroup.m_MasterUpdateList[i]);
            }
        }
    }

    unsafe class ComponentSystemOrderingTests
    {
        // Note, we use a Native dictionary type to allow configs that don't support
        // Dictionary to run these tests.
        static NativeParallelHashMap<ulong, OrderInfo> SystemOrderInfoMap;
        NativeList<SystemTypeIndex> SystemTypeList;
        World TestWorld;
        static int SystemSequenceNo;

        [SetUp]
        public void SetUp()
        {
            var tmp = new List<SystemTypeIndex>()
            {
                TypeManager.GetSystemTypeIndex<TestSystemOrder0_0>(),
                TypeManager.GetSystemTypeIndex<TestSystemOrder3_4>(),
                TypeManager.GetSystemTypeIndex<TestSystemOrderA4_5>(),
                TypeManager.GetSystemTypeIndex<TestSystemOrderB4_7>(),
                TypeManager.GetSystemTypeIndex<TestSystemOrder5_8>(),
                TypeManager.GetSystemTypeIndex<TestSystemOrderB6_9>(),
                TypeManager.GetSystemTypeIndex<TestSystemOrderA7_6>(),
                TypeManager.GetSystemTypeIndex<TestISystemOrder1_3>(),
                TypeManager.GetSystemTypeIndex<TestSystemOrderB8_10>(),
                TypeManager.GetSystemTypeIndex<TestSystemOrder9_11>(),
                TypeManager.GetSystemTypeIndex<TestSystemOrder10_1>(),
                TypeManager.GetSystemTypeIndex<TestISystemOrder2_2>()
            };
            SystemTypeList = new NativeList<SystemTypeIndex>(tmp.Count, Allocator.Temp);
            foreach (var idx in tmp)
            {
                SystemTypeList.Add(idx);
            }

            SystemOrderInfoMap = new NativeParallelHashMap<ulong, OrderInfo>(SystemTypeList.Length, Allocator.Persistent);
            SystemSequenceNo = 0;

            // Make a custom world and manually add the systems
            // since we by default DisableAutoCreation all systems in the test assembly
            TestWorld = CreateWorldWithSystems(SystemTypeList);
        }

        World CreateWorldWithSystems(NativeList<SystemTypeIndex> systemList)
        {
            var world = new World("SystemOrdering World");

            // Normally this happens when fetching all systems based on world filter flags
            // but since we want to be very specific about which systems are registered with the world while using the machinery to do so
            // we sort them here like what would happen normally

            var copy = new NativeList<SystemTypeIndex>(systemList.Length, Allocator.Temp);
            copy.CopyFrom(systemList);

            TypeManager.SortSystemTypesInCreationOrder(copy);

            DefaultWorldInitialization.AddSystemToRootLevelSystemGroupsInternal(world, copy);

            return world;
        }


        [TearDown]
        public void TearDown()
        {
            if (TestWorld.IsCreated)
                TestWorld.Dispose();

            SystemOrderInfoMap.Dispose();
        }

        struct OrderInfo
        {
            public int CreateSeqNo;
            public int UpdateSeqNo;
            public int DestroySeqNo;
        }

        public abstract partial class TestSystemOrder0_0Base : SystemBase
        {

            protected override void OnCreate()
            {
                ulong statePtr = (ulong)m_StatePtr;
                Assert.IsTrue(!SystemOrderInfoMap.ContainsKey(statePtr));

                SystemOrderInfoMap.Add(statePtr, new OrderInfo { CreateSeqNo = SystemSequenceNo++, DestroySeqNo = -1 });
            }

            protected override void OnUpdate()
            {
                ulong statePtr = (ulong)m_StatePtr;
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(statePtr));

                var info = SystemOrderInfoMap[statePtr];
                info.UpdateSeqNo = SystemSequenceNo++;
                SystemOrderInfoMap[statePtr] = info;
            }

            protected override void OnDestroy()
            {
                ulong statePtr = (ulong)m_StatePtr;
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(statePtr));

                var info = SystemOrderInfoMap[statePtr];
                info.DestroySeqNo = SystemSequenceNo++;
                SystemOrderInfoMap[statePtr] = info;
            }
        }

        public partial class TestSystemOrder0_0 : TestSystemOrder0_0Base {

        }

        [CreateBefore(typeof(TestSystemOrder3_4))]
        [UpdateBefore(typeof(TestSystemOrder3_4))]
        [CreateAfter(typeof(TestSystemOrder10_1))]
        [UpdateAfter(typeof(TestSystemOrder10_1))]
        public partial struct TestISystemOrder1_3 : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                TestISystemCreate(ref state);
            }

            public void OnDestroy(ref SystemState state)
            {
                TestISystemDestroy(ref state);
            }

            public void OnUpdate(ref SystemState state)
            {
                TestISystemUpdate(ref state);
            }
        }

        [CreateAfter(typeof(TestISystemOrder1_3))]
        [UpdateAfter(typeof(TestISystemOrder1_3))]
        [CreateBefore(typeof(TestSystemOrder3_4))]
        [UpdateBefore(typeof(TestSystemOrder3_4))]
        public partial struct TestISystemOrder2_2 : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                TestISystemCreate(ref state);
            }

            public void OnDestroy(ref SystemState state)
            {
                TestISystemDestroy(ref state);
            }

            public void OnUpdate(ref SystemState state)
            {
                TestISystemUpdate(ref state);
            }
        }


        private static void TestISystemCreate(ref SystemState state)
        {
            fixed (SystemState* stateptr = &state)
            {
                ulong statePtrUlong = (ulong)stateptr;
                Assert.IsTrue(!SystemOrderInfoMap.ContainsKey(statePtrUlong));

                SystemOrderInfoMap.Add(statePtrUlong,
                    new OrderInfo { CreateSeqNo = SystemSequenceNo++, DestroySeqNo = -1 });
            }
        }

        private static void TestISystemDestroy(ref SystemState state)
        {
            fixed (SystemState* stateptr = &state)
            {
                ulong statePtrUlong = (ulong)stateptr;
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(statePtrUlong));

                var info = SystemOrderInfoMap[statePtrUlong];
                info.DestroySeqNo = SystemSequenceNo++;
                SystemOrderInfoMap[statePtrUlong] = info;
            }
        }

        private static void TestISystemUpdate(ref SystemState state)
        {
            fixed (SystemState* stateptr = &state)
            {
                ulong statePtrUlong = (ulong)stateptr;
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(statePtrUlong));

                var info = SystemOrderInfoMap[statePtrUlong];
                info.UpdateSeqNo = SystemSequenceNo++;
                SystemOrderInfoMap[statePtrUlong] = info;
            }
        }

        // Note, the naming is intentionally not alphabetical
        // just in case the reflection system returns types based on name. The order below
        // is expected in the order listed after the '_' (i.e. xxx_0 is before yyy_1 is before zzz_2)
        // Note the last system defined is early in the expected sort order as we want to ensure we handle system sorting if
        // reflection were to return systems based on the definition order as well

        /*
OnCreate: TestSystemOrder0_0                - No Attributes
OnCreate: TestSystemOrder9_1                - UpdateBefore 1_2
OnCreate: TestISystemOrder50_2              - No Attributes
OnCreate: TestSystemOrder1_3                - No Attributes
OnCreate: TestSystemOrderA2_4               - No Attributes
OnCreate: TestSystemOrderA6_5               - No Attributes
OnCreate: TestSystemOrderB3_6               - UpdateBefore 4_6
OnCreate: TestSystemOrder4_7                - No Attributes
OnCreate: TestSystemOrderB5_8               - No Attributes
OnCreate: TestSystemOrderB7_9               - No Attributes
OnCreate: TestSystemOrder8_10                - UpdateAfter 7_8
         */
        public partial class TestSystemOrder3_4 : TestSystemOrder0_0Base { }
        public partial class TestSystemOrderA4_5 : TestSystemOrder0_0Base { }

        [CreateBefore(typeof(TestSystemOrder5_8))]
        [UpdateBefore(typeof(TestSystemOrder5_8))]
        public partial class TestSystemOrderB4_7 : TestSystemOrder0_0Base { }

        public partial class TestSystemOrder5_8 : TestSystemOrder0_0Base { }
        public partial class TestSystemOrderB6_9 : TestSystemOrder0_0Base { }
        public partial class TestSystemOrderA7_6 : TestSystemOrder0_0Base { }
        public partial class TestSystemOrderB8_10 : TestSystemOrder0_0Base { }

        [CreateAfter(typeof(TestSystemOrderB8_10))]
        [UpdateAfter(typeof(TestSystemOrderB8_10))]
        public partial class TestSystemOrder9_11 : TestSystemOrder0_0Base { }

        [CreateBefore(typeof(TestSystemOrder3_4))]
        [UpdateBefore(typeof(TestSystemOrder3_4))]
        public partial class TestSystemOrder10_1 : TestSystemOrder0_0Base { }

        [Test]
        public void CreateBefore_CreateAfter_SanityCheck()
        {
            var systemList = new NativeList<SystemTypeIndex>(SystemTypeList.Length, Allocator.Temp);
            systemList.CopyFrom(SystemTypeList);
            TypeManager.SortSystemTypesInCreationOrder(systemList);

            // Confirm CreateBefore|After attributes influence create order
            {
                var a = (ulong)TestWorld.GetExistingSystemManaged<TestSystemOrderB4_7>().m_StatePtr;
                var b = (ulong)TestWorld.GetExistingSystemManaged<TestSystemOrder5_8>().m_StatePtr;
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(a));
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(b));
                Assert.Less(SystemOrderInfoMap[a].CreateSeqNo, SystemOrderInfoMap[b].CreateSeqNo);
            }

            {
                var a = (ulong)TestWorld.GetExistingSystemManaged<TestSystemOrder10_1>().m_StatePtr;
                var b = (ulong)TestWorld.GetExistingSystemManaged<TestSystemOrder3_4>().m_StatePtr;
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(a));
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(b));
                Assert.Less(SystemOrderInfoMap[a].CreateSeqNo, SystemOrderInfoMap[b].CreateSeqNo);
            }

            {
                var a = (ulong)TestWorld.GetExistingSystemManaged<TestSystemOrderB8_10>().m_StatePtr;
                var b = (ulong)TestWorld.GetExistingSystemManaged<TestSystemOrder9_11>().m_StatePtr;
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(a));
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(b));
                Assert.Less(SystemOrderInfoMap[a].CreateSeqNo, SystemOrderInfoMap[b].CreateSeqNo);
            }

            {
                var a = (ulong)TestWorld.Unmanaged.ResolveSystemState(TestWorld.Unmanaged
                    .GetExistingUnmanagedSystem<TestISystemOrder1_3>());
                var b = (ulong)TestWorld.GetExistingSystemManaged<TestSystemOrder3_4>().m_StatePtr;
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(a));
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(b));
                Assert.Less(SystemOrderInfoMap[a].CreateSeqNo, SystemOrderInfoMap[b].CreateSeqNo);
            }

            {
                var a = (ulong)TestWorld.GetExistingSystemManaged<TestSystemOrder10_1>().m_StatePtr;
                var b = (ulong)TestWorld.Unmanaged.ResolveSystemState(TestWorld.Unmanaged
                    .GetExistingUnmanagedSystem<TestISystemOrder1_3>());

                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(a));
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(b));
                Assert.Less(SystemOrderInfoMap[a].CreateSeqNo, SystemOrderInfoMap[b].CreateSeqNo);
            }
        }

        [Test]
        public void OnCreateOrderMatchesOnUpdateOrderWhenAttributesMatch()
        {
            // We know OnCreate will have been called before OnUpdate
            // and all systems will have called OnUpdate once. Thus
            // if creation order matches update order, the update order
            // sequence number should be the OnCreate sequence number +
            // the number of systems updating the sequence number
            TestWorld.Update();
            foreach (var system in TestWorld.Systems)
            {
                if (TypeManager.IsSystemAGroup(system.GetType()))
                    continue;
                ulong statePtr = (ulong)TestWorld.GetExistingSystemManaged(system.GetType()).m_StatePtr;
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(statePtr));
                var orderInfo = SystemOrderInfoMap[statePtr];

                Assert.AreEqual(orderInfo.CreateSeqNo + SystemTypeList.Length, orderInfo.UpdateSeqNo);
            }
        }

        [Test]
        public void OnDestroyOrderMatchesReverseOnCreateOrder()
        {
            // We are going to destroy the world so all the system ptrs will be inaccessible.
            // Cache them first so we can perform lookups in our map
            var deadPtrList = new List<ulong>();
            foreach (var type in SystemTypeList)
            {
                ulong statePtr;
                if (!type.IsManaged)
                    statePtr = (ulong)TestWorld.Unmanaged.ResolveSystemState(
                        TestWorld.Unmanaged.GetExistingUnmanagedSystem(type));
                else
                    statePtr = (ulong)TestWorld.GetExistingSystemManaged(type).m_StatePtr;
                deadPtrList.Add(statePtr);
            }

            // We know OnCreate will have been called before OnDestroy, the world has not updated
            // and all systems will have called OnDestroy once by destroying the world.
            // Thus if destroy order matches reverse create order, the sum of the creation order
            // and destruction order will equal twice the number of systems (-1 for 0 based counting).
            // Good:                   Bad:
            // A B C | C B A           A B C | B A C
            // 0 1 2 | 3 4 5           0 1 2 | 3 4 5
            // | | |-5-| | |    vs     | | |-7-^-^-|
            // | |---5---| |           | |---4-| |
            // |-----5-----|           |-----4---|

            TestWorld.Dispose();

            foreach (var statePtr in deadPtrList)
            {
                Assert.IsTrue(SystemOrderInfoMap.ContainsKey(statePtr));
                var orderInfo = SystemOrderInfoMap[statePtr];

                Assert.AreEqual(orderInfo.DestroySeqNo + orderInfo.CreateSeqNo, SystemTypeList.Length * 2 - 1);
            }
        }

        public partial class TestSystemBase : SystemBase { protected override void OnUpdate() { throw new NotImplementedException(); } }

        [CreateBefore(typeof(CircularSystem2))]
        public partial class CircularSystem1 : TestSystemBase { }

        [CreateBefore(typeof(CircularSystem1))]
        public partial class CircularSystem2 : TestSystemBase { }

        [CreateAfter(typeof(CircularSystem4))]
        public partial class CircularSystem3 : TestSystemBase { }

        [CreateAfter(typeof(CircularSystem3))]
        public partial class CircularSystem4 : TestSystemBase { }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires system safety checks")]
        public void Circular_CreateBefore_Throws()
        {
            var systems = new NativeList<SystemTypeIndex>(2, Allocator.Temp);
            systems.AddRange(new NativeArray<SystemTypeIndex>(new[]
            {
                TypeManager.GetSystemTypeIndex<CircularSystem1>(),
                TypeManager.GetSystemTypeIndex<CircularSystem2>()
            }, Allocator.Temp));
            var e = Assert.Throws<InvalidOperationException>(() =>
            {
                using (var world = CreateWorldWithSystems(systems)) { }
            });
            Assert.AreEqual(
                "The following systems form a circular dependency cycle (check their [*Before]/[*After] attributes):\n" +
                "- Unity.Entities.Tests.ComponentSystemOrderingTests+CircularSystem1\n" +
                "- Unity.Entities.Tests.ComponentSystemOrderingTests+CircularSystem2",
                e.Message);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires system safety checks")]
        public void Circular_CreateAfter_Throws()
        {
            var systems = new NativeList<SystemTypeIndex>(2, Allocator.Temp);
            systems.AddRange(new NativeArray<SystemTypeIndex>(new[]
            {
                TypeManager.GetSystemTypeIndex<CircularSystem3>(),
                TypeManager.GetSystemTypeIndex<CircularSystem4>()
            }, Allocator.Temp));
            var e =Assert.Throws<InvalidOperationException>(() =>
            {
                using (var world = CreateWorldWithSystems(systems)) { }
            });
            Assert.AreEqual(
                "The following systems form a circular dependency cycle (check their [*Before]/[*After] attributes):\n" +
                "- Unity.Entities.Tests.ComponentSystemOrderingTests+CircularSystem3\n" +
                "- Unity.Entities.Tests.ComponentSystemOrderingTests+CircularSystem4",
                e.Message);
        }
        
        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires system safety checks")]
        public void ComponentSystem_CreateAfterTargetIsNotSystem_LogsWarning()
        {
            var systems = new NativeList<SystemTypeIndex>(2, Allocator.Temp);
            systems.AddRange(new NativeArray<SystemTypeIndex>(new[]
            {
                TypeManager.GetSystemTypeIndex<CreateAfterIsntAComponentSystem>(),
            }, Allocator.Temp));
            using (var world = CreateWorldWithSystems(systems)) {

                LogAssert.Expect(LogType.Warning,
                    new Regex(
                        @"Ignoring invalid \[Unity.Entities.CreateAfterAttribute\].+CreateAfterIsntAComponentSystem.+Int32 is not a subclass of ComponentSystemBase and does not implement ISystem"));
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires system safety checks")]
        public void ComponentSystem_CreateBeforeTargetIsNotSystem_LogsWarning()
        {
            var systems = new NativeList<SystemTypeIndex>(2, Allocator.Temp);
            systems.AddRange(new NativeArray<SystemTypeIndex>(new[]
            {
                TypeManager.GetSystemTypeIndex<CreateBeforeIsntAComponentSystem>(),
            }, Allocator.Temp));
            using (var world = CreateWorldWithSystems(systems)) {

                LogAssert.Expect(LogType.Warning,
                    new Regex(
                        @"Ignoring invalid \[Unity.Entities.CreateBeforeAttribute\].+CreateBeforeIsntAComponentSystem.+Int32 is not a subclass of ComponentSystemBase and does not implement ISystem"));
            }
        }

        
        
        [CreateAfter(typeof(int))]
        public partial class CreateAfterIsntAComponentSystem : EmptySystem
        {
        }
        
        [CreateBefore(typeof(int))]
        public partial class CreateBeforeIsntAComponentSystem : EmptySystem
        {
        }
    }
}
