using NUnit.Framework;
using Unity.Burst;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Collections.Tests
{
    // If ENABLE_UNITY_COLLECTIONS_CHECKS is not defined we will ignore the test
    // When using this attribute, consider it to logically AND with any other TestRequiresxxxx attrubute
#if ENABLE_UNITY_COLLECTIONS_CHECKS
    internal class TestRequiresCollectionChecks : System.Attribute
    {
        public TestRequiresCollectionChecks(string msg = null) { }
    }
#else
    internal class TestRequiresCollectionChecks : IgnoreAttribute
    {
        public TestRequiresCollectionChecks(string msg = null) : base($"Test requires ENABLE_UNITY_COLLECTION_CHECKS which is not defined{(msg == null ? "." : $": {msg}")}") { }
    }
#endif

    // If ENABLE_UNITY_COLLECTIONS_CHECKS and UNITY_DOTS_DEBUG is not defined we will ignore the test
    // conversely if either of them are defined the test will be run.
    // When using this attribute, consider it to logically AND with any other TestRequiresxxxx attrubute
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
    internal class TestRequiresDotsDebugOrCollectionChecks : System.Attribute
    {
        public TestRequiresDotsDebugOrCollectionChecks(string msg = null) { }
    }
#else
    internal class TestRequiresDotsDebugOrCollectionChecks : IgnoreAttribute
    {
        public TestRequiresDotsDebugOrCollectionChecks(string msg = null) : base($"Test requires UNITY_DOTS_DEBUG || ENABLE_UNITY_COLLECTION_CHECKS which neither are defined{(msg == null ? "." : $": {msg}")}") { }
    }
#endif

    internal class CollectionsTestCommonBase
    {
        AllocatorHelper<RewindableAllocator> rwdAllocatorHelper;

        protected AllocatorHelper<RewindableAllocator> CommonRwdAllocatorHelper => rwdAllocatorHelper;
        protected ref RewindableAllocator CommonRwdAllocator => ref rwdAllocatorHelper.Allocator;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            rwdAllocatorHelper = new AllocatorHelper<RewindableAllocator>(Allocator.Persistent);
            CommonRwdAllocator.Initialize(128 * 1024, true);
        }

        [SetUp]
        public virtual void Setup()
        {
#if UNITY_DOTSRUNTIME
            Unity.Runtime.TempMemoryScope.EnterScope();
#endif
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            CommonRwdAllocator.Dispose();
            rwdAllocatorHelper.Dispose();
        }

        [TearDown]
        public virtual void TearDown()
        {
            CommonRwdAllocator.Rewind();
            // This is test only behavior for determinism.  Rewind twice such that all
            // tests start with an allocator containing only one memory block.
            CommonRwdAllocator.Rewind();

#if UNITY_DOTSRUNTIME
            Unity.Runtime.TempMemoryScope.ExitScope();
#endif
        }
    }

    /// <summary>
    /// Collections test fixture to do setup and teardown.
    /// </summary>
    /// <remarks>
    /// Jobs debugger and safety checks should always be enabled when running collections tests. This fixture verifies
    /// those are enabled to prevent crashing the editor.
    /// </remarks>
    internal abstract class CollectionsTestFixture : CollectionsTestCommonBase
    {
#if !UNITY_DOTSRUNTIME
        static string SafetyChecksMenu = "Jobs > Burst > Safety Checks";
#endif
        private bool JobsDebuggerWasEnabled;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            // Many ECS tests will only pass if the Jobs Debugger enabled;
            // force it enabled for all tests, and restore the original value at teardown.
            JobsDebuggerWasEnabled = JobsUtility.JobDebuggerEnabled;
            JobsUtility.JobDebuggerEnabled = true;
#if !UNITY_DOTSRUNTIME
            Assert.IsTrue(BurstCompiler.Options.EnableBurstSafetyChecks, $"Collections tests must have Burst safety checks enabled! To enable, go to {SafetyChecksMenu}");
#endif
        }

        [TearDown]
        public override void TearDown()
        {
            JobsUtility.JobDebuggerEnabled = JobsDebuggerWasEnabled;

            base.TearDown();
        }
    }
}
