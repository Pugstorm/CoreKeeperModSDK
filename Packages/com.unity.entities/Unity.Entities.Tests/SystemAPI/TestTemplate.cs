// using NUnit.Framework;
//
// namespace Unity.Entities.Tests.TestSystemAPI
// {
//     [TestFixture]
//     public class TestISystem : ECSTestsFixture
//     {
//         [SetUp]
//         public void SetUp() => World.GetOrCreateSystem<TestISystemSystem>();
//         SystemRef<TestISystemSystem> GetTestSystemRef() => World.GetExistingSystem<TestISystemSystem>();
//
//         unsafe ref SystemState GetSystemStateRef<T>(SystemRef<T> state) where T : unmanaged, ISystem
//         {
//             var statePtr = World.Unmanaged.ResolveSystemState(state);
//             if (statePtr == null)
//                 throw new System.InvalidOperationException("No system state exists any more for this SystemRef");
//             return ref *statePtr;
//         }
//
//         [Test]
//         public void SimplestCase() => GetTestSystemRef().Struct.SimplestCase(ref GetSystemStateRef(GetTestSystemRef()));
//     }
//
//     partial struct TestISystemSystem : ISystem
//     {
//         public void SimplestCase(ref SystemState state) {}
//     }
// }
