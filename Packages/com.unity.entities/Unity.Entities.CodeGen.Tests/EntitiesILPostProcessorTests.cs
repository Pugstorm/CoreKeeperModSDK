using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Unity.Entities.CodeGen.Tests
{
    [TestFixture]
    public class EntitiesILPostProcessorTests
    {
        class TestCompiledAssemblyWontProcess : ICompiledAssembly
        {
            public InMemoryAssembly InMemoryAssembly => new InMemoryAssembly(new byte[] {}, new byte[] {});
            public string Name { get => "Unity.Entities.TestCompiledAssemblyWontProcess"; }
            public string[] References { get => new string[] { }; }
            public string[] Defines { get => new string[] {}; }
        }

        [Test]
        public void EntitiesILPostProcessor_WontProcess()
        {
            var testCompiledAssemblyWontProcess = new TestCompiledAssemblyWontProcess();
            var entitiesILPostProcessors = new EntitiesILPostProcessors();

            Assert.False(entitiesILPostProcessors.WillProcess(testCompiledAssemblyWontProcess));
        }

        class TestCompiledAssemblyWillProcess : ICompiledAssembly
        {
            public InMemoryAssembly InMemoryAssembly => new InMemoryAssembly(new byte[] {}, new byte[] {});
            public string Name { get => "Unity.Entities.TestCompiledAssemblyWillProcess"; }
            public string[] References { get => new[] { "Unity.Entities.dll" }; }
            public string[] Defines { get => new string[] {}; }
        }

        [Test]
        public void EntitiesILPostProcessor_WillProcess()
        {
            var testCompiledAssemblyWillProcess = new TestCompiledAssemblyWillProcess();
            var entitiesILPostProcessors = new EntitiesILPostProcessors();

            Assert.True(entitiesILPostProcessors.WillProcess(testCompiledAssemblyWillProcess));
        }

        [Test]
        public void EntitiesILPostProcessor_WillTryToProcessWithProfileMarker()
        {
            var testCompiledAssemblyWillProcess = new TestCompiledAssemblyWillProcess();
            var entitiesILPostProcessors = new EntitiesILPostProcessors();
            EntitiesILPostProcessorProfileMarker.s_ToTestLog = true;
            EntitiesILPostProcessorProfileMarker.s_OmitZeroMSTimings = true;

            // This will throw a BadImageFormatException due to not being able to resolve the InMemoryAssembly
            // but should still emit one profiling marker.
            Assert.Throws<BadImageFormatException>(() =>
            {
                entitiesILPostProcessors.Process(testCompiledAssemblyWillProcess);
            });
            Assert.AreEqual(1, EntitiesILPostProcessorProfileMarker.s_TestLog.Count);
            Assert.IsTrue(EntitiesILPostProcessorProfileMarker.s_TestLog[0].Contains("- EILPP : Unity.Entities.TestCompiledAssemblyWillProcess"));
        }
    }
}
